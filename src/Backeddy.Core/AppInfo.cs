using System.Reflection;

namespace Backeddy.Core;

/// <summary>Nomes e caminhos fixos do Backeddy for Windows.</summary>
public static class AppInfo
{
    /// <summary>Nome do serviço do Windows (sc.exe, Serviços).</summary>
    public const string ServiceName = "Backeddy";

    /// <summary>Canal local entre o app (usuário) e o serviço (sistema).</summary>
    public const string PipeName = "Backeddy.Service.v1";

    /// <summary>Evento usado para a segunda abertura do app trazer a janela da primeira.</summary>
    public const string ShowEventName = "Local\\Backeddy.ShowWindow";

    public const string RepoUrl = "https://github.com/BackEddy/windows";

    public static string Version =>
        Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion?.Split('+')[0]
        ?? "dev";

    /// <summary>C:\ProgramData\Backeddy — configuração e registros do serviço.</summary>
    public static string DataDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Backeddy");

    public static string LogDir => Path.Combine(DataDir, "logs");
}
