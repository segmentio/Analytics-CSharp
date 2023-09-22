// See https://aka.ms/new-console-template for more information

using System;
using ConsoleSample;
using Segment.Analytics;


var configuration = new Configuration("YOUR WRITE KEY",
    flushAt: 1,
    flushInterval: 10,
    exceptionHandler: new ErrorHandler());
var analytics = new Analytics(configuration);
Analytics.Logger = new SegmentLogger();

analytics.Identify("foo");
analytics.Track("track right after identify");

Console.ReadLine();


class ErrorHandler : IAnalyticsErrorHandler
{
    public void OnExceptionThrown(Exception e)
    {
        Console.WriteLine(e.StackTrace);
    }
}
