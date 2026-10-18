using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Backeddy.Core;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Backeddy.App;

/// <summary>
/// Janela principal. Fechar só esconde: o ícone na bandeja continua. As telas ficam em MainWindow.Setup.cs,
/// MainWindow.Home.cs, MainWindow.Exclusions.cs e MainWindow.Timeline.cs.
/// </summary>
public sealed partial class MainWindow : Window
{
    enum Page { Loading, ServiceDown, Setup, Home, Other }

    bool reallyClose;
    Page page = Page.Loading;
    ServiceStatus? status;
    readonly HashSet<string> seenAlerts = new();
    bool firstPoll = true;
    readonly DispatcherQueueTimer timer;

    /// <summary>Alerta novo do servidor (título, texto): o App mostra um aviso perto do relógio.</summary>
    public event Action<string, string>? AlertRaised;

    public MainWindow()
    {
        InitializeComponent();
        Title = "Backeddy";
        AppWindow.Resize(new Windows.Graphics.SizeInt32(680, 760));
        var ico = Path.Combine(AppContext.BaseDirectory, "Backeddy.ico");
        if (File.Exists(ico)) AppWindow.SetIcon(ico);
        AppWindow.Closing += (_, e) =>
        {
            if (reallyClose) return;
            e.Cancel = true;
            AppWindow.Hide();
        };
        Show(Ui.Column(12, Ui.Title("Backeddy"), Ui.Spinner()), Page.Loading);
        timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(5);
        timer.Tick += (_, _) => _ = PollAsync();
        timer.Start();
        _ = PollAsync();
    }

    public void ShowAndFocus()
    {
        AppWindow.Show();
        Activate();
        _ = PollAsync();
    }

    public void ReallyClose()
    {
        reallyClose = true;
        timer.Stop();
        Close();
    }

    XamlRoot XRoot => Root.XamlRoot;

    /// <summary>Troca a tela. scroll=false para telas que cuidam da própria rolagem (linha do tempo).</summary>
    void Show(UIElement view, Page p, bool scroll = true)
    {
        page = p;
        Root.Children.Clear();
        if (view is FrameworkElement fe && scroll) fe.Margin = new Thickness(28, 24, 28, 28);
        Root.Children.Add(scroll ? new ScrollViewer { Content = view } : view);
    }

    async Task PollAsync()
    {
        var st = await ServiceClient.StatusAsync();
        status = st;
        if (st is not null) CheckAlerts(st);
        if (!AppWindow.IsVisible) return;
        switch (page)
        {
            case Page.Loading:
            case Page.ServiceDown:
                Route();
                break;
            case Page.Home:
                if (st is null || st.State == BackupState.NotConfigured) Route();
                else UpdateHome(st);
                break;
        }
    }

    /// <summary>Vai para a tela certa conforme a situação do serviço.</summary>
    void Route()
    {
        if (status is null) ShowServiceDown();
        else if (status.State == BackupState.NotConfigured) ShowSetupStart();
        else ShowHome();
    }

    async Task RefreshAndRouteAsync()
    {
        status = await ServiceClient.StatusAsync();
        Route();
    }

    void CheckAlerts(ServiceStatus st)
    {
        foreach (var a in st.Alerts)
        {
            if (!seenAlerts.Add(a.Id + "|" + a.Text)) continue;
            if (!firstPoll || !AppWindow.IsVisible) AlertRaised?.Invoke(Strings.T("alertTitle"), a.Text);
        }
        firstPoll = false;
    }

    void ShowServiceDown()
    {
        Show(Ui.Column(14,
            Ui.Title("Backeddy"),
            Ui.Bar(InfoBarSeverity.Error, null, Strings.T("serviceDown")),
            Ui.Button(Strings.T("tryAgain"), () => _ = RefreshAndRouteAsync())), Page.ServiceDown);
    }

    static string Profile => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
}
