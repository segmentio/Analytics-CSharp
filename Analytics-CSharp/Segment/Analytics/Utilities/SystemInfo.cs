using global::System;
using global::System.Runtime.InteropServices;

namespace Segment.Analytics.Utilities
{
    public class SystemInfo
    {
        public static string getAppFolder()
        {
            var type = Type.GetType("UnityEngine.Application, UnityEngine");
            string unityPath = type?.GetProperty("persistentDataPath").GetValue(null, null).ToString();
            if (unityPath != null)
            {
                return unityPath;
            }

            return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }

        public static string getPlatform()
        {
            string type = "";

            if (Type.GetType("Xamarin.Forms.Device") != null)
            {
                type = "Xamarin";
            }
            else if (Type.GetType("UnityEngine.Application, UnityEngine") != null)
            {
                type = "Unity";
            }
            else
            {
                string descr = RuntimeInformation.FrameworkDescription;
                string platf = descr.Substring(0, descr.LastIndexOf(' '));

                type = platf;
            }
            return type;
        }

        public static string getOS()
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
                default:
                    break;
            }
            return operatingSystem + " - " + vs.Major + "." + vs.Minor;
        }
    }
}

