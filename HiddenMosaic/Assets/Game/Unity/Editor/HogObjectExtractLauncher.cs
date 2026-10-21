using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Game.Unity.Editor
{
    /// <summary>
    /// Launches the Python HOG object extractor (NixinStudioUnityCore/tools/hog-object-extract) GUI.
    /// Python finds outlines + cutout icons and writes Reveal/&lt;set&gt;/objects.json; the Reveal Set Editor reloads it.
    /// </summary>
    public static class HogObjectExtractLauncher
    {
        const string ToolFolder = "hog-object-extract";
        const string Module = "hog_object_extract";
        static readonly string[] VenvUnixNames = { "python3.14", "python3.13", "python3.12", "python3.11", "python3", "python" };
        const string Menu = "Nixin Studio/Hidden Mosaic/HOG Object Extractor/";
        const string RevealRoot = "Assets/Game/Resources/HiddenMosaic/Reveal";

        [MenuItem(Menu + "Open Extractor", priority = 200)]
        static void MenuOpen() => LaunchOrShow("");

        [MenuItem(Menu + "Open Reference Folder…", priority = 201)]
        static void MenuOpenFolder()
        {
            var dir = EditorUtility.OpenFolderPanel("Reference images (day/night)", EditorPrefs.GetString(PrefLastFolder, ""), "");
            if (string.IsNullOrEmpty(dir))
                return;
            EditorPrefs.SetString(PrefLastFolder, dir);
            LaunchOrShow(dir);
        }

        [MenuItem(Menu + "Open Selected Reveal Set", priority = 202)]
        static void MenuOpenSelected() => LaunchOrShow(Path.GetFullPath(SelectedRevealSet()));

        [MenuItem(Menu + "Open Selected Reveal Set", true)]
        static bool MenuOpenSelectedValidate() => !string.IsNullOrEmpty(SelectedRevealSet());

        [MenuItem(Menu + "Reveal Folder in Finder", priority = 220)]
        static void MenuRevealTool() => EditorUtility.RevealInFinder(ToolRoot());

        const string PrefLastFolder = "Nixin.HiddenMosaic.HogExtract.LastFolder";

        static void LaunchOrShow(string path)
        {
            if (!TryLaunch(path, out var error))
                EditorUtility.DisplayDialog("HOG Object Extractor", error, "OK");
        }

        /// <summary>Reveal/&lt;set&gt; folder of the selected asset (the folder itself or anything inside it); empty if none.</summary>
        static string SelectedRevealSet()
        {
            var path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrEmpty(path) || !path.StartsWith(RevealRoot + "/"))
                return "";
            var rest = path.Substring(RevealRoot.Length + 1);
            var slash = rest.IndexOf('/');
            return RevealRoot + "/" + (slash < 0 ? rest : rest.Substring(0, slash));
        }

        public static string ToolRoot()
        {
            var dir = new DirectoryInfo(Application.dataPath);
            while (dir != null)
            {
                var sibling = Path.Combine(dir.FullName, "NixinStudioUnityCore", "tools", ToolFolder);
                if (Directory.Exists(sibling))
                    return Path.GetFullPath(sibling);
                var nested = Path.Combine(dir.FullName, "tools", ToolFolder);
                if (Directory.Exists(nested))
                    return Path.GetFullPath(nested);
                dir = dir.Parent;
            }

            return Path.GetFullPath(Path.Combine(Application.dataPath, "../../../NixinStudioUnityCore/tools", ToolFolder));
        }

        public static string PythonExe()
        {
            var root = ToolRoot();
            var unixBin = Path.Combine(root, ".venv", "bin");
            foreach (var name in VenvUnixNames)
            {
                var path = Path.Combine(unixBin, name);
                if (File.Exists(path)) // venv pythons are symlinks; File.Exists follows them
                    return path;
            }

            var windows = Path.Combine(root, ".venv", "Scripts", "python.exe");
            return File.Exists(windows) ? windows : null;
        }

        /// <param name="openPath">Folder (a Reveal set or a reference-image folder) or a day image; empty = blank GUI.</param>
        public static bool TryLaunch(string openPath, out string error)
        {
            error = null;
            var root = ToolRoot();
            if (!Directory.Exists(root))
            {
                error = "Could not find " + ToolFolder + " at:\n" + root;
                return false;
            }

            var python = PythonExe();
            if (string.IsNullOrEmpty(python))
            {
                error = "Object extractor venv not found at:\n" + root
                    + "\n\nIn Terminal:\ncd " + root
                    + "\npython3 -m venv .venv\n.venv/bin/pip install -r requirements.txt"
                    + "\n# optional, exact shapes on painted art:\n.venv/bin/pip install -r requirements-sam.txt";
                return false;
            }

            var args = "-m " + Module;
            if (!string.IsNullOrEmpty(openPath))
                args += " --set \"" + Path.GetFullPath(openPath) + "\"";

            var psi = new ProcessStartInfo
            {
                FileName = python,
                Arguments = args,
                WorkingDirectory = root,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            foreach (var key in new[] { "PYTHONHOME", "PYTHONPATH", "PYTHONEXECUTABLE", "__PYVENV_LAUNCHER__", "TCL_LIBRARY", "TK_LIBRARY", "TCLLIBPATH", "DYLD_INSERT_LIBRARIES" })
            {
                if (psi.EnvironmentVariables.ContainsKey(key))
                    psi.EnvironmentVariables.Remove(key);
            }

            psi.EnvironmentVariables["PYTHONUNBUFFERED"] = "1";
            psi.EnvironmentVariables["PYTHONPATH"] = root;

            try
            {
                var proc = new Process { StartInfo = psi };
                var log = new StringBuilder();
                proc.OutputDataReceived += (_, e) => { if (e.Data != null) log.AppendLine(e.Data); };
                proc.ErrorDataReceived += (_, e) => { if (e.Data != null) log.AppendLine(e.Data); };
                if (!proc.Start())
                {
                    error = "Process.Start returned false for:\n" + python;
                    return false;
                }

                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
                if (proc.WaitForExit(1500))
                {
                    var detail = log.ToString().Trim();
                    var logPath = Path.Combine(root, "out", "unity-launch.log");
                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(logPath));
                        File.WriteAllText(logPath, detail);
                    }
                    catch
                    {
                        // log is best effort
                    }

                    error = "Object extractor exited immediately (code " + proc.ExitCode + ").\n"
                        + python + " " + args + "\n\n" + (string.IsNullOrEmpty(detail) ? "No output. See " + logPath : detail);
                    return false;
                }

                Debug.Log("Opened HOG object extractor (" + python + " " + args + ")");
                return true;
            }
            catch (Exception ex)
            {
                error = ex.GetType().Name + ": " + ex.Message + "\n\npython=" + python + "\nroot=" + root;
                return false;
            }
        }
    }
}
