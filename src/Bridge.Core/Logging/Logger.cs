namespace Bridge.Core.Logging;

/// <summary>
/// Niveau de log pour le système de débogage.
/// </summary>
public enum LogLevel
{
    Trace = 0,
    Debug = 1,
    Information = 2,
    Warning = 3,
    Error = 4,
    Critical = 5
}

/// <summary>
/// Interface pour le système de logging.
/// </summary>
public interface ILogger
{
    void Log(LogLevel level, string message, Exception? exception = null);
    void Trace(string message);
    void Debug(string message);
    void Information(string message);
    void Warning(string message);
    void Error(string message, Exception? exception = null);
    void Critical(string message, Exception? exception = null);
}

/// <summary>
/// Implémentation de logger avec fichier et console.
/// </summary>
public sealed class ConsoleFileLogger : ILogger
{
    private readonly string _logDirectory;
    private readonly LogLevel _minimumLevel;
    private readonly object _lockObject = new();

    public ConsoleFileLogger(string logDirectory = "logs", LogLevel minimumLevel = LogLevel.Debug)
    {
        _logDirectory = logDirectory;
        _minimumLevel = minimumLevel;

        // Créer le dossier s'il n'existe pas
        if (!Directory.Exists(_logDirectory))
        {
            Directory.CreateDirectory(_logDirectory);
        }
    }

    public void Log(LogLevel level, string message, Exception? exception = null)
    {
        if (level < _minimumLevel)
            return;

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var logMessage = $"[{timestamp}] [{level,-11}] {message}";

        if (exception != null)
        {
            logMessage += $"\n{exception}";
        }

        lock (_lockObject)
        {
            // Afficher sur la console
            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = GetColorForLevel(level);
            Console.WriteLine(logMessage);
            Console.ForegroundColor = originalColor;

            // Écrire dans le fichier
            var logFile = Path.Combine(_logDirectory, $"bridge-{DateTime.Now:yyyy-MM-dd}.log");
            try
            {
                File.AppendAllText(logFile, logMessage + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Erreur lors de l'écriture du log: {ex.Message}");
            }
        }
    }

    public void Trace(string message) => Log(LogLevel.Trace, message);
    public void Debug(string message) => Log(LogLevel.Debug, message);
    public void Information(string message) => Log(LogLevel.Information, message);
    public void Warning(string message) => Log(LogLevel.Warning, message);
    public void Error(string message, Exception? exception = null) => Log(LogLevel.Error, message, exception);
    public void Critical(string message, Exception? exception = null) => Log(LogLevel.Critical, message, exception);

    private static ConsoleColor GetColorForLevel(LogLevel level) => level switch
    {
        LogLevel.Trace => ConsoleColor.Gray,
        LogLevel.Debug => ConsoleColor.Cyan,
        LogLevel.Information => ConsoleColor.Green,
        LogLevel.Warning => ConsoleColor.Yellow,
        LogLevel.Error => ConsoleColor.Red,
        LogLevel.Critical => ConsoleColor.Magenta,
        _ => ConsoleColor.White
    };
}

/// <summary>
/// Fournisseur global de logger singleton.
/// </summary>
public static class LoggerProvider
{
    private static ILogger? _instance;
    private static readonly object LockObject = new();

    public static ILogger GetLogger()
    {
        if (_instance != null)
            return _instance;

        lock (LockObject)
        {
            _instance ??= new ConsoleFileLogger("logs", LogLevel.Debug);
            return _instance;
        }
    }

    public static void SetLogger(ILogger logger)
    {
        lock (LockObject)
        {
            _instance = logger ?? throw new ArgumentNullException(nameof(logger));
        }
    }

    public static void Reset()
    {
        lock (LockObject)
        {
            _instance = null;
        }
    }
}
