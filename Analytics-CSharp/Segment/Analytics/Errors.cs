using System;
using Segment.Analytics.Utilities;
using Segment.Concurrent;

namespace Segment.Analytics
{
    public partial class Analytics
    {
        /// <summary>
        /// Reports an internal error to the user-defined error handler.
        /// </summary>
        /// <param name="error">Exception to report</param>
        /// <param name="message">Error message</param>
        public static void ReportInternalError(AnalyticsError error = null, string message = null)
        {
            Logger.Log(LogLevel.Error, error, message);
        }

        /// <summary>
        /// Extension method to reports an internal error to the user-defined error handler if
        /// analytics instance is available to access.
        /// </summary>
        /// <param name="type">Type of the analytics error</param>
        /// <param name="exception">Exception to throw</param>
        /// <param name="message">Error message</param>
        public static void ReportInternalError(AnalyticsErrorType type, Exception exception = null, string message = null)
        {
            ReportInternalError(new AnalyticsError(type, message, exception), message);
        }
    }

    public static class ExtensionMethods
    {
        /// <summary>
        /// Extension method to reports an internal error to the user-defined error handler if
        /// analytics instance is available to access.
        /// </summary>
        /// <param name="analytics">Segment Analytics</param>
        /// <param name="error">Exception to report</param>
        public static void ReportInternalError(this Analytics analytics, AnalyticsError error)
        {
            analytics.Configuration.AnalyticsErrorHandler?.OnExceptionThrown(error);
            Analytics.ReportInternalError(error, error.Message);
        }

        /// <summary>
        /// Extension method to reports an internal error to the user-defined error handler if
        /// analytics instance is available to access.
        /// </summary>
        /// <param name="analytics">Segment Analytics</param>
        /// <param name="type">Type of the analytics error</param>
        /// <param name="exception">Exception to throw</param>
        /// <param name="message">Error message</param>
        public static void ReportInternalError(this Analytics analytics, AnalyticsErrorType type, Exception exception = null, string message = null)
        {
            var error = new AnalyticsError(type, message, exception);
            analytics.Configuration.AnalyticsErrorHandler?.OnExceptionThrown(error);
            Analytics.ReportInternalError(error, error.Message);
        }
    }

    public class AnalyticsError : Exception
    {
        public AnalyticsErrorType ErrorType { get; }

        public AnalyticsError(AnalyticsErrorType type, string message = null, Exception exception = null) : base(message, exception)
        {
            ErrorType = type;
        }
    }

    public enum AnalyticsErrorType
    {
        StorageUnableToCreate,
        StorageUnableToWrite,
        StorageUnableToRename,
        StorageUnableToOpen,
        StorageUnableToRemove,
        StorageInvalid,
        StorageUnknown,

        NetworkUnexpectedHttpCode,
        NetworkServerLimited,
        NetworkServerRejected,
        NetworkUnknown,
        NetworkInvalidData,

        JsonUnableToSerialize,
        JsonUnableToDeserialize,
        JsonUnknown,

        PluginError,

        PayloadInvalid
    }

    public interface IAnalyticsErrorHandler : ICoroutineExceptionHandler {}

    internal class AnalyticsErrorHandlerAdapter : IAnalyticsErrorHandler
    {
        private readonly ICoroutineExceptionHandler _handler;

        public AnalyticsErrorHandlerAdapter(ICoroutineExceptionHandler handler)
        {
            _handler = handler;
        }

        public void OnExceptionThrown(Exception e) => _handler.OnExceptionThrown(e);
    }
}
