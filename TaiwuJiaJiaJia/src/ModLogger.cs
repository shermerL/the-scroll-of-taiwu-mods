using System;
using System.IO;
using UnityEngine;

internal static class ModLogger
{
    private const string LogFileName = "TaiwuJiaJiaJia.log";

    public static void Warn(string message)
    {
        Debug.LogWarning("[TaiwuJiaJiaJia] " + message);
        Write("WARN", message, null);
    }

    public static void Warn(string message, Exception ex)
    {
        Debug.LogWarning("[TaiwuJiaJiaJia] " + message + "\n" + ex);
        Write("WARN", message, ex);
    }

    public static void Error(string message, Exception ex)
    {
        Debug.LogError("[TaiwuJiaJiaJia] " + message + "\n" + ex);
        Write("ERROR", message, ex);
    }

    private static void Write(string level, string message, Exception ex)
    {
        try
        {
            string path = GetLogPath();
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
            if (ex != null)
            {
                line += Environment.NewLine + ex;
            }

            File.AppendAllText(path, line + Environment.NewLine);
        }
        catch
        {
            // Logging must never break gameplay or the mod UI.
        }
    }

    private static string GetLogPath()
    {
        try
        {
            string assemblyPath = typeof(ModLogger).Assembly.Location;
            if (string.IsNullOrEmpty(assemblyPath))
            {
                return null;
            }

            DirectoryInfo pluginDir = Directory.GetParent(assemblyPath);
            DirectoryInfo modDir = pluginDir?.Parent;
            return modDir == null ? null : Path.Combine(modDir.FullName, LogFileName);
        }
        catch
        {
            return null;
        }
    }
}
