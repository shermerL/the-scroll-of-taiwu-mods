using System;
using System.IO;

namespace TaiwuGoodPartnerAya;

internal static class ModLogger
{
    private const string Prefix = "[太吾好伙伴阿雅] ";
    private static string _logPath;
    private static bool _diagnosticEnabled;

    public static void Initialize(string pluginDirectory, bool diagnosticEnabled)
    {
        _diagnosticEnabled = diagnosticEnabled;
        try
        {
            var root = string.IsNullOrEmpty(pluginDirectory)
                ? AppDomain.CurrentDomain.BaseDirectory
                : Path.GetFullPath(Path.Combine(pluginDirectory, ".."));
            var logDirectory = Path.Combine(root, "Logs");
            Directory.CreateDirectory(logDirectory);
            _logPath = Path.Combine(logDirectory, "TaiwuGoodPartnerAya.log");
            File.AppendAllText(_logPath, Environment.NewLine + Prefix + "Logger initialized at " + DateTime.Now + Environment.NewLine);
        }
        catch
        {
            TryInitializeFallbackLogPath(pluginDirectory);
        }
    }

    private static void TryInitializeFallbackLogPath(string pluginDirectory)
    {
        try
        {
            var root = string.IsNullOrEmpty(pluginDirectory)
                ? AppDomain.CurrentDomain.BaseDirectory
                : Path.GetFullPath(Path.Combine(pluginDirectory, ".."));
            _logPath = Path.Combine(root, "TaiwuGoodPartnerAya.log");
            File.AppendAllText(_logPath, Environment.NewLine + Prefix + "Logger initialized at " + DateTime.Now + Environment.NewLine);
        }
        catch
        {
            _logPath = null;
        }
    }

    public static void Diagnostic(string message)
    {
        if (!_diagnosticEnabled)
        {
            return;
        }

        Write("Diagnostic", message, null);
    }

    public static void Warning(string message)
    {
        Console.Error.WriteLine(Prefix + "Warning: " + message);
        Write("Warning", message, null);
    }

    public static void Error(string message, Exception ex = null)
    {
        Console.Error.WriteLine(Prefix + "Error: " + message + (ex == null ? string.Empty : "\n" + ex));
        Write("Error", message, ex);
    }

    private static void Write(string level, string message, Exception ex)
    {
        if (string.IsNullOrEmpty(_logPath))
        {
            return;
        }

        try
        {
            File.AppendAllText(
                _logPath,
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
                    + " | "
                    + level
                    + " | "
                    + message
                    + (ex == null ? string.Empty : Environment.NewLine + ex)
                    + Environment.NewLine);
        }
        catch
        {
        }
    }
}
