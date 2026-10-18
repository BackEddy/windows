using System;
using System.IO;
using System.Threading.Tasks;
using Backeddy.Core;
using Microsoft.UI.Xaml;

namespace Backeddy.App;

/// <summary>Janela principal. Fechar só esconde: o ícone na bandeja continua.</summary>
public sealed partial class MainWindow : Window
{
    bool reallyClose;

    public MainWindow()
    {
        InitializeComponent();
        Title = "Backeddy";
        AppWindow.Resize(new Windows.Graphics.SizeInt32(560, 420));
        var ico = Path.Combine(AppContext.BaseDirectory, "Backeddy.ico");
        if (File.Exists(ico)) AppWindow.SetIcon(ico);
        AppWindow.Closing += (_, e) =>
        {
            if (reallyClose) return;
            e.Cancel = true;
            AppWindow.Hide();
        };
        TaglineText.Text = Strings.T("tagline");
        PreviewBar.Message = Strings.T("preview");
        RefreshButton.Content = Strings.T("refresh");
        RefreshButton.Click += async (_, _) => await RefreshAsync();
    }

    public void ShowAndFocus()
    {
        AppWindow.Show();
        Activate();
        _ = RefreshAsync();
    }

    public void ReallyClose()
    {
        reallyClose = true;
        Close();
    }

    async Task RefreshAsync()
    {
        var st = await ServiceClient.StatusAsync();
        if (st is null)
        {
            ServiceText.Text = Strings.T("serviceDown");
            StateText.Text = "";
            return;
        }
        ServiceText.Text = Strings.F("serviceOk", st.Version);
        StateText.Text = st.State == BackupState.NotConfigured ? Strings.T("notConfigured") : st.Message ?? "";
    }
}
