using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core;
using UnityEditor;
using UnityEngine;

namespace Game.Unity.Editor
{
    /// <summary>
    /// Author hidden objects for a reveal set (day.png + night.png in
    /// Assets/Game/Resources/HiddenMosaic/Reveal/&lt;set&gt;/).
    /// - "Open Object Extractor" runs the Python tool (exact outlines + cutout icons); "Reload" picks up its export.
    /// - Outlined objects: drag a vertex to move it, Shift-click an edge to insert one, Ctrl/Cmd-click a vertex
    ///   to delete it, drag inside the shape to move the whole outline.
    /// - Rect-only objects: drag empty space to draw a box, drag a box to move it, drag its corner to resize.
    /// Saves objects.json in the format RevealLevelBuilder reads (outlines, icons and seeds round-trip).
    /// </summary>
    public sealed class RevealSetEditorWindow : EditorWindow
    {
        const string RevealRoot = "Assets/Game/Resources/HiddenMosaic/Reveal";
        const float SideWidth = 300f;
        const float Handle = 12f;
        const float VertexHandle = 7f;

        string[] _sets = new string[0];
        int _setIndex;
        Texture2D _day;
        Texture2D _night;
        int _width;
        int _height;
        string _background = "#101418";
        readonly List<RevealObjectDef> _objects = new List<RevealObjectDef>();
        int _selected = -1;
        bool _showNight;
        bool _showOutlines = true;
        bool _dirty;
        float _zoom = 1f;
        Vector2 _imageScroll;
        List<string> _errors = new List<string>();
        Vector2 _listScroll;

        enum Drag { None, Draw, Move, Resize, Vertex, MoveOutline }
        Drag _drag;
        Vector2Int _dragStart;
        RectInt _dragOrigin;
        RevealObjectDef _dragOriginDef;
        int _dragPath;
        int _dragVertex;

        [MenuItem("Nixin Studio/Hidden Mosaic/Reveal Set Editor")]
        static void Open() => GetWindow<RevealSetEditorWindow>("Reveal Set Editor");

        void OnEnable()
        {
            RefreshSets();
            if (_sets.Length > 0)
                Load(_sets[Mathf.Clamp(_setIndex, 0, _sets.Length - 1)]);
        }

        void RefreshSets()
        {
            _sets = Directory.Exists(RevealRoot)
                ? Directory.GetDirectories(RevealRoot).Select(Path.GetFileName).OrderBy(s => s).ToArray()
                : new string[0];
        }

        static string Folder(string set) => RevealRoot + "/" + set;

        void Load(string set)
        {
            _objects.Clear();
            _selected = -1;
            _dirty = false;
            AssetDatabase.ImportAsset(Folder(set), ImportAssetOptions.ImportRecursive);
            _day = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder(set) + "/day.png");
            _night = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder(set) + "/night.png");
            _width = _height = 0;
            if (_day != null && AssetImporter.GetAtPath(Folder(set) + "/day.png") is TextureImporter ti)
                ti.GetSourceTextureWidthAndHeight(out _width, out _height);

            var jsonPath = Folder(set) + "/objects.json";
            if (File.Exists(jsonPath))
            {
                var def = RevealLevelBuilder.Parse(set, File.ReadAllText(jsonPath));
                if (def != null)
                {
                    if (def.Width > 0) _width = def.Width;
                    if (def.Height > 0) _height = def.Height;
                    if (!string.IsNullOrEmpty(def.Background)) _background = def.Background;
                    _objects.AddRange(def.Objects);
                }
            }

            Validate();
        }

        RevealSetDef ToDef(string set) => new RevealSetDef(set, _width, _height, _objects.ToList(), _background);

        void Validate()
        {
            _errors = _sets.Length == 0 ? new List<string>() : RevealSetValidator.Validate(ToDef(CurrentSet));
            if (_day == null) _errors.Insert(0, "day.png missing");
            if (_night == null) _errors.Insert(0, "night.png missing");
        }

        string CurrentSet => _sets.Length == 0 ? "" : _sets[Mathf.Clamp(_setIndex, 0, _sets.Length - 1)];

        void Save()
        {
            var path = Folder(CurrentSet) + "/objects.json";
            File.WriteAllText(path, RevealLevelBuilder.ToJson(ToDef(CurrentSet)));
            AssetDatabase.ImportAsset(path);
            RevealSetAssetMenu.Sync(CurrentSet); // link objects.json into the set's RevealSet.asset
            _dirty = false;
            Validate();
            Debug.Log($"Reveal set '{CurrentSet}' saved: {_objects.Count} objects.");
        }

        void Replace(int index, RevealObjectDef def)
        {
            _objects[index] = def;
            _dirty = true;
        }

        void OpenExtractor(string path)
        {
            if (!HogObjectExtractLauncher.TryLaunch(path, out var error))
                EditorUtility.DisplayDialog("HOG Object Extractor", error, "OK");
        }

        void OnGUI()
        {
            if (_sets.Length == 0)
            {
                EditorGUILayout.HelpBox($"No reveal sets. Create a folder in {RevealRoot}/<name>/ with day.png and night.png (same size), or export one from the object extractor.", MessageType.Info);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Refresh")) RefreshSets();
                    if (GUILayout.Button("Open Object Extractor…")) OpenExtractor("");
                }

                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(SideWidth)))
                    DrawSide();
                var area = GUILayoutUtility.GetRect(10, 10, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                DrawImage(area);
            }
        }

        void DrawSide()
        {
            var next = EditorGUILayout.Popup("Set", _setIndex, _sets);
            if (next != _setIndex)
            {
                if (!_dirty || EditorUtility.DisplayDialog("Unsaved changes", "Discard changes to " + CurrentSet + "?", "Discard", "Cancel"))
                {
                    _setIndex = next;
                    Load(CurrentSet);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reload")) { RefreshSets(); Load(CurrentSet); }
                GUI.enabled = _dirty;
                if (GUILayout.Button(_dirty ? "Save *" : "Save")) Save();
                GUI.enabled = true;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Open Object Extractor", "Python tool: mark centres, auto-detect exact outlines, export here. Then press Reload.")))
                {
                    if (!_dirty || EditorUtility.DisplayDialog("Unsaved changes", "Save before opening the extractor? It reads objects.json.", "Save", "Skip"))
                    {
                        if (_dirty) Save();
                    }

                    OpenExtractor(Path.GetFullPath(Folder(CurrentSet)));
                }

                if (GUILayout.Button(new GUIContent("Reference Folder…", "Open the extractor on a folder of day/night reference images.")))
                {
                    var dir = EditorUtility.OpenFolderPanel("Reference images", "", "");
                    if (!string.IsNullOrEmpty(dir)) OpenExtractor(dir);
                }
            }

            EditorGUILayout.LabelField("Image", $"{_width} x {_height}");
            var bg = EditorGUILayout.TextField("Background", _background);
            if (bg != _background) { _background = bg; _dirty = true; }
            using (new EditorGUILayout.HorizontalScope())
            {
                _showNight = EditorGUILayout.ToggleLeft("Show night", _showNight, GUILayout.Width(100));
                _showOutlines = EditorGUILayout.ToggleLeft("Outlines", _showOutlines, GUILayout.Width(90));
            }

            _zoom = EditorGUILayout.Slider("Zoom", _zoom, 1f, 8f);

            EditorGUILayout.Space();
            var outlined = _objects.Count(o => o.HasOutline);
            EditorGUILayout.LabelField($"Objects ({_objects.Count}, {outlined} outlined)", EditorStyles.boldLabel);
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll, GUILayout.Height(260));
            for (var i = 0; i < _objects.Count; i++)
            {
                var style = i == _selected ? EditorStyles.helpBox : EditorStyles.label;
                var tag = _objects[i].HasOutline ? "◆" : "▭";
                if (GUILayout.Button($"{i + 1}. {tag} {_objects[i].Name}", style))
                    _selected = i;
            }

            EditorGUILayout.EndScrollView();

            if (_selected >= 0 && _selected < _objects.Count)
                DrawSelected();

            EditorGUILayout.Space();
            if (_errors.Count == 0)
                EditorGUILayout.HelpBox("Valid.", MessageType.Info);
            else
                EditorGUILayout.HelpBox(string.Join("\n", _errors), MessageType.Warning);
            EditorGUILayout.HelpBox(
                "◆ outlined (exact collider): drag vertex = move, Shift-click edge = add vertex, Ctrl/Cmd-click vertex = delete, drag inside = move outline.\n"
                + "▭ rect only: drag empty space = new box, drag box = move, drag corner = resize.\n"
                + "Names must be unique; levels need at least wave_size x waves objects.",
                MessageType.None);
        }

        void DrawSelected()
        {
            var o = _objects[_selected];
            var name = EditorGUILayout.TextField("Name", o.Name);
            if (name != o.Name) { Replace(_selected, o.WithName(name.Trim().Replace(' ', '_'))); Validate(); }
            if (o.HasOutline)
            {
                EditorGUILayout.LabelField("Outline", $"{o.Paths.Count} path(s), {o.Paths.Sum(p => p.VertexCount)} vertices, area {RevealGeometry.Area(o.Paths):0} px²");
                EditorGUILayout.LabelField("Bounds", $"{o.X}, {o.Y}  {o.Width} x {o.Height}");
                if (!string.IsNullOrEmpty(o.Icon)) EditorGUILayout.LabelField("Icon", o.Icon);
                if (GUILayout.Button(new GUIContent("Clear Outline (rect only)", "Falls back to a BoxCollider2D over the bounding box.")))
                {
                    Replace(_selected, o.WithPaths(null));
                    Validate();
                }
            }
            else
            {
                var r = EditorGUILayout.RectIntField("Rect (px)", new RectInt(o.X, o.Y, o.Width, o.Height));
                if (r.x != o.X || r.y != o.Y || r.width != o.Width || r.height != o.Height)
                {
                    Replace(_selected, o.WithRect(r.x, r.y, r.width, r.height));
                    Validate();
                }
            }

            if (GUILayout.Button("Delete"))
            {
                _objects.RemoveAt(_selected);
                _selected = -1;
                _dirty = true;
                Validate();
            }
        }

        void DrawImage(Rect area)
        {
            var tex = _showNight && _night != null ? _night : _day;
            if (tex == null || _width <= 0 || _height <= 0)
            {
                EditorGUI.HelpBox(area, "day.png not found / unreadable size.", MessageType.Warning);
                return;
            }

            var fit = Mathf.Min(area.width / _width, area.height / _height);
            var scale = fit * _zoom;
            var content = new Rect(0, 0, _width * scale, _height * scale);
            _imageScroll = GUI.BeginScrollView(area, _imageScroll, content);
            var img = content;
            GUI.DrawTexture(img, tex, ScaleMode.StretchToFill);

            for (var i = 0; i < _objects.Count; i++)
            {
                var o = _objects[i];
                var sel = i == _selected;
                var col = sel ? Color.yellow : (o.HasOutline ? new Color(0.3f, 0.9f, 1f) : new Color(0.3f, 1f, 0.4f));
                if (o.HasOutline && _showOutlines)
                    DrawOutline(o, img, scale, col, sel);
                else
                {
                    var r = ToScreen(new RectInt(o.X, o.Y, o.Width, o.Height), img, scale);
                    Handles.DrawSolidRectangleWithOutline(r, new Color(col.r, col.g, col.b, 0.12f), col);
                    if (sel && !o.HasOutline)
                        EditorGUI.DrawRect(new Rect(r.xMax - Handle, r.yMax - Handle, Handle, Handle), Color.yellow);
                }

                var label = ToScreen(new RectInt(o.X, o.Y, o.Width, o.Height), img, scale);
                GUI.Label(new Rect(label.x + 3, label.y - 15, 200, 18), o.Name, EditorStyles.whiteMiniLabel);
            }

            HandleMouse(img, scale);
            GUI.EndScrollView();
        }

        static void DrawOutline(RevealObjectDef o, Rect img, float scale, Color col, bool selected)
        {
            Handles.BeginGUI();
            for (var p = 0; p < o.Paths.Count; p++)
            {
                var path = o.Paths[p];
                var n = path.VertexCount;
                var pts = new Vector3[n + 1];
                for (var k = 0; k <= n; k++)
                {
                    var v = k % n;
                    pts[k] = new Vector3(img.x + path.Points[2 * v] * scale, img.y + path.Points[2 * v + 1] * scale, 0f);
                }

                Handles.color = path.Hole ? new Color(1f, 0.4f, 0.3f) : col;
                Handles.DrawAAPolyLine(selected ? 3f : 2f, pts);
                if (!selected)
                    continue;
                for (var k = 0; k < n; k++)
                {
                    var r = new Rect(pts[k].x - VertexHandle * 0.5f, pts[k].y - VertexHandle * 0.5f, VertexHandle, VertexHandle);
                    EditorGUI.DrawRect(r, Color.black);
                    EditorGUI.DrawRect(new Rect(r.x + 1, r.y + 1, r.width - 2, r.height - 2), Color.white);
                }
            }

            Handles.EndGUI();
        }

        static Rect ToScreen(RectInt r, Rect img, float scale) =>
            new Rect(img.x + r.x * scale, img.y + r.y * scale, r.width * scale, r.height * scale);

        Vector2Int ToPixel(Vector2 mouse, Rect img, float scale) =>
            new Vector2Int(Mathf.Clamp(Mathf.RoundToInt((mouse.x - img.x) / scale), 0, _width),
                           Mathf.Clamp(Mathf.RoundToInt((mouse.y - img.y) / scale), 0, _height));

        Vector2 ToPixelF(Vector2 mouse, Rect img, float scale) =>
            new Vector2(Mathf.Clamp((mouse.x - img.x) / scale, 0, _width), Mathf.Clamp((mouse.y - img.y) / scale, 0, _height));

        /// <summary>Topmost object under the pixel: outline test for outlined objects, rect otherwise; smallest wins.</summary>
        int PickObject(Vector2 px)
        {
            var best = -1;
            var bestArea = float.MaxValue;
            for (var i = 0; i < _objects.Count; i++)
            {
                var o = _objects[i];
                bool inside;
                float area;
                if (o.HasOutline)
                {
                    inside = RevealGeometry.Contains(o.Paths, px.x, px.y);
                    area = RevealGeometry.Area(o.Paths);
                }
                else
                {
                    inside = px.x >= o.X && px.y >= o.Y && px.x <= o.X + o.Width && px.y <= o.Y + o.Height;
                    area = o.Width * o.Height;
                }

                if (inside && area < bestArea)
                {
                    bestArea = area;
                    best = i;
                }
            }

            return best;
        }

        bool FindVertex(RevealObjectDef o, Vector2 mouse, Rect img, float scale, out int path, out int vertex)
        {
            path = vertex = -1;
            var bestD = VertexHandle * VertexHandle;
            for (var p = 0; p < o.Paths.Count; p++)
            {
                var pts = o.Paths[p].Points;
                for (var k = 0; k < o.Paths[p].VertexCount; k++)
                {
                    var s = new Vector2(img.x + pts[2 * k] * scale, img.y + pts[2 * k + 1] * scale);
                    var d = (s - mouse).sqrMagnitude;
                    if (d < bestD)
                    {
                        bestD = d;
                        path = p;
                        vertex = k;
                    }
                }
            }

            return path >= 0;
        }

        bool FindEdge(RevealObjectDef o, Vector2 mouse, Rect img, float scale, out int path, out int afterVertex)
        {
            path = afterVertex = -1;
            var bestD = 6f;
            for (var p = 0; p < o.Paths.Count; p++)
            {
                var pts = o.Paths[p].Points;
                var n = o.Paths[p].VertexCount;
                for (var k = 0; k < n; k++)
                {
                    var a = new Vector2(img.x + pts[2 * k] * scale, img.y + pts[2 * k + 1] * scale);
                    var j = (k + 1) % n;
                    var b = new Vector2(img.x + pts[2 * j] * scale, img.y + pts[2 * j + 1] * scale);
                    var d = HandleUtility.DistancePointToLineSegment(mouse, a, b);
                    if (d < bestD)
                    {
                        bestD = d;
                        path = p;
                        afterVertex = k;
                    }
                }
            }

            return path >= 0;
        }

        static RevealObjectDef WithPath(RevealObjectDef o, int pathIndex, List<float> points)
        {
            var paths = o.Paths.ToList();
            paths[pathIndex] = new RevealPathDef(o.Paths[pathIndex].Hole, points.ToArray());
            return o.WithPaths(paths);
        }

        void HandleMouse(Rect img, float scale)
        {
            var e = Event.current;
            if (e.button != 0)
                return;
            var px = ToPixel(e.mousePosition, img, scale);
            var pxf = ToPixelF(e.mousePosition, img, scale);
            var sel = _selected >= 0 && _selected < _objects.Count ? _objects[_selected] : null;

            switch (e.type)
            {
                case EventType.MouseDown when img.Contains(e.mousePosition):
                {
                    _drag = Drag.None;
                    _dragStart = px;

                    // Vertex edits on the selected outline first.
                    if (sel != null && sel.HasOutline && _showOutlines)
                    {
                        if (FindVertex(sel, e.mousePosition, img, scale, out var vp, out var vk))
                        {
                            if (e.control || e.command)
                            {
                                if (sel.Paths[vp].VertexCount > 3)
                                {
                                    var pts = sel.Paths[vp].Points.ToList();
                                    pts.RemoveRange(2 * vk, 2);
                                    Replace(_selected, WithPath(sel, vp, pts));
                                    Validate();
                                }

                                e.Use();
                                Repaint();
                                break;
                            }

                            _drag = Drag.Vertex;
                            _dragPath = vp;
                            _dragVertex = vk;
                            e.Use();
                            break;
                        }

                        if (e.shift && FindEdge(sel, e.mousePosition, img, scale, out var ep, out var ek))
                        {
                            var pts = sel.Paths[ep].Points.ToList();
                            pts.InsertRange(2 * (ek + 1), new[] { pxf.x, pxf.y });
                            Replace(_selected, WithPath(sel, ep, pts));
                            _drag = Drag.Vertex;
                            _dragPath = ep;
                            _dragVertex = ek + 1;
                            e.Use();
                            Repaint();
                            break;
                        }
                    }

                    // Rect corner resize on a selected rect-only object.
                    if (sel != null && !sel.HasOutline)
                    {
                        var sr = ToScreen(new RectInt(sel.X, sel.Y, sel.Width, sel.Height), img, scale);
                        var corner = new Rect(sr.xMax - Handle, sr.yMax - Handle, Handle * 2, Handle * 2);
                        if (corner.Contains(e.mousePosition))
                        {
                            _drag = Drag.Resize;
                            _dragOrigin = new RectInt(sel.X, sel.Y, sel.Width, sel.Height);
                            e.Use();
                            break;
                        }
                    }

                    var hit = PickObject(pxf);
                    if (hit >= 0)
                    {
                        _selected = hit;
                        var o = _objects[hit];
                        _dragOriginDef = o;
                        _dragOrigin = new RectInt(o.X, o.Y, o.Width, o.Height);
                        _drag = o.HasOutline ? Drag.MoveOutline : Drag.Move;
                    }
                    else
                    {
                        _objects.Add(new RevealObjectDef(UniqueName("object"), px.x, px.y, 0, 0));
                        _selected = _objects.Count - 1;
                        _drag = Drag.Draw;
                    }

                    e.Use();
                    Repaint();
                    break;
                }
                case EventType.MouseDrag when _drag != Drag.None && sel != null:
                {
                    var d = px - _dragStart;
                    switch (_drag)
                    {
                        case Drag.Draw:
                        {
                            var min = Vector2Int.Min(_dragStart, px);
                            var max = Vector2Int.Max(_dragStart, px);
                            Replace(_selected, sel.WithRect(min.x, min.y, max.x - min.x, max.y - min.y));
                            break;
                        }
                        case Drag.Move:
                            Replace(_selected, sel.WithRect(
                                Mathf.Clamp(_dragOrigin.x + d.x, 0, _width - _dragOrigin.width),
                                Mathf.Clamp(_dragOrigin.y + d.y, 0, _height - _dragOrigin.height),
                                _dragOrigin.width, _dragOrigin.height));
                            break;
                        case Drag.Resize:
                            Replace(_selected, sel.WithRect(_dragOrigin.x, _dragOrigin.y,
                                Mathf.Max(4, Mathf.Min(_dragOrigin.width + d.x, _width - _dragOrigin.x)),
                                Mathf.Max(4, Mathf.Min(_dragOrigin.height + d.y, _height - _dragOrigin.y))));
                            break;
                        case Drag.MoveOutline:
                        {
                            var dx = Mathf.Clamp(d.x, -_dragOrigin.x, _width - _dragOrigin.xMax);
                            var dy = Mathf.Clamp(d.y, -_dragOrigin.y, _height - _dragOrigin.yMax);
                            Replace(_selected, _dragOriginDef.Translated(dx, dy));
                            break;
                        }
                        case Drag.Vertex:
                        {
                            var pts = sel.Paths[_dragPath].Points.ToList();
                            pts[2 * _dragVertex] = Mathf.Round(pxf.x * 10f) / 10f;
                            pts[2 * _dragVertex + 1] = Mathf.Round(pxf.y * 10f) / 10f;
                            Replace(_selected, WithPath(sel, _dragPath, pts));
                            break;
                        }
                    }

                    e.Use();
                    Repaint();
                    break;
                }
                case EventType.MouseUp when _drag != Drag.None:
                {
                    if (_drag == Drag.Draw && sel != null && (sel.Width < 8 || sel.Height < 8))
                    {
                        _objects.RemoveAt(_selected);
                        _selected = -1;
                        _dirty = true;
                    }

                    _drag = Drag.None;
                    Validate();
                    e.Use();
                    Repaint();
                    break;
                }
            }
        }

        string UniqueName(string stem)
        {
            for (var i = 1; ; i++)
            {
                var n = stem + "_" + i;
                if (_objects.All(b => b.Name != n))
                    return n;
            }
        }
    }
}
