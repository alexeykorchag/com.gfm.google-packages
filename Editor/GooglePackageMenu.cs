using UnityEditor;

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
}
