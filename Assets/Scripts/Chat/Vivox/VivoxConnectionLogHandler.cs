using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Unity.Services.Vivox;
using UnityEngine;

namespace BingoGame.Chat
{
    internal sealed class VivoxConnectionLogHandler : ILogHandler
    {
        internal sealed class ConnectionAttempt : IDisposable
        {
            internal readonly HashSet<string> KnownFailures = new();
            internal bool Active = true;
            internal bool WarningReported;
            internal string CapturedDetails;

            public void ReportFailure(Exception _exception)
            {
                if (!IsConnectionException(_exception))
                {
                    Debug.LogException(_exception);
                    return;
                }

                string details = GetExceptionDetails(_exception);
                lock (gate)
                {
                    KnownFailures.Add(GetFailureKey(details));
                    if (WarningReported) return;
                    WarningReported = true;
                }

                installedHandler.previousHandler.LogFormat(LogType.Warning, null,
                    "[Vivox Connection] Chat connection attempt failed: {0}", BuildWarning(details));
            }

            public void Dispose()
            {
                string details = null;
                lock (gate)
                {
                    Active = false;
                    if (!WarningReported && !string.IsNullOrEmpty(CapturedDetails))
                    {
                        WarningReported = true;
                        details = CapturedDetails;
                    }
                }
                if (details != null)
                    installedHandler.previousHandler.LogFormat(LogType.Warning, null,
                        "[Vivox Connection] The SDK reported a connection error: {0}", BuildWarning(details));
            }
        }

        private static readonly object gate = new();
        private static readonly Regex statusPattern = new(@"\((-?\d+)\)", RegexOptions.CultureInvariant);
        private static readonly Regex requestPattern = new(@"\[RequestId:\s*([^\]]+)\]", RegexOptions.CultureInvariant);
        private static VivoxConnectionLogHandler installedHandler;
        private static ConnectionAttempt currentAttempt;
        private readonly ILogHandler previousHandler;

        private VivoxConnectionLogHandler(ILogHandler _previousHandler)
        {
            previousHandler = _previousHandler;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetAttempts()
        {
            lock (gate) currentAttempt = null;
        }

        internal static void Install()
        {
            if (installedHandler != null) return;
            installedHandler = new VivoxConnectionLogHandler(Debug.unityLogger.logHandler);
            Debug.unityLogger.logHandler = installedHandler;
        }

        internal static ConnectionAttempt BeginAttempt()
        {
            Install();
            lock (gate)
            {
                currentAttempt = new ConnectionAttempt();
                return currentAttempt;
            }
        }

        public void LogFormat(LogType _logType, UnityEngine.Object _context, string _format, params object[] _args)
        {
            if (_logType == LogType.Error || _logType == LogType.Exception)
            {
                string message = FormatMessage(_format, _args);
                if (IsSdkConnectionLog(message) && CaptureFailure(message)) return;
            }
            previousHandler.LogFormat(_logType, _context, _format, _args);
        }

        public void LogException(Exception _exception, UnityEngine.Object _context)
        {
            if (IsConnectionException(_exception))
            {
                string details = GetExceptionDetails(_exception);
                string trace = _exception.StackTrace ?? string.Empty;
                bool sdkOrigin = _exception is VivoxApiException || trace.Contains("Unity.Services.Vivox.");
                if (sdkOrigin && CaptureFailure(details)) return;
            }
            previousHandler.LogException(_exception, _context);
        }

        private static bool CaptureFailure(string _details)
        {
            string key = GetFailureKey(_details);
            lock (gate)
            {
                if (currentAttempt == null) return false;
                if (!currentAttempt.Active) return currentAttempt.KnownFailures.Contains(key);
                currentAttempt.KnownFailures.Add(key);
                if (currentAttempt.CapturedDetails == null)
                {
                    int lineEnd = _details.IndexOfAny(new[] { '\r', '\n' });
                    currentAttempt.CapturedDetails = lineEnd >= 0 ? _details.Substring(0, lineEnd) : _details;
                }
                return true;
            }
        }

        private static bool IsSdkConnectionLog(string _message)
        {
            if (!_message.StartsWith("[Vivox]", StringComparison.Ordinal)) return false;
            bool connectionStage = _message.Contains("vx_req_connector_create_t failed") ||
                _message.Contains("BeginGetConnectorHandle failed") || _message.Contains("BeginLogin");
            bool expectedException = _message.Contains("VivoxApiException:") ||
                _message.Contains("TimeoutException:") || _message.Contains("HttpRequestException:") ||
                _message.Contains("WebException:") || _message.Contains("SocketException:");
            return connectionStage && expectedException;
        }

        private static bool IsConnectionException(Exception _exception)
        {
            if (_exception == null) return false;
            if (_exception is AggregateException aggregate)
            {
                foreach (Exception inner in aggregate.InnerExceptions)
                    if (!IsConnectionException(inner)) return false;
                return aggregate.InnerExceptions.Count > 0;
            }
            string typeName = _exception.GetType().FullName;
            return _exception is VivoxApiException || _exception is TimeoutException ||
                typeName == "System.Net.Http.HttpRequestException" || typeName == "System.Net.WebException" ||
                typeName == "System.Net.Sockets.SocketException";
        }

        private static string GetExceptionDetails(Exception _exception)
        {
            if (_exception is AggregateException aggregate && aggregate.InnerExceptions.Count == 1)
                return GetExceptionDetails(aggregate.InnerExceptions[0]);
            return $"{_exception.GetType().Name}: {_exception.Message}";
        }

        private static string GetFailureKey(string _details)
        {
            Match status = statusPattern.Match(_details);
            Match request = requestPattern.Match(_details);
            if (status.Success)
                return $"status:{status.Groups[1].Value};request:{request.Groups[1].Value}";
            if (_details.Contains("TimeoutException")) return "timeout";
            if (_details.Contains("HttpRequestException")) return "http-connection";
            if (_details.Contains("WebException")) return "web-connection";
            if (_details.Contains("SocketException")) return "socket-connection";
            return _details;
        }

        private static string BuildWarning(string _details)
        {
            Match status = statusPattern.Match(_details);
            string reason;
            if (status.Success && int.TryParse(status.Groups[1].Value, out int code))
            {
                switch (code)
                {
                    case 400: reason = "Vivox rejected the connection request"; break;
                    case 401: reason = "Vivox authentication failed"; break;
                    case 403: reason = "Vivox denied access"; break;
                    case 404: reason = "Vivox returned Not Found while connecting"; break;
                    case 408: case 504: reason = "The Vivox connection request timed out"; break;
                    case 429: reason = "Vivox rejected the request because of rate limiting"; break;
                    default: reason = code >= 500 && code <= 599
                        ? "Vivox reported a service error" : "Vivox reported a connection error"; break;
                }
            }
            else reason = _details.Contains("TimeoutException")
                ? "The Vivox connection attempt timed out" : "Could not establish the Vivox connection";

            return $"{reason}. {_details.Replace('\r', ' ').Replace('\n', ' ')}";
        }

        private static string FormatMessage(string _format, object[] _args)
        {
            if (_args == null || _args.Length == 0) return _format ?? string.Empty;
            try { return string.Format(_format, _args); }
            catch (FormatException) { return _format ?? string.Empty; }
        }
    }
}
