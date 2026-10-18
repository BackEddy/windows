using System.Globalization;

namespace Backeddy.Core;

/// <summary>Números, datas e erros do jeito que a pessoa lê.</summary>
public static class Format
{
    static CultureInfo Culture => Strings.Current switch
    {
        Strings.Lang.Pt => new CultureInfo("pt-BR"),
        Strings.Lang.Es => new CultureInfo("es-ES"),
        _ => CultureInfo.CurrentCulture.TwoLetterISOLanguageName == "en" ? CultureInfo.CurrentCulture : new CultureInfo("en-US"),
    };

    public static string Size(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double v = bytes;
        int u = 0;
        while (v >= 1024 && u < units.Length - 1) { v /= 1024; u++; }
        return u == 0 ? $"{bytes} B" : v.ToString(v < 10 ? "0.0" : "0", Culture) + " " + units[u];
    }

    public static string Count(long n) => n.ToString("N0", Culture);

    /// <summary>"hoje às 10:00", "ontem às 22:15", "12/10/2026 às 09:00".</summary>
    public static string When(DateTimeOffset t, DateTimeOffset now)
    {
        var lt = t.ToLocalTime();
        var ln = now.ToLocalTime();
        var time = lt.ToString("HH:mm", Culture);
        if (lt.Date == ln.Date) return Strings.F("todayAt", time);
        if (lt.Date == ln.Date.AddDays(-1)) return Strings.F("yesterdayAt", time);
        if (lt.Date == ln.Date.AddDays(1)) return Strings.F("tomorrowAt", time);
        return Strings.F("dateAt", lt.ToString("d", Culture), time);
    }

    /// <summary>Data longa para a linha do tempo: "sábado, 17 de outubro de 2026".</summary>
    public static string Day(DateTimeOffset t) => t.ToLocalTime().ToString("D", Culture);

    public static string Time(DateTimeOffset t) => t.ToLocalTime().ToString("HH:mm", Culture);

    /// <summary>Código de erro do serviço → frase.</summary>
    public static string Error(string? code)
    {
        if (string.IsNullOrEmpty(code)) return Strings.T("err.unknown");
        var key = "err." + code;
        return Strings.Keys.Contains(key) ? Strings.T(key) : Strings.T("err.unknown");
    }
}
