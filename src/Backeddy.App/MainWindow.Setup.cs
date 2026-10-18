using System;
using System.IO;
using System.Linq;
using System.Net;
using Backeddy.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;

namespace Backeddy.App;

// Telas de configuração: escolher o destino (Backeddy Server ou HD externo).
public sealed partial class MainWindow
{
    void ShowSetupStart()
    {
        var back = status?.Destination is DestinationKind.Disk or DestinationKind.Server
            ? Ui.Link(Strings.T("back"), ShowHome) : null;
        var col = Ui.Column(14,
            Ui.Title(Strings.T("setupTitle")),
            Ui.Text(Strings.T("setupIntro"), 0.85),
            Ui.Card("\uE968", Strings.T("setupServer"), Strings.T("setupServerDetail"), ShowServerSearch),
            Ui.Card("\uEDA2", Strings.T("setupDisk"), Strings.T("setupDiskDetail"), ShowDiskList),
            Ui.Caption(Strings.T("setupPrivacy")));
        if (back is not null) col.Children.Insert(0, back);
        Show(col, Page.Setup);
    }

    // ---------------------------------------------------------------- Backeddy Server

    async void ShowServerSearch()
    {
        var list = Ui.Column(8);
        var searching = Ui.Row(10, Ui.Spinner(), Ui.Text(Strings.T("searching")));
        var manual = new TextBox { PlaceholderText = Strings.T("addressPlaceholder"), MinWidth = 300 };
        var col = Ui.Column(14,
            Ui.Link(Strings.T("back"), ShowSetupStart),
            Ui.Title(Strings.T("setupServer")),
            Ui.Text(Strings.T("serverIntro"), 0.85),
            searching,
            list,
            Ui.Strong(Strings.T("typeAddress")),
            Ui.Caption(Strings.T("typeAddressDetail")),
            Ui.Row(8, manual, Ui.Button(Strings.T("continue"), () =>
            {
                var h = manual.Text.Trim().Replace("http://", "").Replace("https://", "").TrimEnd('/');
                if (h.Length == 0) return;
                ShowServerPassword(new FoundServer(h, h, IPAddress.TryParse(h, out _) ? h : null));
            })));
        Show(col, Page.Setup);

        var found = await Discovery.FindAsync();
        if (page != Page.Setup) return;
        col.Children.Remove(searching);
        if (found.Count == 0)
        {
            list.Children.Add(Ui.Bar(InfoBarSeverity.Informational, null, Strings.T("noServers")));
        }
        foreach (var s in found)
            list.Children.Add(Ui.Card("\uE968", s.Name, s.Host + (s.Ip is null ? "" : $" · {s.Ip}"), () => ShowServerPassword(s)));
        list.Children.Add(Ui.Link(Strings.T("searchAgain"), ShowServerSearch));

        // sem servidor: ele precisa ficar em OUTRO computador (no mesmo PC, um defeito levaria o backup junto)
        var help = Ui.Column(8,
            Ui.Strong(Strings.T("noServerTitle")),
            Ui.Text(Strings.T("noServerIntro"), 0.85),
            Ui.Link(Strings.T("noServerHow"), () => OpenUrl("https://github.com/BackEddy/server#get-it")),
            Ui.Link(Strings.T("useDiskMeanwhile"), ShowDiskList));
        help.Margin = new Thickness(0, 12, 0, 0);
        list.Children.Add(help);
    }

    static void OpenUrl(string url)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception) { }
    }

    void ShowServerPassword(FoundServer s)
    {
        var pass = new PasswordBox { Header = Strings.T("serverPassword"), MaxWidth = 360, HorizontalAlignment = HorizontalAlignment.Left };
        var pcName = new TextBox
        {
            Header = Strings.T("pcName"),
            Text = Environment.MachineName,
            MaxWidth = 360,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        var feedback = Ui.Column(8);
        var go = new Button { Content = Strings.T("connect") };
        if (Application.Current.Resources.TryGetValue("AccentButtonStyle", out var accent) && accent is Style st) go.Style = st;
        bool busy = false;
        async void Connect()
        {
            if (busy || pass.Password.Length == 0) return;
            busy = true;
            go.IsEnabled = false;
            feedback.Children.Clear();
            feedback.Children.Add(Ui.Row(10, Ui.Spinner(), Ui.Text(Strings.T("connecting"))));
            var name = string.IsNullOrWhiteSpace(pcName.Text) ? Environment.MachineName : pcName.Text.Trim();
            var r = await ServiceClient.CallAsync(Cmd.SetupServer,
                new ServerSetup(s.Host, s.Ip, pass.Password, name, Profile, Environment.UserName), ServiceClient.Long);
            feedback.Children.Clear();
            go.IsEnabled = true;
            busy = false;
            if (r?.Ok == true)
            {
                await RefreshAndRouteAsync();
                return;
            }
            feedback.Children.Add(Ui.Bar(InfoBarSeverity.Error, Strings.T("couldNotConnect"), ServiceClient.ErrorText(r)));
        }
        go.Click += (_, _) => Connect();
        pass.KeyDown += (_, e) => { if (e.Key == Windows.System.VirtualKey.Enter) Connect(); };

        Show(Ui.Column(14,
            Ui.Link(Strings.T("back"), ShowServerSearch),
            Ui.Title(s.Name),
            Ui.Caption(s.Host),
            Ui.Text(Strings.T("serverPasswordIntro"), 0.85),
            pass,
            pcName,
            Ui.Caption(Strings.T("pcNameDetail")),
            go,
            feedback), Page.Setup);
    }

    // ---------------------------------------------------------------- HD externo

    void ShowDiskList()
    {
        var system = Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\";
        var drives = DriveInfo.GetDrives()
            .Where(d => d.IsReady && d.DriveType is DriveType.Removable or DriveType.Fixed
                        && !string.Equals(d.Name, system, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var feedback = Ui.Column(8);
        var list = Ui.Column(8);
        foreach (var d in drives)
        {
            string label = string.IsNullOrWhiteSpace(d.VolumeLabel) ? Strings.T("unnamedDisk") : d.VolumeLabel;
            string detail = Strings.F("diskSpace", Format.Size(d.AvailableFreeSpace), Format.Size(d.TotalSize))
                            + (d.DriveType == DriveType.Fixed ? " · " + Strings.T("internalDisk") : "");
            var root = d.Name;
            list.Children.Add(Ui.Card("\uEDA2", $"{label} ({root.TrimEnd('\\')})", detail, () => _ = UseDiskAsync(root, null, feedback)));
        }
        if (drives.Count == 0) list.Children.Add(Ui.Bar(InfoBarSeverity.Informational, null, Strings.T("noDisks")));

        Show(Ui.Column(14,
            Ui.Link(Strings.T("back"), ShowSetupStart),
            Ui.Title(Strings.T("setupDisk")),
            Ui.Text(Strings.T("diskIntro"), 0.85),
            list,
            Ui.Link(Strings.T("refreshList"), ShowDiskList),
            feedback), Page.Setup);
    }

    async System.Threading.Tasks.Task UseDiskAsync(string root, string? key, StackPanel feedback)
    {
        feedback.Children.Clear();
        feedback.Children.Add(Ui.Row(10, Ui.Spinner(), Ui.Text(Strings.T("preparingDisk"))));
        Root.IsHitTestVisible = false;
        var r = await ServiceClient.CallAsync(Cmd.SetupDisk, new DiskSetup(root, key, Profile, Environment.UserName), ServiceClient.Long);
        Root.IsHitTestVisible = true;
        feedback.Children.Clear();
        if (r?.Ok != true)
        {
            feedback.Children.Add(Ui.Bar(InfoBarSeverity.Error, Strings.T("couldNotUseDisk"), ServiceClient.ErrorText(r)));
            return;
        }
        var result = Ipc.From<DiskSetupResult>(r.Data);
        if (result?.NeedsKey == true) ShowKeyEntry(root);
        else if (result?.Key is { } newKey) ShowNewKey(newKey);
        else await RefreshAndRouteAsync();
    }

    void ShowNewKey(string key)
    {
        var keyText = new TextBlock
        {
            Text = key,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 24,
            IsTextSelectionEnabled = true,
            TextWrapping = TextWrapping.Wrap,
        };
        var copied = Ui.Caption("");
        var check = new CheckBox { Content = Strings.T("keyWritten") };
        var go = Ui.Button(Strings.T("continue"), () => _ = RefreshAndRouteAsync(), accent: true);
        go.IsEnabled = false;
        check.Checked += (_, _) => go.IsEnabled = true;
        check.Unchecked += (_, _) => go.IsEnabled = false;

        Show(Ui.Column(14,
            Ui.Title(Strings.T("keyTitle")),
            Ui.Text(Strings.T("keyIntro"), 0.85),
            new Border
            {
                Child = keyText,
                Padding = new Thickness(16),
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            },
            Ui.Row(10, Ui.Button(Strings.T("copy"), () =>
            {
                var p = new DataPackage();
                p.SetText(key);
                Clipboard.SetContent(p);
                copied.Text = Strings.T("copied");
            }), copied),
            Ui.Bar(InfoBarSeverity.Warning, null, Strings.T("keyWarning")),
            check,
            go), Page.Setup);
    }

    void ShowKeyEntry(string root)
    {
        var box = new TextBox
        {
            Header = Strings.T("recoveryKey"),
            PlaceholderText = "XXXX-XXXX-XXXX-XXXX-XXXX-XXXX",
            FontFamily = new FontFamily("Consolas"),
            MaxWidth = 420,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        var feedback = Ui.Column(8);
        Show(Ui.Column(14,
            Ui.Link(Strings.T("back"), ShowDiskList),
            Ui.Title(Strings.T("existingTitle")),
            Ui.Text(Strings.T("existingIntro"), 0.85),
            box,
            Ui.Button(Strings.T("connect"), () =>
            {
                if (RecoveryKey.Normalize(box.Text) is null)
                {
                    feedback.Children.Clear();
                    feedback.Children.Add(Ui.Bar(InfoBarSeverity.Error, null, Strings.T("err.wrongKey")));
                    return;
                }
                _ = UseDiskAsync(root, box.Text, feedback);
            }, accent: true),
            feedback), Page.Setup);
    }
}
