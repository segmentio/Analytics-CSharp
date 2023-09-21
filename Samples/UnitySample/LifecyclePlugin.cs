using System;
using System.Collections.Generic;
using Segment.Analytics;
using Segment.Serialization;
using UnityEngine;

namespace UnitySample
{

    /// <summary>
    /// Track your lifecycle events such as: installed, updated, opened, and backgrounded.
    /// Copy and paste the classes in this file and the <see cref="Singleton{T}"/> class to your unity project,
    /// and add the following one-liner:
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
        // use Segment's ConcurrentList to avoid modification during enumeration
        // or you have to make a copy for iterating the observers.
        private readonly IList<IObserver<State>> _observers = new ConcurrentList<IObserver<State>>();

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
            private IList<IObserver<State>> _observers;
            private IObserver<State> _observer;

            public Unsubscriber(IList<IObserver<State>> observers, IObserver<State> observer)
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
}
