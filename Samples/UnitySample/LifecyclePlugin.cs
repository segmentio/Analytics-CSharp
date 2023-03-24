using System;
using System.Collections.Generic;
using Segment.Analytics;
using Segment.Serialization;
using UnityEngine;

namespace UnitySample
{

    /// <summary>
    /// Track your lifecycle events such as: installed, updated, opened, and backgrounded.
    /// Copy and paste the classes in this file to your unity project, and add the following one-liner:
    /// <code>
    /// analytics.Add(new LifecyclePlugin());
    /// </code>
    /// Now your lifecycle events are automatically tracked.
    /// </summary>
    public class LifecyclePlugin : Plugin, IObserver<Lifecycle.State>
    {
        public override PluginType Type => PluginType.Utility;

        private IDisposable _unsubscriber;

        public override void Configure(Analytics analytics)
        {
            base.Configure(analytics);
            _unsubscriber = Lifecycle.Instance.Subscribe(this);
        }

        public void OnNext(Lifecycle.State newState)
        {
            Analytics.Track(newState.Message, newState.Properties);
        }

        public override void Shutdown()
        {
            base.Shutdown();
            _unsubscriber?.Dispose();
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }
    }


    #region Observer Classes

    /// <summary>
    /// A singleton component that listens unity events and reports to its observers
    /// </summary>
    public class Lifecycle : Singleton<Lifecycle>, IObservable<Lifecycle.State>
    {
        private readonly List<IObserver<State>> _observers = new();

        private const string AppVersionKey = "app_version";

        private void CheckVersion()
        {
            string currentVersion = Application.version;
            string previousVersion = PlayerPrefs.GetString(AppVersionKey);

            if (!PlayerPrefs.HasKey(AppVersionKey))
            {
                NotifyObservers(new State
                {
                    Message = "Application Installed", Properties = new JsonObject {["version"] = currentVersion}
                });
            }
            else if (previousVersion != currentVersion)
            {
                NotifyObservers(new State
                {
                    Message = "Application Updated",
                    Properties = new JsonObject
                    {
                        ["previous_version"] = previousVersion, ["version"] = currentVersion
                    }
                });
            }

            PlayerPrefs.SetString(AppVersionKey, currentVersion);
            PlayerPrefs.Save();
        }

        private void Start()
        {
            CheckVersion();
            NotifyObservers(new State {Message = "Application Opened"});
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                NotifyObservers(new State {Message = "Application Backgrounded"});
            }
        }

        public void NotifyObservers(State newState)
        {
            foreach (var observer in _observers)
            {
                observer.OnNext(newState);
            }
        }

        public IDisposable Subscribe(IObserver<State> observer)
        {
            if (!_observers.Contains(observer))
            {
                _observers.Add(observer);
            }

            return new Unsubscriber(_observers, observer);
        }

        private class Unsubscriber : IDisposable
        {
            private List<IObserver<State>> _observers;
            private IObserver<State> _observer;

            public Unsubscriber(List<IObserver<State>> observers, IObserver<State> observer)
            {
                _observers = observers;
                _observer = observer;
            }

            public void Dispose()
            {
                if (_observer != null && _observers.Contains(_observer))
                    _observers.Remove(_observer);
            }
        }

        /// <summary>
        /// Lifecycle state that contains the data send over to analytics
        /// </summary>
        public class State
        {
            public string Message { get; set; }
            public JsonObject Properties { get; set; }
        }
    }

    #endregion

    #region Singleton Tempalte

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
                        s_instance = new GameObject("Segment Singleton").AddComponent<T>();
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
