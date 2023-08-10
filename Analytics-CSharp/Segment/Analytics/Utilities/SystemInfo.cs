using System.IO;
using System.Linq;
using System.Reflection;
using global::System;
using global::System.Runtime.InteropServices;

namespace Segment.Analytics.Utilities
{
    public static class SystemInfo
    {
        public static string GetAppFolder()
        {
            var type = Type.GetType("UnityEngine.Application, UnityEngine.CoreModule");
            string unityPath = type?.GetRuntimeProperty("persistentDataPath")?.GetValue(null, null).ToString();

            if (unityPath != null) return unityPath;

#if NETSTANDARD2_0
            return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
#else
            return Directory.GetCurrentDirectory();
#endif
        }

        public static string GetPlatform()
        {
            string type = "";

            if (Type.GetType("UnityEngine.Application, UnityEngine.CoreModule") != null)
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
            return RuntimeInformation.OSDescription;
        }

        public static bool AssemblyExists(string assembly)
        {
#if NETSTANDARD2_0
            return AppDomain.CurrentDomain.GetAssemblies().Where(a => a.ToString().StartsWith(assembly)).Count() > 0;
#else
            try
            {
                Assembly.Load(new AssemblyName(assembly));
                return true;
            }
            catch (Exception)
            {
                return false;
            }
#endif
        }
    }
}

