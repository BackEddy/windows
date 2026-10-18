using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace Backeddy.Core;

public sealed record MdnsService(string Name, string Host, int Port, string? Ip);

/// <summary>
/// Procura serviços na rede local por mDNS (Bonjour), direto por UDP, sem depender do Windows.
/// Pergunta de uma porta qualquer (não a 5353): os servidores respondem direto para nós ("legacy unicast", RFC 6762),
/// então o firewall do Windows deixa a resposta entrar.
/// </summary>
public static class Mdns
{
    static readonly IPEndPoint Group = new(IPAddress.Parse("224.0.0.251"), 5353);
    const ushort PTR = 12, SRV = 33, A = 1;

    public static async Task<List<MdnsService>> BrowseAsync(string service, TimeSpan wait, CancellationToken ct = default)
    {
        var records = new Records();
        var sockets = new List<UdpClient>();
        try
        {
            foreach (var local in LocalAddresses())
            {
                try
                {
                    var u = new UdpClient(new IPEndPoint(local, 0));
                    u.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, local.GetAddressBytes());
                    u.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 255);
                    sockets.Add(u);
                }
                catch (SocketException) { }
            }
            if (sockets.Count == 0) return new();
            var query = BuildQuery(service, PTR);
            var until = DateTime.UtcNow + wait;
            var receivers = sockets.Select(s => Receive(s, records, until, ct)).ToList();
            for (int i = 0; i < 3 && DateTime.UtcNow < until; i++)
            {
                foreach (var s in sockets) { try { await s.SendAsync(query, query.Length, Group); } catch (SocketException) { } }
                // completa o que faltou (servidor, endereço) perguntando de novo
                foreach (var q in records.FollowUps(service))
                    foreach (var s in sockets) { try { await s.SendAsync(q, q.Length, Group); } catch (SocketException) { } }
                try { await Task.Delay(TimeSpan.FromMilliseconds(Math.Min(800, wait.TotalMilliseconds / 3)), ct); } catch (OperationCanceledException) { break; }
            }
            await Task.WhenAll(receivers);
            return records.Services(service);
        }
        finally { foreach (var s in sockets) s.Dispose(); }
    }

    static async Task Receive(UdpClient u, Records records, DateTime until, CancellationToken ct)
    {
        while (DateTime.UtcNow < until && !ct.IsCancellationRequested)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(until - DateTime.UtcNow);
            try
            {
                var r = await u.ReceiveAsync(cts.Token);
                lock (records) Parse(r.Buffer, r.RemoteEndPoint.Address, records);
            }
            catch (Exception e) when (e is OperationCanceledException or SocketException or ObjectDisposedException) { break; }
            catch (Exception) { }   // pacote estranho de outro aparelho: ignora
        }
    }

    static IEnumerable<IPAddress> LocalAddresses()
    {
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            if (!ni.SupportsMulticast) continue;
            foreach (var a in ni.GetIPProperties().UnicastAddresses)
                if (a.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a.Address)
                    && !a.Address.ToString().StartsWith("169.254.")) yield return a.Address;
        }
    }

    // ---------------------------------------------------------------- pacotes

    public static byte[] BuildQuery(string name, ushort type)
    {
        var b = new List<byte> { 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0 };   // id 0, uma pergunta
        foreach (var label in name.TrimEnd('.').Split('.'))
        {
            var bytes = Encoding.UTF8.GetBytes(label);
            b.Add((byte)bytes.Length);
            b.AddRange(bytes);
        }
        b.Add(0);
        b.Add((byte)(type >> 8)); b.Add((byte)type);
        b.Add(0); b.Add(1);   // classe IN
        return b.ToArray();
    }

    public sealed class Records
    {
        public readonly HashSet<string> Instances = new(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, (string Target, int Port)> Srv = new(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, string> Addr = new(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, string> From = new(StringComparer.OrdinalIgnoreCase);   // quem respondeu

        public IEnumerable<byte[]> FollowUps(string service)
        {
            lock (this)
            {
                var list = new List<byte[]>();
                foreach (var i in Instances)
                {
                    if (!Srv.TryGetValue(i, out var s)) list.Add(BuildQuery(i, SRV));
                    else if (!Addr.ContainsKey(s.Target)) list.Add(BuildQuery(s.Target, A));
                }
                return list;
            }
        }

        public List<MdnsService> Services(string service)
        {
            lock (this)
            {
                var suffix = "." + service.TrimEnd('.');
                var list = new List<MdnsService>();
                foreach (var i in Instances)
                {
                    var name = i.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) ? i[..^suffix.Length] : i;
                    Srv.TryGetValue(i, out var s);
                    string? ip = s.Target is not null && Addr.TryGetValue(s.Target, out var a) ? a : From.GetValueOrDefault(i);
                    var host = s.Target ?? ip;
                    if (host is null) continue;
                    list.Add(new MdnsService(name, host, s.Port, ip));
                }
                return list.OrderBy(s => s.Name).ToList();
            }
        }
    }

    public static void Parse(byte[] p, IPAddress from, Records r)
    {
        if (p.Length < 12) return;
        int qd = p[4] << 8 | p[5], an = p[6] << 8 | p[7], ns = p[8] << 8 | p[9], ar = p[10] << 8 | p[11];
        int pos = 12;
        for (int i = 0; i < qd; i++) { ReadName(p, ref pos); pos += 4; }
        for (int i = 0; i < an + ns + ar && pos + 10 <= p.Length; i++)
        {
            var name = ReadName(p, ref pos);
            int type = p[pos] << 8 | p[pos + 1];
            int len = p[pos + 8] << 8 | p[pos + 9];
            pos += 10;
            int data = pos;
            if (data + len > p.Length) return;
            switch (type)
            {
                case PTR:
                    var inst = ReadName(p, ref data);
                    r.Instances.Add(inst);
                    r.From[inst] = from.ToString();
                    break;
                case SRV:
                    int port = p[data + 4] << 8 | p[data + 5];
                    int t = data + 6;
                    r.Srv[name] = (ReadName(p, ref t), port);
                    break;
                case A when len == 4:
                    r.Addr[name] = new IPAddress(p.AsSpan(data, 4)).ToString();
                    break;
            }
            pos += len;
        }
    }

    // nome DNS com compressão (ponteiros 0xC0)
    static string ReadName(byte[] p, ref int pos)
    {
        var labels = new List<string>();
        int jumps = 0, cur = pos;
        bool jumped = false;
        while (cur < p.Length)
        {
            int len = p[cur];
            if (len == 0) { cur++; break; }
            if ((len & 0xC0) == 0xC0)
            {
                if (cur + 1 >= p.Length || ++jumps > 20) break;
                int target = (len & 0x3F) << 8 | p[cur + 1];
                if (!jumped) pos = cur + 2;
                jumped = true;
                cur = target;
                continue;
            }
            if (cur + 1 + len > p.Length) break;
            labels.Add(Encoding.UTF8.GetString(p, cur + 1, len));
            cur += 1 + len;
        }
        if (!jumped) pos = cur;
        return string.Join('.', labels);
    }
}
