// See https://aka.ms/new-console-template for more information

using System;
using System.Threading;
using Segment.Analytics;
using Segment.Analytics.Utilities;
using Segment.Concurrent;


var configuration = new Configuration("YOUR WRITE KEY",
    flushAt: 1,
    flushInterval: 10,
    exceptionHandler: new ErrorHandler());
var analytics = new Analytics(configuration);
Analytics.Logger = new SegmentLogger();

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

class SegmentLogger : ISegmentLogger
{
    public void Log(LogLevel logLevel, Exception exception = null, string message = null)
    {
        switch (logLevel)
        {
            case LogLevel.Warning:
            case LogLevel.Information:
            case LogLevel.Debug:
                Console.Out.WriteLine("Message: " + message);
                break;
            case LogLevel.Critical:
            case LogLevel.Trace:
            case LogLevel.Error:
                Console.Error.WriteLine("Exception: " + exception?.StackTrace);
                Console.Error.WriteLine("Message: " + message);
                break;
            case LogLevel.None:
            default:
                break;
        }
    }
}
