using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;

namespace Backeddy.App;

public sealed record FoundServer(string Name, string Host, string? Ip);

/// <summary>Acha os Backeddy Servers da rede (Bonjour/mDNS, serviço _backeddy._tcp), sem programas extras.</summary>
static class Discovery
{
    const string Aqs = "System.Devices.AepService.ProtocolId:={4526e8c1-8aac-4153-9b16-55e86ada0e54} AND " +
                       "System.Devices.Dnssd.Domain:=\"local\" AND System.Devices.Dnssd.ServiceName:=\"_backeddy._tcp\"";

    static readonly string[] Props =
    {
        "System.Devices.Dnssd.HostName", "System.Devices.Dnssd.InstanceName", "System.Devices.IpAddress",
    };

    /// <summary>Duas buscas juntas: mDNS direto (Backeddy.Core.Mdns, o mais confiável) e a do Windows.</summary>
    public static async Task<List<FoundServer>> FindAsync()
    {
        var direct = DirectAsync();
        var windows = WindowsAsync();
        var list = new List<FoundServer>();
        foreach (var s in (await direct).Concat(await windows))
            if (!list.Any(x => string.Equals(x.Host, s.Host, StringComparison.OrdinalIgnoreCase)
                               || (s.Ip is not null && x.Ip == s.Ip)))
                list.Add(s);
        return list.OrderBy(s => s.Name).ToList();
    }

    static async Task<List<FoundServer>> DirectAsync()
    {
        try
        {
            var found = await Backeddy.Core.Mdns.BrowseAsync("_backeddy._tcp.local", TimeSpan.FromSeconds(3));
            return found.Select(f => new FoundServer(f.Name, f.Host.TrimEnd('.'), f.Ip)).ToList();
        }
        catch (Exception) { return new(); }
    }

    static async Task<List<FoundServer>> WindowsAsync()
    {
        var list = new List<FoundServer>();
        try
        {
            var found = await DeviceInformation.FindAllAsync(Aqs, Props, DeviceInformationKind.AssociationEndpointService).AsTask()
                .WaitAsync(TimeSpan.FromSeconds(6));
            foreach (var d in found)
            {
                var host = Prop(d, "System.Devices.Dnssd.HostName") as string;
                if (string.IsNullOrWhiteSpace(host)) continue;
                host = host.TrimEnd('.');
                var name = Prop(d, "System.Devices.Dnssd.InstanceName") as string;
                if (string.IsNullOrWhiteSpace(name)) name = d.Name;
                string? ip = (Prop(d, "System.Devices.IpAddress") as string[])?.FirstOrDefault(a => a.Contains('.'));
                if (list.Any(s => string.Equals(s.Host, host, StringComparison.OrdinalIgnoreCase))) continue;
                list.Add(new FoundServer(string.IsNullOrWhiteSpace(name) ? host : name, host, ip));
            }
        }
        catch (Exception) { }   // sem mDNS na rede: a pessoa digita o endereço
        return list;
    }

    static object? Prop(DeviceInformation d, string key) => d.Properties.TryGetValue(key, out var v) ? v : null;
}
