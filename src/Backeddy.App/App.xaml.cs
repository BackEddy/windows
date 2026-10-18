using System;
using System.IO;
using System.Linq;
using System.Threading;
using Backeddy.Core;
using Microsoft.UI.Xaml;

namespace Backeddy.App;

/// <summary>
/// Uma instância só: abrir de novo (menu Iniciar) traz a janela da que já está rodando.
/// Com --tray (início do Windows), começa só com o ícone na bandeja.
/// </summary>
public partial class App : Application
{
    static Mutex? single;
    MainWindow? window;
    Tray? tray;
    EventWaitHandle? showEvent;

    public App() => InitializeComponent();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        single = new Mutex(true, "Local\\Backeddy.App", out bool first);
        if (!first)
        {
            try { EventWaitHandle.OpenExisting(AppInfo.ShowEventName).Set(); } catch (Exception) { }
            Exit();
            return;
        }
        window = new MainWindow();
        var ui = window.DispatcherQueue;
        showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, AppInfo.ShowEventName);
        var waiter = new Thread(() => { while (showEvent.WaitOne()) ui.TryEnqueue(ShowWindow); }) { IsBackground = true };
        waiter.Start();

        tray = new Tray(Path.Combine(AppContext.BaseDirectory, "Backeddy.ico"), Strings.T("trayTooltip"), ShowWindow, QuitTray);
        if (!Environment.GetCommandLineArgs().Contains("--tray")) ShowWindow();
    }

    void ShowWindow() => window?.ShowAndFocus();

    // fecha só o ícone e a janela: o serviço continua fazendo os backups
    void QuitTray()
    {
        tray?.Dispose();
        tray = null;
        window?.ReallyClose();
        Exit();
    }
}
