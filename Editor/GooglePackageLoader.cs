using System.Collections;
using System.IO;
using UnityEngine.Networking;

namespace GFM.GooglePackages
{
    public class GooglePackageLoader
    {
        const string prefix = "https://dl.google.com/games/registry/unity/";
        const string extension = ".tgz";

        const string packagesPath = "GooglePackages";

        public IEnumerator Download(string packageName, string packageVersion)
        {
            var fileName = $"{packageName}-{packageVersion}{extension}";
            var url = $"{prefix}{packageName}/{fileName}";

            var unityWebRequest = UnityWebRequest.Get(url);
            var webRequest = unityWebRequest.SendWebRequest();

            while (!webRequest.isDone)
                yield return null;

            if (!Directory.Exists(packagesPath))
                Directory.CreateDirectory(packagesPath);

            var path = Path.Combine(packagesPath, fileName);
            var data = webRequest.webRequest.downloadHandler.data;

            File.WriteAllBytes(path, data);
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
}
