using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine.Networking;

namespace GFM.GooglePackages
{
    public class GooglePackageParser
    {
        const string url = "https://developers.google.com/unity/archive";
        const string prefix = "https://dl.google.com/games/registry/unity/";
        const string extension = ".tgz";

        public List<GooglePackageInfo> Packages { get; private set; }

        public GooglePackageParser()
        {
            Packages = new List<GooglePackageInfo>();
        }

        public IEnumerator Load()
        {
            var unityWebRequest = UnityWebRequest.Get(url);
            var webRequest = unityWebRequest.SendWebRequest();

            while (!webRequest.isDone)
                yield return null;

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
        }

        private List<GooglePackageInfo> ParseInfo(List<string> links)
        {
            var infos = new List<GooglePackageInfo>();

            foreach (var link in links)
            {
                var name = GetName(link);
                var version = GetVersion(link);

                if (!name.StartsWith("com")) continue;

                var info = infos.FirstOrDefault(x => x.Name == name);
                if (info == null)
                {
                    info = new GooglePackageInfo(name);
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
}
