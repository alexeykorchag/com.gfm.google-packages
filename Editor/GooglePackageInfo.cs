using System.Collections.Generic;

namespace GFM.GooglePackages
{
    public class GooglePackageInfo
    {
        public string Name { get; private set; }
        public List<string> Versions { get; private set; }

        public string Selected;
        public string Installed;

        public const string NONE = "None";
        public bool IsButtonRemove => (string.IsNullOrEmpty(Selected) || Selected == NONE) && !string.IsNullOrEmpty(Installed) && Installed != NONE;
        public bool IsButtonInstall => !string.IsNullOrEmpty(Selected) && Selected != NONE && Selected != Installed;

        public GooglePackageInfo(string name)
        {
            Name = name;
            Versions = new List<string>();
        }

        public void AddVersion(string version)
        {
            Versions.Add(version);
        }
    }
}
