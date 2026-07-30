using System;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class QosConsoleLogFilter
{
    private sealed class FilteredLogHandler : ILogHandler
    {
        private readonly ILogHandler defaultLogHandler;

        public FilteredLogHandler(ILogHandler defaultLogHandler)
        {
            this.defaultLogHandler = defaultLogHandler;
        }

        public void LogFormat(
            LogType logType,
            UnityEngine.Object context,
            string format,
            params object[] args)
        {
            string message = GetMessage(format, args);

            if (logType == LogType.Log &&
                message.StartsWith(
                    "QosJob:",
                    StringComparison.Ordinal))
            {
                return;
            }

            defaultLogHandler.LogFormat(
                logType,
                context,
                format,
                args);
        }

        public void LogException(
            Exception exception,
            UnityEngine.Object context)
        {
            defaultLogHandler.LogException(
                exception,
                context);
        }

        private string GetMessage(
            string format,
            object[] args)
        {
            if (args == null || args.Length == 0)
            {
                return format ?? string.Empty;
            }

            try
            {
                return string.Format(format, args);
            }
            catch
            {
                return format ?? string.Empty;
            }
        }
    }

    static QosConsoleLogFilter()
    {
        ILogHandler currentLogHandler =
            Debug.unityLogger.logHandler;

        if (currentLogHandler is FilteredLogHandler)
        {
            return;
        }

        Debug.unityLogger.logHandler =
            new FilteredLogHandler(
                currentLogHandler);
    }
}