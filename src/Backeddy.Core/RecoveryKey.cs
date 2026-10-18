using System.Security.Cryptography;
using System.Text;

namespace Backeddy.Core;

/// <summary>
/// Chave de recuperação do backup no HD externo (é a senha do repositório). 24 caracteres sem letras que confundem
/// (sem 0/O, 1/I/L), em grupos de 4: K7QM-2XPD-…
/// </summary>
public static class RecoveryKey
{
    const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    const int Length = 24;

    public static string New()
    {
        var sb = new StringBuilder(Length);
        for (int i = 0; i < Length; i++) sb.Append(Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)]);
        return Group(sb.ToString());
    }

    /// <summary>O que a pessoa digitou, do jeito canônico (maiúsculas, sem espaços, grupos de 4). Null se não for uma chave.</summary>
    public static string? Normalize(string? typed)
    {
        if (typed is null) return null;
        var chars = typed.ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray();
        var s = new string(chars);
        if (s.Length != Length || s.Any(c => !Alphabet.Contains(c))) return null;
        return Group(s);
    }

    static string Group(string s) =>
        string.Join("-", Enumerable.Range(0, s.Length / 4).Select(i => s.Substring(i * 4, 4)));
}
