using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;

namespace GFM.GooglePackages
{
    public class GooglePackageDependencies
    {
        const string packagesManifestPath = "Packages/manifest.json";

        const string packagesPath = "file:../GooglePackages/";
        const string extension = ".tgz";

        private JObject _jObject;
        private Dictionary<string, string> _dependencies = new Dictionary<string, string>();

        public void Read()
        {
            var json = File.ReadAllText(packagesManifestPath);
            _jObject = JObject.Parse(json);

            _dependencies = _jObject["dependencies"].ToObject<Dictionary<string, string>>();
        }

        public void AddPackage(string packageName, string packageVersion)
        {
            var value = $"{packagesPath}{packageName}-{packageVersion}{extension}";
            if (!_dependencies.ContainsKey(packageName))
            {
                _dependencies.Add(packageName, value);
            }
            else
            {
                _dependencies[packageName] = value;
            }
        }

        public void RemovePackage(string packageName)
        {
            if (_dependencies.ContainsKey(packageName))
            {
                _dependencies.Remove(packageName);
            }
        }

        public bool TryGetVersion(string packageName, out string version)
        {
            version = "";

            if (!_dependencies.TryGetValue(packageName, out var value))
                return false;

            value = value.Replace($"{packagesPath}{packageName}-", "").Replace($"{extension}", "");

            version = value;
            return true;
        }

        public void Save()
        {
            _jObject["dependencies"] = JToken.FromObject(_dependencies);
            File.WriteAllText(packagesManifestPath, _jObject.ToString());
        }
    }
}
