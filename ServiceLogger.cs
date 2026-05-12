using System;
using System.IO;
using System.Reflection;

namespace SamanDeviceAdapter
{
    /// <summary>
    /// Simple logging utility for the Windows Service.
    /// Logs to a file in the application directory.
    /// </summary>
    public static class ServiceLogger
    {
        private static readonly string _logDirectory;
        private static readonly object _lockObject = new object();

        static ServiceLogger()
        {
            _logDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        }

        /// <summary>
        /// Gets the current log file path.
        /// </summary>
        public static string LogFilePath
        {
            get
            {
                return Path.Combine(_logDirectory, $"ServiceLog_{DateTime.Now:yyyy-MM-dd}.txt");
            }
        }

        /// <summary>
        /// Logs an information message.
        /// </summary>
        public static void LogInfo(string source, string message)
        {
            Log("INFO", source, message);
        }

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        public static void LogWarning(string source, string message)
        {
            Log("WARN", source, message);
        }

        /// <summary>
        /// Logs an error message.
        /// </summary>
        public static void LogError(string source, string message)
        {
            Log("ERROR", source, message);
        }

        /// <summary>
        /// Logs an exception with its full details.
        /// </summary>
        public static void LogException(string source, Exception ex)
        {
            string message = $"Exception: {ex.GetType().Name} - {ex.Message}\r\nStack Trace: {ex.StackTrace}";
            Log("ERROR", source, message);
        }

        private static void Log(string level, string source, string message)
        {
            lock (_lockObject)
            {
                try
                {
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    string logEntry = $"[{timestamp}] [{level}] [{source}] {message}";

                    // Write to file
                    File.AppendAllText(LogFilePath, logEntry + Environment.NewLine);
                }
                catch
                {
                    // Silently fail if logging fails to avoid crashes
                }
            }
        }
    }
}
