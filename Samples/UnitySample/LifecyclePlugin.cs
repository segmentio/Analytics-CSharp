using Segment.Analytics;
using Segment.Serialization;
using UnityEngine;

namespace UnitySample
{
    /// <summary>
    /// Track your lifecycle events such as: installed, updated, opened, and backgrounded.
    /// Copy and paste this class to your unity project, and add the following one-liner:
    /// <code>
    /// analytics.Add(new LifecyclePlugin());
    /// </code>
    /// Now your lifecycle events are automatically tracked.
    /// </summary>
    public class LifecyclePlugin : Plugin
    {
        public override PluginType Type => PluginType.Utility;

        public override void Configure(Analytics analytics)
        {
            base.Configure(analytics);
            LifecycleObserver.Instance.Analytics = analytics;
        }


        #region Observer Classes

        /// <summary>
        /// A singleton component that listens unity events and reports to analytics
        /// </summary>
        public class LifecycleObserver : Singleton<LifecycleObserver>
        {
            public Analytics Analytics { get; set; }

            private const string AppVersionKey = "app_version";

            private void CheckVersion()
            {
                string currentVersion = Application.version;
                string previousVersion = PlayerPrefs.GetString(AppVersionKey);

                if (!PlayerPrefs.HasKey(AppVersionKey))
                {
                    OnApplicationInstalled(currentVersion);
                }
                else if (previousVersion != currentVersion)
                {
                    OnApplicationUpdated(previousVersion, currentVersion);
                }

                PlayerPrefs.SetString(AppVersionKey, currentVersion);
                PlayerPrefs.Save();
            }

            private void Start()
            {
                CheckVersion();
                Analytics?.Track("Application Opened");
            }

            private void OnApplicationPause(bool pauseStatus)
            {
                if (pauseStatus)
                {
                    Analytics?.Track("Application Backgrounded");
                }
            }

            private void OnApplicationUpdated(string previousVersion, string currentVersion)
            {
                Analytics?.Track("Application Updated",new JsonObject
                {
                    ["previous_version"] = previousVersion,
                    ["version"] = currentVersion
                });
            }

            private void OnApplicationInstalled(string currentVersion)
            {
                Analytics?.Track("Application Installed", new JsonObject
                {
                    ["version"] = currentVersion
                });
            }
        }

        /// <summary>
        /// A singleton template that adds component to the scene automatically and persists across scenes
        /// </summary>
        /// <typeparam name="T">Type of the Component</typeparam>
        public class Singleton<T> : MonoBehaviour where T : Component
        {
            private static T s_instance;

            public static T Instance
            {
                get
                {
                    if (s_instance == null)
                    {
                        s_instance = FindObjectOfType<T>();
                        if (s_instance == null)
                        {
                            s_instance = new GameObject("Segment Lifecycle Observer").AddComponent<T>();
                            DontDestroyOnLoad(s_instance.gameObject);
                        }
                    }

                    return s_instance;
                }
            }

            private void Awake()
            {
                if (s_instance == null)
                {
                    s_instance = this as T;
                    DontDestroyOnLoad(gameObject);
                }
                else
                {
                    Destroy(gameObject);
                }
            }
        }

        #endregion
    }
}
