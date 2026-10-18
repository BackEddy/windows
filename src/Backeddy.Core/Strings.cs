using System.Globalization;

namespace Backeddy.Core;

/// <summary>
/// Textos em português, inglês e espanhol. O idioma segue o do Windows (português, espanhol; os outros, inglês).
/// </summary>
public static class Strings
{
    public enum Lang { Pt, En, Es }

    public static Lang Current { get; set; } = FromCulture(CultureInfo.CurrentUICulture);

    public static Lang FromCulture(CultureInfo c) => c.TwoLetterISOLanguageName switch
    {
        "pt" => Lang.Pt,
        "es" => Lang.Es,
        _ => Lang.En,
    };

    /// <summary>Texto no idioma atual; a chave volta se não existir (fica fácil achar o que falta).</summary>
    public static string T(string key)
    {
        if (!Table.TryGetValue(key, out var v)) return key;
        return Current switch { Lang.Pt => v.Pt, Lang.Es => v.Es, _ => v.En };
    }

    public static string F(string key, params object[] args) => string.Format(CultureInfo.CurrentCulture, T(key), args);

    public static IReadOnlyCollection<string> Keys => Table.Keys;

    internal static readonly Dictionary<string, (string Pt, string En, string Es)> Table = new()
    {
        ["appName"] = ("Backeddy", "Backeddy", "Backeddy"),
        ["tagline"] = ("Backups do seu PC, sem dor de cabeça", "Your PC's backups, without the guesswork", "Los backups de tu PC, sin complicaciones"),
        ["trayTooltip"] = ("Backeddy", "Backeddy", "Backeddy"),
        ["trayOpen"] = ("Abrir o Backeddy", "Open Backeddy", "Abrir Backeddy"),
        ["trayQuit"] = ("Fechar o ícone (os backups continuam)", "Close the icon (backups keep running)", "Cerrar el icono (los backups siguen)"),
        ["serviceOk"] = ("Serviço de backup: funcionando (versão {0})", "Backup service: running (version {0})", "Servicio de backup: funcionando (versión {0})"),
        ["serviceDown"] = ("Serviço de backup: parado. Reinstale o Backeddy ou reinicie o PC.", "Backup service: stopped. Reinstall Backeddy or restart the PC.", "Servicio de backup: detenido. Reinstala Backeddy o reinicia el PC."),
        ["notConfigured"] = ("Ainda não há destino de backup. (Esta é uma versão de teste: o backup chega na próxima.)", "No backup destination yet. (This is a test build: backups arrive in the next one.)", "Todavía no hay destino de backup. (Esta es una versión de prueba: el backup llega en la próxima.)"),
        ["refresh"] = ("Atualizar", "Refresh", "Actualizar"),
        ["preview"] = ("Versão de teste: ainda não faz backup.", "Test build: it doesn't back up yet.", "Versión de prueba: todavía no hace backup."),
    };
}
