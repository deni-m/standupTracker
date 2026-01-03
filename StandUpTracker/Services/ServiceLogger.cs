using System;
using System.IO;
using StandUpTracker.Config;

namespace StandUpTracker.Services
{
    /// <summary>
    /// Thread-safe service logger that writes timestamped logs to daily files.
    /// </summary>
    public class ServiceLogger
    {
        private readonly object _logLock = new object();
        private readonly string _logFolder;

        public ServiceLogger()
        {
            _logFolder = Path.Combine(AppSettings.LogsFolder, "Service");
            Directory.CreateDirectory(_logFolder);
        }

        public void Log(string level, string category, string message, params object[] args)
        {
            try
            {
                var formattedMessage = args.Length > 0 ? string.Format(message, args) : message;
                var logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] [{category}] {formattedMessage}";

                lock (_logLock)
                {
                    var fileName = $"service_{DateTime.Now:yyyy-MM-dd}.log";
                    var filePath = Path.Combine(_logFolder, fileName);
                    File.AppendAllText(filePath, logEntry + Environment.NewLine);
                }
            }
            catch
            {
                // Cannot log logging errors - would create infinite loop
            }
        }

        public void Info(string category, string message, params object[] args) => Log("INFO", category, message, args);
        public void Warning(string category, string message, params object[] args) => Log("WARN", category, message, args);
        public void Error(string category, string message, params object[] args) => Log("ERROR", category, message, args);
        public void Debug(string category, string message, params object[] args) => Log("DEBUG", category, message, args);
    }
}
