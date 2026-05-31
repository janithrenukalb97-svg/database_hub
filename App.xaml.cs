using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Database_Hub
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static readonly object CrashLogLock = new object();

        protected override void OnStartup(StartupEventArgs e)
        {
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

            base.OnStartup(e);
            var bootstrapper = new Bootstrapper();
            bootstrapper.Run();
        }

        private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            WriteCrashLog("DispatcherUnhandledException", e.Exception);
            e.Handled = true;
        }

        private static void OnCurrentDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            WriteCrashLog("AppDomainUnhandledException", ex, $"IsTerminating={e.IsTerminating}");
        }

        private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            WriteCrashLog("TaskSchedulerUnobservedTaskException", e.Exception);
            e.SetObserved();
        }

        private static void WriteCrashLog(string source, Exception? exception, string? details = null)
        {
            try
            {
                var logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log");
                Directory.CreateDirectory(logDirectory);

                var crashLogPath = Path.Combine(logDirectory, $"database-hub-crash-{DateTime.Now:yyyyMMdd}.txt");
                var lines = new List<string>
                {
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] SOURCE={source}",
                    $"MESSAGE={exception?.Message ?? "(no exception message)"}",
                    $"STACK={exception?.StackTrace ?? "(no stack trace)"}"
                };

                if (!string.IsNullOrWhiteSpace(details))
                {
                    lines.Add($"DETAILS={details}");
                }

                lines.Add(string.Empty);

                lock (CrashLogLock)
                {
                    File.AppendAllLines(crashLogPath, lines);
                }
            }
            catch
            {
                // Avoid recursive crash behavior if logging fails.
            }
        }
    }
}
