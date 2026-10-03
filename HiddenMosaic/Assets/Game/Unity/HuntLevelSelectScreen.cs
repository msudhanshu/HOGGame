using Game.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Unity
{
    public sealed class HuntLevelSelectScreen : MonoBehaviour
    {
        const int Columns = 3;

        HuntCatalog _catalog;
        Transform _contentRoot;
        GameObject _root;

        void Awake()
        {
            EnsureSceneRefs();
            WireBackButton();
        }

        public void BindCatalog(HuntCatalog catalog)
        {
            _catalog = catalog;
            EnsureSceneRefs();
            WireBackButton();
        }

        public void Build(Transform parent, HuntCatalog catalog)
        {
            _catalog = catalog;
            _root = HuntMenuUi.MakePanel("LevelSelect", parent, new Color(0.05f, 0.06f, 0.08f, 1f));
            _root.SetActive(false);

            var title = HuntMenuUi.MakeText("Title", _root.transform, 48, FontStyle.Bold, TextAnchor.MiddleCenter);
            title.text = "Select Level";
            var titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -24f);
            titleRect.sizeDelta = new Vector2(0f, 72f);

            var back = HuntMenuUi.MakeButton(_root.transform, "Back", new Vector2(200f, 56f), new Color(0.22f, 0.24f, 0.32f, 0.95f));
            var backRect = back.GetComponent<RectTransform>();
            backRect.anchorMin = new Vector2(0f, 1f);
            backRect.anchorMax = new Vector2(0f, 1f);
            backRect.pivot = new Vector2(0f, 1f);
            backRect.anchoredPosition = new Vector2(24f, -24f);
            back.onClick.AddListener(Hide);

            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
            scrollGo.transform.SetParent(_root.transform, false);
            var scrollRect = scrollGo.GetComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0f, 0f);
            scrollRect.anchorMax = new Vector2(1f, 1f);
            scrollRect.offsetMin = new Vector2(16f, 16f);
            scrollRect.offsetMax = new Vector2(-16f, -120f);
            scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.15f);

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(scrollGo.transform, false);
            var viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            viewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.02f);
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            _contentRoot = content.transform;
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            var vlg = content.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 16f;
            vlg.padding = new RectOffset(8, 8, 8, 8);
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            var fitter = content.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.viewport = viewportRect;
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.vertical = true;

            RebuildGrid();
        }

        public void Show()
        {
            if (_catalog == null)
                _catalog = HuntCatalogLoader.LoadJsonFromResources();
            EnsureSceneRefs();
            WireBackButton();
            if (_root != null)
                _root.SetActive(true);
            RebuildGrid();
        }

        public         void Hide()
        {
            if (_root != null)
                _root.SetActive(false);
        }

        void EnsureSceneRefs()
        {
            if (_root == null)
            {
                var levelSelect = transform.Find("LevelSelect");
                if (levelSelect != null)
                    _root = levelSelect.gameObject;
            }

            if (_contentRoot == null && _root != null)
            {
                var content = _root.transform.Find("Scroll/Viewport/Content");
                if (content != null)
                    _contentRoot = content;
            }
        }

        void WireBackButton()
        {
            if (_root == null)
                return;
            HuntMenuUi.WireClick(_root.transform, "BackButton", Hide);
        }

        void RebuildGrid()
        {
            if (_contentRoot == null || _catalog == null)
                return;
            for (var i = _contentRoot.childCount - 1; i >= 0; i--)
                Destroy(_contentRoot.GetChild(i).gameObject);

            var count = _catalog.Count;
            if (count == 0)
                return;

            var rowCount = (count + Columns - 1) / Columns;
            for (var row = 0; row < rowCount; row++)
            {
                var rowGo = new GameObject("Row_" + row, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
                rowGo.transform.SetParent(_contentRoot, false);
                var rowLayout = rowGo.GetComponent<HorizontalLayoutGroup>();
                rowLayout.spacing = 12f;
                rowLayout.childAlignment = TextAnchor.UpperCenter;
                rowLayout.childControlWidth = true;
                rowLayout.childControlHeight = false;
                rowLayout.childForceExpandWidth = true;
                rowLayout.childForceExpandHeight = false;
                rowGo.GetComponent<LayoutElement>().preferredHeight = 400f;

                for (var col = 0; col < Columns; col++)
                {
                    var index = row * Columns + col;
                    if (index >= count)
                    {
                        var spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
                        spacer.transform.SetParent(rowGo.transform, false);
                        spacer.GetComponent<LayoutElement>().flexibleWidth = 1f;
                        continue;
                    }

                    var cellGo = new GameObject("Cell_" + index, typeof(RectTransform));
                    var cell = cellGo.AddComponent<HuntLevelCell>();
                    cell.Build(rowGo.transform);
                    cell.Bind(index, _catalog[index], OnLevelPicked);
                    var cellLayout = cellGo.GetComponent<LayoutElement>();
                    if (cellLayout == null)
                        cellLayout = cellGo.AddComponent<LayoutElement>();
                    cellLayout.flexibleWidth = 1f;
                    cellLayout.preferredHeight = 380f;
                }
            }
        }

        void OnLevelPicked(int index)
        {
            HuntLaunchRequest.SetSingle(index);
            SceneManager.LoadScene(HuntScenes.Play);
        }
    }
}
