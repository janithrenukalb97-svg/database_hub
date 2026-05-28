using System;
using System.IO;

namespace Database_Hub.Services
{
    public class ActionLoggerService
    {
        private readonly object _writeLock = new object();

        public string LogDirectory { get; }
        public string LogFilePath { get; }

        public ActionLoggerService()
        {
            LogDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log");
            Directory.CreateDirectory(LogDirectory);
            LogFilePath = Path.Combine(LogDirectory, $"database-hub-actions-{DateTime.Now:yyyyMMdd}.txt");
        }

        public void LogAction(string action, string status, string details)
        {
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] ACTION={action}; STATUS={status}; DETAILS={details}";
            WriteLine(line);
        }

        public void LogException(string action, Exception ex, string? details = null)
        {
            var safeDetails = details ?? string.Empty;
            LogAction(action, "FAILED", $"{safeDetails} Message={ex.Message}");
            WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] EXCEPTION={action}; STACK={ex.StackTrace ?? "(no stack trace)"}");
        }

        private void WriteLine(string line)
        {
            try
            {
                lock (_writeLock)
                {
                    File.AppendAllText(LogFilePath, line + Environment.NewLine);
                }
            }
            catch
            {
                // Never break app flow if logging fails.
            }
        }
    }
}