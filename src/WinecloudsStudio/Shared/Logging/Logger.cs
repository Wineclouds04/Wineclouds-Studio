using System.Collections.Concurrent;

namespace WinecloudsStudio.Shared.Logging;

/// <summary>
/// Lightweight file logger with automatic daily rotation.
/// Thread-safe — all public methods may be called from any thread.
/// Error-level messages are flushed to disk immediately.
/// </summary>
public static class Logger
{
    private static readonly ConcurrentQueue<LogEntry> s_queue = new();
    private static readonly object s_writeLock = new();
    private static StreamWriter? s_writer;
    private static string s_currentDate = string.Empty;
    private static string s_logDir = string.Empty;
    private static volatile bool s_initialized;
    private static readonly ManualResetEventSlim s_flushSignal = new(false);
    private static Thread? s_flushThread;
    private static CancellationTokenSource? s_cts;

    public enum Level { Debug, Info, Warn, Error }

    private readonly struct LogEntry
    {
        public readonly DateTime Time;
        public readonly Level Lvl;
        public readonly string Source;
        public readonly string Message;

        public LogEntry(Level lvl, string source, string message)
        {
            Time = DateTime.Now;
            Lvl = lvl;
            Source = source;
            Message = message;
        }
    }

    /// <summary>
    /// Initialises the logger. Safe to call multiple times.
    /// Call once at app startup. Subscribes to unhandled-exception handlers.
    /// </summary>
    public static void Init()
    {
        if (s_initialized) return;
        s_initialized = true;

        try
        {
            s_logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WinecloudsStudio", "logs");
            Directory.CreateDirectory(s_logDir);

            // Clean up logs older than 7 days
            CleanOldLogs();

            OpenWriter();

            s_cts = new CancellationTokenSource();
            s_flushThread = new Thread(FlushLoop)
            {
                Name = "WinecloudsLogger",
                IsBackground = true
            };
            s_flushThread.Start();

            // Capture unhandled crashes
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                WriteErrorDirect($"UnhandledException: {e.ExceptionObject}");
            };

            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                WriteErrorDirect($"UnobservedTaskException: {e.Exception}");
                e.SetObserved();
            };

            // First log entry — confirms logger is working
            Enqueue(Level.Info, "Logger", "Logger initialised");
        }
        catch
        {
            // Logger must never crash the app. If init fails, we
            // leave s_initialized = true so we don't keep retrying.
        }
    }

    /// <summary>Direct write bypassing queue — used by crash handlers.</summary>
    private static void WriteErrorDirect(string message)
    {
        try
        {
            string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [ERROR] [Crash] {message}";
            lock (s_writeLock)
            {
                OpenWriter();
                s_writer?.WriteLine(line);
                s_writer?.Flush();
            }
        }
        catch
        {
            // Last resort — nothing we can do
        }
    }

    /// <summary>
    /// Shuts down the logger, flushing any buffered messages.
    /// </summary>
    public static void Shutdown()
    {
        Info("Logger", "Logger shutting down");
        s_cts?.Cancel();
        s_flushSignal.Set();
        s_flushThread?.Join(TimeSpan.FromSeconds(2));
        Flush();
        lock (s_writeLock)
        {
            s_writer?.Dispose();
            s_writer = null;
        }
    }

    // ---- Public logging methods ----

    public static void Debug(string source, string message) => Enqueue(Level.Debug, source, message);
    public static void Info(string source, string message) => Enqueue(Level.Info, source, message);
    public static void Warn(string source, string message) => Enqueue(Level.Warn, source, message);
    public static void Error(string source, string message) => Enqueue(Level.Error, source, message);

    // ---- Private ----

    private static void Enqueue(Level level, string source, string message)
    {
        if (!s_initialized) return;
        s_queue.Enqueue(new LogEntry(level, source, message));
        s_flushSignal.Set();

        // Error messages bypass the buffer — flush immediately
        if (level == Level.Error)
            Flush();
    }

    private static void FlushLoop()
    {
        var token = s_cts?.Token ?? CancellationToken.None;
        while (!token.IsCancellationRequested)
        {
            s_flushSignal.Wait(TimeSpan.FromSeconds(2));
            s_flushSignal.Reset();
            Flush();
        }
    }

    private static void Flush()
    {
        // Collect all pending entries
        var batch = new List<LogEntry>();
        while (s_queue.TryDequeue(out var entry))
            batch.Add(entry);

        if (batch.Count == 0) return;

        lock (s_writeLock)
        {
            try
            {
                string today = DateTime.Now.ToString("yyyyMMdd");
                if (today != s_currentDate)
                    OpenWriter();

                if (s_writer == null) return;

                foreach (var entry in batch)
                {
                    string line = FormatEntry(entry);
                    s_writer.WriteLine(line);
                }
                s_writer.Flush();
            }
            catch
            {
                // Logging must never throw
            }
        }
    }

    private static string FormatEntry(LogEntry entry)
    {
        string level = entry.Lvl switch
        {
            Level.Debug => "DEBUG",
            Level.Info => "INFO",
            Level.Warn => "WARN",
            Level.Error => "ERROR",
            _ => "????"
        };
        return $"[{entry.Time:yyyy-MM-dd HH:mm:ss.fff}] [{level}] [{entry.Source}] {entry.Message}";
    }

    private static void OpenWriter()
    {
        s_currentDate = DateTime.Now.ToString("yyyyMMdd");
        string filePath = Path.Combine(s_logDir, $"wineclouds_{s_currentDate}.log");

        s_writer?.Dispose();
        s_writer = new StreamWriter(filePath, append: true, System.Text.Encoding.UTF8)
        {
            AutoFlush = false
        };
    }

    private static void CleanOldLogs()
    {
        try
        {
            var cutoff = DateTime.Now.AddDays(-7);
            foreach (var file in Directory.GetFiles(s_logDir, "wineclouds_*.log"))
            {
                if (File.GetLastWriteTime(file) < cutoff)
                    File.Delete(file);
            }
        }
        catch
        {
            // Non-critical
        }
    }
}
