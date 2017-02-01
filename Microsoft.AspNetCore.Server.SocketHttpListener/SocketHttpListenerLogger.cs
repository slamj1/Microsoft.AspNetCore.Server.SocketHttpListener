using System;
using System.Text;
using Microsoft.Extensions.Logging;
using Patterns.Logging;
using SocketHttpListener.Net;
using ILogger = Patterns.Logging.ILogger;

namespace Microsoft.AspNetCore.Server.SocketHttpListener
{
	class SocketHttpListenerLogger : ILogger
	{
		private readonly Microsoft.Extensions.Logging.ILogger _logger;

		public SocketHttpListenerLogger(ILoggerFactory loggerFactory)
		{
			_logger = loggerFactory.CreateLogger(typeof(HttpListener).FullName);
		}

		public void Info(string message, params object[] paramList)
		{
			_logger.LogInformation(message, paramList);
		}

		public void Error(string message, params object[] paramList)
		{
			_logger.LogError(message, paramList);
		}

		public void Warn(string message, params object[] paramList)
		{
			_logger.LogWarning(message, paramList);
		}

		public void Debug(string message, params object[] paramList)
		{
			_logger.LogDebug(message, paramList);
		}

		public void Fatal(string message, params object[] paramList)
		{
			_logger.LogCritical(message, paramList);
		}

		public void FatalException(string message, Exception exception, params object[] paramList)
		{
			_logger.LogCritical(0, exception, message, paramList);
		}

		public void ErrorException(string message, Exception exception, params object[] paramList)
		{
			_logger.LogError(0, exception, message, paramList);
		}

		public void LogMultiline(string message, LogSeverity severity, StringBuilder additionalContent)
		{
			_logger.Log(GetLogLevel(severity), 0, 0, null, (state, error) =>
			{
				var sb = new StringBuilder(message);
				sb.AppendLine();
				sb.Append(additionalContent);
				return sb.ToString();
			});
		}

		public void Log(LogSeverity severity, string message, params object[] paramList)
		{
			_logger.Log(GetLogLevel(severity), 0, 0, null, 
				(state, error) => string.Format(message, paramList)
			);
		}

		private static LogLevel GetLogLevel(LogSeverity severity)
		{
			switch (severity)
			{
				case LogSeverity.Info:
					return LogLevel.Information;
				case LogSeverity.Debug:
					return LogLevel.Debug;
				case LogSeverity.Warn:
					return LogLevel.Warning;
				case LogSeverity.Error:
					return LogLevel.Error;
				case LogSeverity.Fatal:
					return LogLevel.Critical;
				default:
					throw new ArgumentOutOfRangeException(nameof(severity), severity, null);
			}
		}
	}
}
