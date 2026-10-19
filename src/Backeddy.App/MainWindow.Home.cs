using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Backeddy.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Backeddy.App;

// Tela inicial: situação, destino, último backup, avisos do servidor e os botões principais.
public sealed partial class MainWindow
{
    TextBlock? homeHeadline, homeDetail, homeDest, homeLast, homeNext;
    StackPanel? homeBars, homeHeadRow;
    ProgressRing? homeSpinner;
    Button? homeBackupNow;
    StackPanel? homeUpdate;

    void ShowHome()
    {
        homeHeadline = Ui.Subtitle("");
        homeSpinner = new ProgressRing { IsActive = true, Width = 22, Height = 22, Visibility = Visibility.Collapsed };
        homeHeadRow = Ui.Row(12, homeSpinner, homeHeadline);
        homeDetail = Ui.Text("", 0.85);
        homeDest = Ui.Text("");
        homeLast = Ui.Text("");
        homeNext = Ui.Text("");
        homeBars = Ui.Column(8);
        homeUpdate = Ui.Column(0);
        homeBackupNow = Ui.Button(Strings.T("backupNow"), () => _ = BackupNowAsync(), accent: true);

        var facts = new Grid { ColumnSpacing = 16, RowSpacing = 6 };
        facts.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        facts.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        void Fact(int row, string label, TextBlock value)
        {
            facts.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var l = Ui.Text(label, 0.7);
            Grid.SetRow(l, row);
            Grid.SetRow(value, row);
            Grid.SetColumn(value, 1);
            facts.Children.Add(l);
            facts.Children.Add(value);
        }
        Fact(0, Strings.T("destination"), homeDest);
        Fact(1, Strings.T("lastBackup"), homeLast);
        Fact(2, Strings.T("nextBackup"), homeNext);

        Show(Ui.Column(18,
            Ui.Title("Backeddy"),
            homeUpdate,
            Ui.Column(6, homeHeadRow, homeDetail),
            homeBars,
            facts,
            Ui.Row(10, homeBackupNow, Ui.Button(Strings.T("restoreFiles"), ShowTimeline)),
            Ui.Column(6,
                Ui.Link(Strings.T("whatIsBackedUp"), ShowExclusions),
                Ui.Link(Strings.T("changeDestination"), () => _ = ChangeDestinationAsync())),
            Ui.Caption(Strings.F("versionLine", status?.Version ?? AppInfo.Version))), Page.Home);
        RenderUpdateBar();
        if (status is not null) UpdateHome(status);
    }

    void UpdateHome(ServiceStatus st)
    {
        if (homeHeadline is null || homeBars is null) return;
        var now = DateTimeOffset.Now;
        bool server = st.Destination == DestinationKind.Server;
        homeSpinner!.Visibility = st.State == BackupState.Running ? Visibility.Visible : Visibility.Collapsed;
        homeBackupNow!.IsEnabled = st.State != BackupState.Running;
        (homeHeadline.Text, homeDetail!.Text) = st.State switch
        {
            BackupState.Running => (Strings.T("stateRunning"),
                st.RunningSince is { } since ? Strings.F("runningSince", Format.Time(since)) : ""),
            BackupState.Waiting when st.Wait == WaitReason.LowBattery => (Strings.T("stateWaiting"), Strings.T("waitBattery")),
            BackupState.Waiting => (Strings.T("stateWaiting"),
                Strings.F(server ? "waitServer" : "waitDisk", st.DestinationName ?? "")),
            BackupState.Error => (Strings.T("stateError"), Format.Error(st.LastError) + " " + Strings.T("retrySoon")),
            _ when st.LastBackup is null => (Strings.T("stateFirst"), Strings.T("stateFirstDetail")),
            _ => (Strings.T("stateOk"), ""),
        };
        homeDetail.Visibility = homeDetail.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        homeDest!.Text = (server ? "Backeddy Server · " : Strings.T("externalDisk") + " · ") + (st.DestinationName ?? "");
        homeLast!.Text = st.LastBackup is { } last
            ? Format.When(last, now) + (st.LastFiles > 0 ? " · " + Strings.F("filesAndSize", Format.Count(st.LastFiles), Format.Size(st.LastBytes)) : "")
            : Strings.T("never");
        homeNext!.Text = st.State == BackupState.Running ? Strings.T("nowRunning")
            : st.NextBackup is { } next ? (next <= now ? Strings.T("soon") : Format.When(next, now)) : "—";

        homeBars.Children.Clear();
        if (st.Message == "kopiaMissing")
            homeBars.Children.Add(Ui.Bar(InfoBarSeverity.Error, null, Strings.T("err.kopiaMissing")));
        foreach (var a in st.Alerts)
        {
            var bar = Ui.Bar(InfoBarSeverity.Warning, Strings.T("alertTitle"), a.Text);
            bar.Content = Ui.Row(8,
                Ui.Text(Strings.T("remindAgain"), 0.8),
                Ui.Button(Strings.T("tomorrow"), () => _ = AckAsync(1)),
                Ui.Button(Strings.T("in3days"), () => _ = AckAsync(3)),
                Ui.Button(Strings.T("in7days"), () => _ = AckAsync(7)));
            homeBars.Children.Add(bar);
        }
        if (st.Restore is { } r)
        {
            if (r.Running)
                homeBars.Children.Add(Ui.Bar(InfoBarSeverity.Informational, Strings.T("restoring"), r.Target ?? ""));
            else if (r.Error is not null)
                homeBars.Children.Add(Ui.Bar(InfoBarSeverity.Error, Strings.T("restoreFailed"), Format.Error(r.Error), closable: true));
            else if (r.FinishedAt is { } done && now - done < TimeSpan.FromHours(1))
            {
                var bar = Ui.Bar(InfoBarSeverity.Success, Strings.T("restored"), r.Target ?? "", closable: true);
                var target = r.Target;
                bar.ActionButton = Ui.Button(Strings.T("showInFolder"), () => ShowInExplorer(target));
                homeBars.Children.Add(bar);
            }
        }
    }

    async System.Threading.Tasks.Task BackupNowAsync()
    {
        homeBackupNow!.IsEnabled = false;
        var r = await ServiceClient.CallAsync(Cmd.BackupNow);
        if (r?.Ok != true)
        {
            homeBackupNow.IsEnabled = true;
            await Ui.InfoAsync(XRoot, Strings.T("backupNow"), ServiceClient.ErrorText(r), Strings.T("ok"));
            return;
        }
        await System.Threading.Tasks.Task.Delay(1500);   // o serviço começa em seguida
        await PollAsync();
    }

    async System.Threading.Tasks.Task AckAsync(int days)
    {
        var r = await ServiceClient.CallAsync(Cmd.Ack, new AckRequest(days), TimeSpan.FromSeconds(30));
        if (r?.Ok != true)
            await Ui.InfoAsync(XRoot, Strings.T("alertTitle"), ServiceClient.ErrorText(r), Strings.T("ok"));
        await PollAsync();
    }

    async System.Threading.Tasks.Task ChangeDestinationAsync()
    {
        if (!await Ui.ConfirmAsync(XRoot, Strings.T("changeDestination"), Strings.T("changeDestinationConfirm"),
                Strings.T("change"), Strings.T("cancel"))) return;
        var r = await ServiceClient.CallAsync(Cmd.Forget, ServiceClient.Long);
        if (r?.Ok != true)
        {
            await Ui.InfoAsync(XRoot, Strings.T("changeDestination"), ServiceClient.ErrorText(r), Strings.T("ok"));
            return;
        }
        await RefreshAndRouteAsync();
    }

    static void ShowInExplorer(string? path)
    {
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = false });
        }
        catch (Exception) { }
    }
}

// Aviso de versão nova (só avisa; quem baixa e instala é a pessoa).
public sealed partial class MainWindow
{
    UpdateInfo? update;
    DispatcherQueueTimer? updateTimer;

    void StartUpdateChecks()
    {
        _ = CheckUpdateAsync();
        updateTimer = DispatcherQueue.CreateTimer();
        updateTimer.Interval = TimeSpan.FromHours(24);
        updateTimer.Tick += (_, _) => _ = CheckUpdateAsync();
        updateTimer.Start();
    }

    async Task CheckUpdateAsync()
    {
        var found = await UpdateCheck.CheckAsync(AppInfo.Version);
        if (found is null || found == update) return;
        update = found;
        if (page == Page.Home) RenderUpdateBar();
    }

    void RenderUpdateBar()
    {
        if (homeUpdate is null) return;
        homeUpdate.Children.Clear();
        if (update is not { } u) return;
        var bar = Ui.Bar(InfoBarSeverity.Informational, Strings.F("updateAvailable", u.Version), Strings.T("updateDetail"));
        bar.ActionButton = Ui.Button(Strings.T("updateDownload"), () => OpenUrl(u.Url));
        homeUpdate.Children.Add(bar);
    }
}
