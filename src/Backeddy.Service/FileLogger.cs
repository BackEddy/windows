using Backeddy.Core;

namespace Backeddy.Service;

/// <summary>
/// Registro simples em C:\ProgramData\Backeddy\logs\backeddy.log (fácil de mandar para quem está ajudando).
/// Passou de 5 MB, vira backeddy.old.log e começa outro.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    static readonly object gate = new();
    static string Path => System.IO.Path.Combine(AppInfo.LogDir, "backeddy.log");

    public ILogger CreateLogger(string category) => new FileLogger(category);
    public void Dispose() { }

    sealed class FileLogger(string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => level >= LogLevel.Information;

        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? e, Func<TState, Exception?, string> format)
        {
            if (!IsEnabled(level)) return;
            var cat = category[(category.LastIndexOf('.') + 1)..];
            var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss} {level.ToString()[..4].ToUpperInvariant()} {cat}: {format(state, e)}";
            if (e is not null) line += Environment.NewLine + e;
            lock (gate)
            {
                try
                {
                    Directory.CreateDirectory(AppInfo.LogDir);
                    var fi = new FileInfo(Path);
                    if (fi.Exists && fi.Length > 5 << 20)
                        File.Move(Path, System.IO.Path.Combine(AppInfo.LogDir, "backeddy.old.log"), true);
                    File.AppendAllText(Path, line + Environment.NewLine);
                }
                catch (Exception) { }
            }
        }
    }
}
