using System.IO;
using System.Text;

namespace ScreenSoundSwitch.App.Data;

public static class DebugLogStore
{
    private static readonly List<string> _logs = new();
    private static readonly object _syncRoot = new();
    private const long MaxLogFileBytes = 1024 * 1024;
    private static readonly string _sessionName = DateTime.Now.ToString("yyyyMMdd-HHmmss");
    private static readonly string _logDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ScreenSoundSwitch", "Logs");
    private static int _logFileIndex = 1;
    private static string _currentLogFilePath;

    public static event Action<string>? LogAdded;
    public static event Action? LogsCleared;

    public static IReadOnlyList<string> Logs => _logs;
    public static string LogDirectory => _logDirectory;

    static DebugLogStore()
    {
        Directory.CreateDirectory(_logDirectory);
        _currentLogFilePath = MakeLogFilePath();
    }

    public static void Initialize()
    {
        Add($"Log session started. Directory={_logDirectory}");
    }

    public static void Add(string message)
    {
        var log = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
        lock (_syncRoot)
        {
            _logs.Add(log);
            WriteToFile(log);
        }
        LogAdded?.Invoke(log);
    }

    public static void Clear()
    {
        lock (_syncRoot)
            _logs.Clear();
        LogsCleared?.Invoke();
    }

    private static void WriteToFile(string log)
    {
        try
        {
            var line = log + Environment.NewLine;
            var bytes = Encoding.UTF8.GetByteCount(line);
            RotateIfNeeded(bytes);
            File.AppendAllText(_currentLogFilePath, line, Encoding.UTF8);
        }
        catch { }
    }

    private static void RotateIfNeeded(long incoming)
    {
        var current = File.Exists(_currentLogFilePath) ? new FileInfo(_currentLogFilePath).Length : 0;
        if (current + incoming > MaxLogFileBytes)
        {
            _logFileIndex++;
            _currentLogFilePath = MakeLogFilePath();
        }
    }

    private static string MakeLogFilePath() =>
        Path.Combine(_logDirectory, $"ScreenSoundSwitch-{_sessionName}-{_logFileIndex:000}.log");
}
