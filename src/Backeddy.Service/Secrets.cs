using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

namespace Backeddy.Service;

/// <summary>
/// Senhas guardadas com o DPAPI do próprio serviço (conta Sistema): só o serviço consegue ler o arquivo,
/// e a pasta fica fechada para usuários comuns.
/// </summary>
public sealed class Secrets
{
    public string? RepoPassword { get; set; }      // chave de recuperação (HD) ou senha do PC no servidor
    public string? ServerPassword { get; set; }    // senha do servidor (alertas)

    static string FilePath => Path.Combine(PrivateDir, "secrets.bin");

    public static string PrivateDir
    {
        get
        {
            var dir = Path.Combine(Backeddy.Core.AppInfo.DataDir, "private");
            if (!Directory.Exists(dir))
            {
                var info = Directory.CreateDirectory(dir);
                var sec = new DirectorySecurity();
                sec.SetAccessRuleProtection(true, false);
                foreach (var sid in new[] { WellKnownSidType.LocalSystemSid, WellKnownSidType.BuiltinAdministratorsSid })
                    sec.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(sid, null), FileSystemRights.FullControl,
                        InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
                info.SetAccessControl(sec);
            }
            return dir;
        }
    }

    public static Secrets Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new Secrets();
            var plain = Unprotect(File.ReadAllBytes(FilePath));
            return JsonSerializer.Deserialize<Secrets>(plain) ?? new Secrets();
        }
        catch (Exception) { return new Secrets(); }
    }

    public void Save()
    {
        var bytes = Protect(JsonSerializer.SerializeToUtf8Bytes(this));
        var tmp = FilePath + ".tmp";
        File.WriteAllBytes(tmp, bytes);
        File.Move(tmp, FilePath, true);
    }

    public static void Delete()
    {
        try { File.Delete(FilePath); } catch (Exception) { }
    }

    // ---- DPAPI (crypt32) ----

    [StructLayout(LayoutKind.Sequential)]
    struct Blob { public int Size; public IntPtr Data; }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool CryptProtectData(ref Blob input, string? desc, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, ref Blob output);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool CryptUnprotectData(ref Blob input, IntPtr desc, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, ref Blob output);

    [DllImport("kernel32.dll")]
    static extern IntPtr LocalFree(IntPtr p);

    const int UiForbidden = 0x1;

    static byte[] Protect(byte[] data) => Run(data, true);
    static byte[] Unprotect(byte[] data) => Run(data, false);

    static byte[] Run(byte[] data, bool protect)
    {
        var h = GCHandle.Alloc(data, GCHandleType.Pinned);
        try
        {
            var input = new Blob { Size = data.Length, Data = h.AddrOfPinnedObject() };
            var output = new Blob();
            bool ok = protect
                ? CryptProtectData(ref input, "Backeddy", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, UiForbidden, ref output)
                : CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, UiForbidden, ref output);
            if (!ok) throw new InvalidOperationException("DPAPI " + Marshal.GetLastWin32Error());
            var result = new byte[output.Size];
            Marshal.Copy(output.Data, result, 0, output.Size);
            LocalFree(output.Data);
            return result;
        }
        finally { h.Free(); }
    }
}
