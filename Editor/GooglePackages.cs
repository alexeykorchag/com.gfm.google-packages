using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using System.Linq;
using System.IO;
using Newtonsoft.Json.Linq;

namespace GFM.GooglePackages
{
    public class GooglePackageMenu
    {
        [MenuItem("Tools/GooglePackageManager", false, 2)]
        public static void Show()
        {
            GooglePackageManager.ShowWindow();
        }
    }

    public class GooglePackageManager : EditorWindow
    {
        private const int Width = 430;
        private const int Height = 650;

        private readonly GUILayoutOption toggleWidth = GUILayout.Width(15);
        private readonly GUILayoutOption textWidth = GUILayout.Width(250);
        private readonly GUILayoutOption menuWidth = GUILayout.Width(75);
        private readonly GUILayoutOption buttonWidth = GUILayout.Width(75);

        private GUIStyle textStyle;

        private GooglePackageParser _packageParser = new GooglePackageParser();
        private GooglePackageLoader _packageLoader = new GooglePackageLoader();
        private GooglePackageDependencies _packageDependencies = new GooglePackageDependencies();

        public static void ShowWindow()
        {
            var win = GetWindowWithRect<GooglePackageManager>(new Rect(0, 0, Width, Height), true);
            win.titleContent = new GUIContent("Google Package Manager");
            win.Focus();
        }

        void Awake()
        {
            textStyle = new GUIStyle(EditorStyles.label)
            {
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft
            };
        }

        void OnGUI()
        {
            var btnLoad = GUILayout.Button(new GUIContent { text = "Load" });
            if (btnLoad)
            {
                GUI.enabled = true;
                EditorCoroutines.StartEditorCoroutine(Load());
            }

            foreach (var package in _packageParser.Packages)
            {
                using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandWidth(false)))
                {
                    package.IsToggle = EditorGUILayout.Toggle("", package.IsToggle, toggleWidth);

                    // Label
                    EditorGUILayout.LabelField(package.Name, textStyle, textWidth);

                    // DropDown
                    void MenuFunction(object userData)
                    {
                        package.Selected = userData as string;
                    }

                    var menu = new GenericMenu();

                    menu.AddItem(new GUIContent(PackageInfo.NONE), string.IsNullOrEmpty(package.Selected) || package.Selected == PackageInfo.NONE, MenuFunction, PackageInfo.NONE);
                    foreach (var version in package.Versions)
                        menu.AddItem(new GUIContent($"{version}"), package.Selected == version, MenuFunction, version);

                    if (EditorGUILayout.DropdownButton(new GUIContent(package.Selected), FocusType.Keyboard, menuWidth))
                    {
                        menu.ShowAsContext();
                    }

                    // Button 
                    var btnText = "None";
                    var guiEnable = false;

                    if (package.IsButtonRemove)
                    {
                        guiEnable = true;
                        btnText = "Remove";
                    }
                    else if (package.IsButtonInstall)
                    {
                        guiEnable = true;
                        btnText = (string.IsNullOrEmpty(package.Installed) || package.Installed == PackageInfo.NONE) ? "Install" : "Change";
                    }

                    GUI.enabled = guiEnable;
                    if (GUILayout.Button(new GUIContent { text = btnText }, buttonWidth))
                    {
                        ClickOnAction(package);
                    }
                    GUI.enabled = true;
                }
                GUILayout.Space(2);
            }
        }


        private IEnumerator Load()
        {
            EditorCoroutines.StartEditorCoroutine(_packageParser.Load());

            while (!_packageParser.IsDone)
                yield return new WaitForSeconds(0.1f);

            ReadDependencies();
        }

        private void ClickOnAction(PackageInfo package)
        {
            if (package.IsButtonRemove)
            {
                RemovePackage(package.Name);
            }
            else if (package.IsButtonInstall)
            {
                EditorCoroutines.StartEditorCoroutine(DownloadPackage(package.Name, package.Selected));
            }
        }

        private IEnumerator DownloadPackage(string packageName, string packageVersion)
        {
            _packageLoader.Remove(packageName);

            EditorCoroutines.StartEditorCoroutine(_packageLoader.Download(packageName, packageVersion));
            while (!_packageLoader.IsDone)
                yield return new WaitForSeconds(0.1f);

            _packageDependencies.AddPackage(packageName, packageVersion);
            _packageDependencies.Save();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            UpdateDependencies();
        }

        private void RemovePackage(string packageName)
        {
            _packageLoader.Remove(packageName);

            _packageDependencies.RemovePackage(packageName);
            _packageDependencies.Save();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            UpdateDependencies();
        }

        private void ReadDependencies()
        {
            _packageDependencies.Read();
            UpdateDependencies();
        }

        private void UpdateDependencies()
        {
            foreach (var package in _packageParser.Packages)
            {
                if (_packageDependencies.TryGetVersion(package.Name, out var value))
                {
                    package.Selected = value;
                    package.Installed = value;
                }
                else
                {
                    package.Selected = PackageInfo.NONE;
                    package.Installed = PackageInfo.NONE;
                }
            }
        }
    }


    public class GooglePackageParser
    {
        const string url = "https://developers.google.com/unity/archive";
        const string prefix = "https://dl.google.com/games/registry/unity/";
        const string extension = ".tgz";

        public bool IsDone { get; private set; }
        public List<PackageInfo> Packages { get; private set; }

        public GooglePackageParser()
        {
            Packages = new List<PackageInfo>();
        }

        public IEnumerator Load()
        {
            IsDone = false;

            var unityWebRequest = UnityWebRequest.Get(url);
            var webRequest = unityWebRequest.SendWebRequest();

            while (!webRequest.isDone)
                yield return new WaitForSeconds(0.1f);

            var links = new List<string>();

            var input = webRequest.webRequest.downloadHandler.text;
            var pattern = @"(http|ftp|https):\/\/([\w_-]+(?:(?:\.[\w_-]+)+))([\w.,@?^=%&:\/~+#-]*[\w@?^=%&\/~+#-])";

            var matches = Regex.Matches(input, pattern);
            foreach (var match in matches)
            {
                var value = match.ToString();
                if (value.EndsWith(extension))
                {
                    if (!links.Contains(value))
                        links.Add(value);
                }
            }

            Packages = ParseInfo(links);
            Sort();

            IsDone = true;
        }

        private List<PackageInfo> ParseInfo(List<string> links)
        {
            var infos = new List<PackageInfo>();

            foreach (var link in links)
            {
                var name = GetName(link);
                var version = GetVersion(link);

                if (!name.StartsWith("com")) continue;

                var info = infos.FirstOrDefault(x => x.Name == name);
                if (info == null)
                {
                    info = new PackageInfo(name);
                    infos.Add(info);
                }
                info.AddVersion(version);
            }
            return infos;
        }

        private static string GetName(string link)
        {
            var name = link.Replace(prefix, "");
            var index = name.IndexOf("/");
            name = name.Remove(index, name.Length - index);
            return name;
        }

        private static string GetVersion(string link)
        {
            var name = GetName(link);
            var version = link.Replace($"{prefix}{name}/{name}-", "").Replace(extension, "");
            return version;
        }

        private void Sort()
        {
            Packages.Sort((x1, x2) => x1.Name.CompareTo(x2.Name));

            foreach (var package in Packages)
                package.Versions.Sort((x1, x2) => x2.CompareTo(x1));
        }
    }

    public class GooglePackageLoader
    {
        const string prefix = "https://dl.google.com/games/registry/unity/";
        const string extension = ".tgz";

        const string packagesPath = "GooglePackages";

        public bool IsDone { get; private set; }

        public IEnumerator Download(string packageName, string packageVersion)
        {
            IsDone = false;

            var fileName = $"{packageName}-{packageVersion}{extension}";
            var url = $"{prefix}{packageName}/{fileName}";

            var unityWebRequest = UnityWebRequest.Get(url);
            var webRequest = unityWebRequest.SendWebRequest();

            while (!webRequest.isDone)
                yield return new WaitForSeconds(0.1f);

            if (!Directory.Exists(packagesPath))
                Directory.CreateDirectory(packagesPath);

            var path = Path.Combine(packagesPath, fileName);
            var data = webRequest.webRequest.downloadHandler.data;

            File.WriteAllBytes(path, data);

            IsDone = true;
        }

        public void Remove(string packageName)
        {
            if (!Directory.Exists(packagesPath))
                return;

            var files = Directory.GetFiles(packagesPath);
            foreach (var file in files)
            {
                var info = new FileInfo(file);
                if (info.Name.StartsWith(packageName))
                {
                    info.Delete();
                }
            }
        }

    }

    public class PackageInfo
    {
        private string _name;
        private List<string> _versions;

        public string Name => _name;
        public List<string> Versions => _versions;

        public bool IsToggle;
        public string Selected;
        public string Installed;

        public const string NONE = "None";
        public bool IsButtonRemove => (string.IsNullOrEmpty(Selected) || Selected == NONE) && !string.IsNullOrEmpty(Installed) && Installed != NONE;
        public bool IsButtonInstall => !string.IsNullOrEmpty(Selected) && Selected != NONE && Selected != Installed;



        public PackageInfo(string name)
        {
            _name = name;
            _versions = new List<string>();
        }

        public void AddVersion(string version)
        {
            _versions.Add(version);
        }
    }


    public class EditorCoroutines
    {
        readonly IEnumerator mRoutine;

        public static EditorCoroutines StartEditorCoroutine(IEnumerator routine)
        {
            EditorCoroutines coroutine = new EditorCoroutines(routine);
            coroutine.start();
            return coroutine;
        }

        EditorCoroutines(IEnumerator routine)
        {
            mRoutine = routine;
        }

        void start()
        {
            EditorApplication.update += update;
        }

        void update()
        {
            if (!mRoutine.MoveNext())
            {
                StopEditorCoroutine();
            }
        }

        public void StopEditorCoroutine()
        {
            EditorApplication.update -= update;
        }
    }

    public class GooglePackageDependencies
    {
        const string packagesManifestPath = "Packages/manifest.json";

        const string packagesPath = "file:../GooglePackages/";
        const string extension = ".tgz";

        private JObject jObject;
        private Dictionary<string, string> dependencies = new Dictionary<string, string>();

        public void Read()
        {
            var json = File.ReadAllText(packagesManifestPath);
            jObject = JObject.Parse(json);

            dependencies = jObject["dependencies"].ToObject<Dictionary<string, string>>();
        }

        public void AddPackage(string packageName, string packageVersion)
        {
            var value = $"{packagesPath}{packageName}-{packageVersion}{extension}";
            if (!dependencies.ContainsKey(packageName))
            {
                dependencies.Add(packageName, value);
            }
            else
            {
                dependencies[packageName] = value;
            }
        }

        public void RemovePackage(string packageName)
        {
            if (dependencies.ContainsKey(packageName))
            {
                dependencies.Remove(packageName);
            }
        }

        public bool TryGetVersion(string packageName, out string version)
        {
            version = "";

            if (!dependencies.TryGetValue(packageName, out var value))
                return false;

            value = value.Replace($"{packagesPath}{packageName}-", "").Replace($"{extension}", "");

            version = value;
            return true;
        }

        public void Save()
        {
            jObject["dependencies"] = JToken.FromObject(dependencies);
            File.WriteAllText(packagesManifestPath, jObject.ToString());
        }
    }
}
