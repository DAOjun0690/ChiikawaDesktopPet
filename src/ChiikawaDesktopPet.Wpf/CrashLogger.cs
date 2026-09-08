// src/ChiikawaDesktopPet.Wpf/CrashLogger.cs
using System;
using System.IO;
using System.Text;

namespace ChiikawaDesktopPet.Wpf;

public static class CrashLogger
{
    private static readonly object LogLock = new();

    public static string GetLogFilePath()
    {
        try
        {
            string appDir = AppContext.BaseDirectory;
            string primaryPath = Path.Combine(appDir, "crash.log");

            // Test if primary path directory is writable
            using (var fs = new FileStream(primaryPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite))
            {
                // Write access confirmed
            }
            return primaryPath;
        }
        catch
        {
            // Fallback to %LOCALAPPDATA%\ChiikawaDesktopPet\crash.log
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appFolder = Path.Combine(localAppData, "ChiikawaDesktopPet");
            if (!Directory.Exists(appFolder))
            {
                Directory.CreateDirectory(appFolder);
            }
            return Path.Combine(appFolder, "crash.log");
        }
    }

    public static void Log(Exception ex, string context = "")
    {
        try
        {
            lock (LogLock)
            {
                string logFile = GetLogFilePath();
                var sb = new StringBuilder();
                sb.AppendLine("==================================================");
                sb.AppendLine($"[Timestamp] {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
                if (!string.IsNullOrEmpty(context))
                {
                    sb.AppendLine($"[Context] {context}");
                }
                sb.AppendLine($"[Exception Type] {ex.GetType().FullName}");
                sb.AppendLine($"[Message] {ex.Message}");
                sb.AppendLine($"[Source] {ex.Source}");
                sb.AppendLine("[Stack Trace]");
                sb.AppendLine(ex.StackTrace);

                var inner = ex.InnerException;
                while (inner != null)
                {
                    sb.AppendLine("--- Inner Exception ---");
                    sb.AppendLine($"[Exception Type] {inner.GetType().FullName}");
                    sb.AppendLine($"[Message] {inner.Message}");
                    sb.AppendLine(inner.StackTrace);
                    inner = inner.InnerException;
                }
                sb.AppendLine();

                File.AppendAllText(logFile, sb.ToString(), Encoding.UTF8);
            }
        }
        catch
        {
            // Never allow crash logger itself to throw secondary exception
        }
    }
}
