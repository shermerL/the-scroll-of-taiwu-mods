using System;
using System.IO;
using UnityEngine;

internal static class ManagerLogger
{
    private const string LogFileName = "TaiwuGoodPartnerManager.log";
    private const string Prefix = "[TaiwuGoodPartnerManager] ";

    public static void Warning(string message)
    {
        Debug.LogWarning(Prefix + message);
        Write("WARN", message, null);
    }

    public static void Warning(string message, Exception ex)
    {
        Debug.LogWarning(Prefix + message + "\n" + ex);
        Write("WARN", message, ex);
    }

    public static void Error(string message, Exception ex)
    {
        Debug.LogError(Prefix + message + "\n" + ex);
        Write("ERROR", message, ex);
    }

    private static void Write(string level, string message, Exception ex)
    {
        try
        {
            var logPath = GetLogPath();
            if (string.IsNullOrEmpty(logPath))
            {
                return;
            }

            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
            if (ex != null)
            {
                line += Environment.NewLine + ex;
            }

            File.AppendAllText(logPath, line + Environment.NewLine);
        }
        catch
        {
            // Logging must never break gameplay.
        }
    }

    private static string GetLogPath()
    {
        try
        {
            var assembly = typeof(ManagerLogger).Assembly;
            var path = assembly.Location;
            if (string.IsNullOrEmpty(path) && Uri.TryCreate(assembly.CodeBase, UriKind.Absolute, out var uri) && uri.IsFile)
            {
                path = uri.LocalPath;
            }

            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            var pluginDir = Directory.GetParent(path);
            var modDir = pluginDir?.Parent;
            return modDir == null ? null : Path.Combine(modDir.FullName, LogFileName);
        }
        catch
        {
            return null;
        }
    }
}
