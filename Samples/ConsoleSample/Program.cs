// See https://aka.ms/new-console-template for more information

using System;
using System.Threading;
using Segment.Analytics;
using Segment.Concurrent;


var configuration = new Configuration("YOUR WRITE KEY",
    persistentDataPath: "temp",
    flushAt: 1,
    flushInterval: 10,
    exceptionHandler: new ErrorHandler());
var analytics = new Analytics(configuration);

analytics.Identify("foo");
analytics.Track("track right after identify");

Console.ReadLine();


class ErrorHandler : ICoroutineExceptionHandler
{
    public void OnExceptionThrown(Exception e)
    {
        Console.WriteLine(e.StackTrace);
    }
}