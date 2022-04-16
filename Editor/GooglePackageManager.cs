using System.Collections;
using System.Linq;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEngine;

namespace GFM.GooglePackages
{
    public class GooglePackageManager : EditorWindow
    {
        private const int Width = 430;
        private const int Height = 650;

        private readonly GUILayoutOption _toggleWidth = GUILayout.Width(15);
        private readonly GUILayoutOption _textWidth = GUILayout.Width(250);
        private readonly GUILayoutOption _menuWidth = GUILayout.Width(75);
        private readonly GUILayoutOption _buttonWidth = GUILayout.Width(75);

        private GUIStyle _textStyle;

        private GooglePackageParser _packageParser = new GooglePackageParser();
        private GooglePackageDependencies _packageDependencies = new GooglePackageDependencies();

        public static void ShowWindow()
        {
            var win = GetWindowWithRect<GooglePackageManager>(new Rect(0, 0, Width, Height), true);
            win.titleContent = new GUIContent("Google Package Manager");
            win.Focus();
        }

        void Awake()
        {
            _textStyle = new GUIStyle(EditorStyles.label)
            {
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft
            };

            EditorCoroutineUtility.StartCoroutine(Load(), this);
        }

        void OnGUI()
        {
            var btnLoad = GUILayout.Button(new GUIContent { text = "Load" });
            if (btnLoad)
            {
                GUI.enabled = true;
                EditorCoroutineUtility.StartCoroutine(Load(), this);
            }

            var btnApplyAll = GUILayout.Button(new GUIContent { text = "Apply All" });
            if (btnApplyAll)
            {
                GUI.enabled = _packageParser.Packages.Count > 0;
                var selectedPackages = _packageParser.Packages.Where(x => x.Selected != x.Installed).ToArray();
                EditorCoroutineUtility.StartCoroutine(ClickOnAction(selectedPackages), this);
            }

            foreach (var package in _packageParser.Packages)
            {
                using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandWidth(false)))
                {
                    // Label
                    EditorGUILayout.LabelField(package.Name, _textStyle, _textWidth);

                    // DropDown
                    void MenuFunction(object userData)
                    {
                        package.Selected = userData as string;
                    }

                    var menu = new GenericMenu();

                    menu.AddItem(new GUIContent(GooglePackageInfo.NONE), string.IsNullOrEmpty(package.Selected) || package.Selected == GooglePackageInfo.NONE, MenuFunction, GooglePackageInfo.NONE);
                    foreach (var version in package.Versions)
                        menu.AddItem(new GUIContent($"{version}"), package.Selected == version, MenuFunction, version);

                    if (EditorGUILayout.DropdownButton(new GUIContent(package.Selected), FocusType.Keyboard, _menuWidth))
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
                        btnText = (string.IsNullOrEmpty(package.Installed) || package.Installed == GooglePackageInfo.NONE) ? "Install" : "Change";
                    }

                    GUI.enabled = guiEnable;
                    if (GUILayout.Button(new GUIContent { text = btnText }, _buttonWidth))
                    {
                        EditorCoroutineUtility.StartCoroutine(ClickOnAction(package), this);
                    }
                    GUI.enabled = true;
                }
                GUILayout.Space(2);
            }
        }

        private IEnumerator Load()
        {
            yield return _packageParser.Load();

            ReadDependencies();
        }

        private IEnumerator ClickOnAction(params GooglePackageInfo[] packages)
        {
            EditorUtility.DisplayProgressBar("GooglePackageManager", "Start", 0);

            for (var i = 0; i < packages.Length; i++)
            {
                var package = packages[i];

                EditorUtility.DisplayProgressBar("GooglePackageManager", package.Name, i / (float)packages.Length);

                if (package.IsButtonRemove)
                {
                    RemovePackage(package);
                }
                else if (package.IsButtonInstall)
                {
                    yield return DownloadPackage(package);
                }
            }

            EditorUtility.DisplayProgressBar("GooglePackageManager", "Save", 1f);

            _packageDependencies.Save();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.ClearProgressBar();
        }

        private IEnumerator DownloadPackage(GooglePackageInfo package)
        {
            package.Installed = package.Selected;

            var packageName = package.Name;
            var packageVersion = package.Selected;

            var packageLoader = new GooglePackageLoader();
            packageLoader.Remove(packageName);

            yield return packageLoader.Download(packageName, packageVersion);

            _packageDependencies.AddPackage(packageName, packageVersion);
        }

        private void RemovePackage(GooglePackageInfo package)
        {
            package.Selected = GooglePackageInfo.NONE;
            package.Installed = GooglePackageInfo.NONE;

            var packageName = package.Name;

            var packageLoader = new GooglePackageLoader();
            packageLoader.Remove(packageName);

            _packageDependencies.RemovePackage(packageName);
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
                    package.Selected = GooglePackageInfo.NONE;
                    package.Installed = GooglePackageInfo.NONE;
                }
            }
        }
    }
}
