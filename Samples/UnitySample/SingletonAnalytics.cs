using System;
using Segment.Analytics;
using Segment.Concurrent;
using UnityEngine;

namespace UnitySample
{
    public class SingletonAnalytics : Singleton<SingletonAnalytics>
    {
        public Analytics Analytics { get; set; }

        protected override void Awake()
        {
            // you don't have to use `UnityHTTPClientProvider`
            // the default httpClientProvider works on Unity, too.
            Configuration configuration =
                new Configuration("YOUR WRITE KEY",
                    exceptionHandler: new ErrorHandler(),
                    httpClientProvider: new UnityHTTPClientProvider(MainThreadDispatcher.Instance));
            Analytics = new Analytics(configuration);
            Analytics.Add(new LifecyclePlugin());
        }

        class ErrorHandler : ICoroutineExceptionHandler
        {
            public void OnExceptionThrown(Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}
