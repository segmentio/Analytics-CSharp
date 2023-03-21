using System.Linq;
using global::System;
using global::System.Runtime.InteropServices;

namespace Segment.Analytics.Utilities
{
    public static class SystemInfo
    {
        public static string GetAppFolder()
        {
            var type = Type.GetType("UnityEngine.Application, UnityEngine");
            string unityPath = type?.GetProperty("persistentDataPath")?.GetValue(null, null).ToString();
            return unityPath ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }

        public static string GetPlatform()
        {
            string type = "";

            if (Type.GetType("UnityEngine.Application, UnityEngine") != null)
            {
                type = "Unity";
            }
            else if (AssemblyExists("Xamarin.Forms.Core"))
            {
                type = "Xamarin";
            }
            else
            {
                string descr = RuntimeInformation.FrameworkDescription;
                string platf = descr.Substring(0, descr.LastIndexOf(' '));

                type = platf;
            }
            return type;
        }

        public static string GetOs()
        {
            OperatingSystem os = Environment.OSVersion;
            global::System.Version vs = os.Version;

            string operatingSystem = "";

            switch (os.Platform)
            {

                case PlatformID.Win32S:
                    operatingSystem = "Win32S";
                    break;
                case PlatformID.Win32Windows:
                    operatingSystem = "Win32Windows";
                    break;
                case PlatformID.Win32NT:
                    operatingSystem = "Win32NT";
                    break;
                case PlatformID.WinCE:
                    operatingSystem = "WinCE";
                    break;
                case PlatformID.Unix:
                    operatingSystem = "Unix";
                    break;
                case PlatformID.Xbox:
                    operatingSystem = "Xbox";
                    break;
                case PlatformID.MacOSX:
                    operatingSystem = "MacOSX";
                    break;
            }
            return operatingSystem + " - " + vs.Major + "." + vs.Minor;
        }

        public static bool AssemblyExists(string prefix)
        {
            return AppDomain.CurrentDomain.GetAssemblies().Where(a => a.ToString().StartsWith(prefix)).Count() > 0;
        }
    }
}

