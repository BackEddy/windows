using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backeddy.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Backeddy.App;

// Linha do tempo: escolhe o momento (data e hora), navega pelas pastas e restaura.
public sealed partial class MainWindow
{
    sealed record Crumb(string Name, string Obj, string OriginalPath);

    List<TimePoint> points = new();
    TimePoint? point;
    readonly List<Crumb> crumbs = new();
    StackPanel? tlPoints, tlCrumbs, tlEntries, tlBars;
    const int MaxEntries = 1500;

    async void ShowTimeline()
    {
        tlPoints = Ui.Column(4);
        tlCrumbs = Ui.Row(4);
        tlEntries = Ui.Column(2);
        tlBars = Ui.Column(8);
        point = null;
        crumbs.Clear();

        var grid = new Grid { ColumnSpacing = 20, RowSpacing = 12, Padding = new Thickness(28, 24, 28, 20) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(230) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var header = Ui.Column(8, Ui.Link(Strings.T("back"), ShowHome), Ui.Title(Strings.T("restoreFiles")),
            Ui.Text(Strings.T("timelineIntro"), 0.85), tlBars);
        Grid.SetColumnSpan(header, 2);
        grid.Children.Add(header);

        var left = new ScrollViewer { Content = tlPoints };
        Grid.SetRow(left, 1);
        grid.Children.Add(left);

        var right = new Grid { RowSpacing = 8 };
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var crumbScroll = new ScrollViewer
        {
            Content = tlCrumbs,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollMode = ScrollMode.Auto,
            VerticalScrollMode = ScrollMode.Disabled,
        };
        right.Children.Add(crumbScroll);
        var entriesScroll = new ScrollViewer { Content = tlEntries };
        Grid.SetRow(entriesScroll, 1);
        right.Children.Add(entriesScroll);
        Grid.SetRow(right, 1);
        Grid.SetColumn(right, 1);
        grid.Children.Add(right);

        Show(grid, Page.Other, scroll: false);
        tlPoints.Children.Add(Ui.Row(10, Ui.Spinner(), Ui.Text(Strings.T("loadingPoints"))));

        var r = await ServiceClient.CallAsync(Cmd.Points, ServiceClient.Long);
        tlPoints.Children.Clear();
        if (r?.Ok != true)
        {
            tlBars.Children.Add(Ui.Bar(InfoBarSeverity.Error, Strings.T("couldNotList"), ServiceClient.ErrorText(r)));
            return;
        }
        points = Ipc.From<List<TimePoint>>(r.Data) ?? new();
        if (points.Count == 0)
        {
            tlPoints.Children.Add(Ui.Text(Strings.T("noPoints"), 0.8));
            return;
        }
        string? day = null;
        foreach (var p in points)
        {
            var d = Format.Day(p.Time);
            if (d != day)
            {
                day = d;
                var h = Ui.Strong(d);
                h.Margin = new Thickness(0, tlPoints.Children.Count == 0 ? 0 : 10, 0, 2);
                tlPoints.Children.Add(h);
            }
            var pp = p;
            var b = new Button
            {
                Content = Format.Time(p.Time),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
            };
            b.Click += (_, _) => OpenPoint(pp);
            tlPoints.Children.Add(b);
        }
        OpenPoint(points[0]);
    }

    void OpenPoint(TimePoint p)
    {
        point = p;
        crumbs.Clear();
        RenderCrumbs();
        tlEntries!.Children.Clear();
        foreach (var item in p.Items)
        {
            var name = item.Label == "profile" ? Strings.F("yourFolderPath", item.Source) : item.Source.TrimEnd('\\');
            var it = item;
            tlEntries.Children.Add(EntryRow(true, name, null, item.Obj, item.Source, null,
                () => _ = OpenDirAsync(new Crumb(name, it.Obj, it.Source))));
        }
    }

    async Task OpenDirAsync(Crumb c, bool push = true)
    {
        if (point is null) return;
        if (push) crumbs.Add(c);
        RenderCrumbs();
        tlEntries!.Children.Clear();
        tlEntries.Children.Add(Ui.Row(10, Ui.Spinner(), Ui.Text(Strings.T("loading"))));
        var r = await ServiceClient.CallAsync(Cmd.ListDir, new DirRequest(c.Obj), ServiceClient.Long);
        if (crumbs.LastOrDefault() != c) return;   // a pessoa já clicou em outra pasta
        tlEntries.Children.Clear();
        if (r?.Ok != true)
        {
            tlEntries.Children.Add(Ui.Bar(InfoBarSeverity.Error, Strings.T("couldNotList"), ServiceClient.ErrorText(r)));
            return;
        }
        var entries = Ipc.From<List<DirEntry>>(r.Data) ?? new();
        if (entries.Count == 0) tlEntries.Children.Add(Ui.Text(Strings.T("emptyFolder"), 0.7));
        var source = crumbs[0].OriginalPath;
        foreach (var e in entries.Take(MaxEntries))
        {
            var path = c.OriginalPath.TrimEnd('\\') + "\\" + e.Name;
            var detail = (e.Size > 0 ? Format.Size(e.Size) : "") +
                         (e.Modified is { } m ? (e.Size > 0 ? " · " : "") + Format.When(m, DateTimeOffset.Now) : "");
            var en = e;
            var refObj = FileHistory.Ref(c.Obj, e.Name);
            Action? versions = e.IsDir ? null : () => _ = ShowVersionsAsync(source, path, en.Name);
            tlEntries.Children.Add(EntryRow(e.IsDir, e.Name, detail, refObj, path, versions,
                e.IsDir ? () => _ = OpenDirAsync(new Crumb(en.Name, en.Obj, path)) : null));
        }
        if (entries.Count > MaxEntries)
            tlEntries.Children.Add(Ui.Caption(Strings.F("moreEntries", MaxEntries, entries.Count)));
    }

    void RenderCrumbs()
    {
        tlCrumbs!.Children.Clear();
        if (point is null) return;
        var p = point;
        tlCrumbs.Children.Add(Ui.Link(Format.Day(p.Time) + " " + Format.Time(p.Time), () => OpenPoint(p)));
        for (int i = 0; i < crumbs.Count; i++)
        {
            var c = crumbs[i];
            int keep = i + 1;
            tlCrumbs.Children.Add(Ui.Text("›", 0.6));
            if (i == crumbs.Count - 1)
            {
                var refObj = i == 0 ? c.Obj : FileHistory.Ref(crumbs[i - 1].Obj, c.Name);
                tlCrumbs.Children.Add(new DropDownButton { Content = c.Name, Flyout = RestoreMenu(refObj, true, c.OriginalPath, null) });
            }
            else
                tlCrumbs.Children.Add(Ui.Link(c.Name, () =>
                {
                    crumbs.RemoveRange(keep, crumbs.Count - keep);
                    _ = OpenDirAsync(c, push: false);
                }));
        }
        foreach (var child in tlCrumbs.Children.OfType<FrameworkElement>()) child.VerticalAlignment = VerticalAlignment.Center;
    }

    /// <summary>Linha de uma pasta ou arquivo: clicar numa pasta entra nela; o botão "…" (ou clicar num arquivo) mostra a restauração.</summary>
    UIElement EntryRow(bool isDir, string name, string? detail, string refObj, string originalPath, Action? versions, Action? open)
    {
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var icon = new FontIcon { Glyph = isDir ? "" : "", FontSize = 18, VerticalAlignment = VerticalAlignment.Center };
        var texts = string.IsNullOrEmpty(detail) ? (UIElement)Ui.Text(name) : Ui.Column(0, Ui.Text(name), Ui.Caption(detail));
        Grid.SetColumn((FrameworkElement)texts, 1);
        grid.Children.Add(icon);
        grid.Children.Add(texts);

        var row = new Button
        {
            Content = grid,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 6, 8, 6),
        };
        if (open is not null) row.Click += (_, _) => open();
        else row.Flyout = RestoreMenu(refObj, isDir, originalPath, versions);

        var more = new Button { Content = new FontIcon { Glyph = "", FontSize = 14 }, Flyout = RestoreMenu(refObj, isDir, originalPath, versions) };
        ToolTipService.SetToolTip(more, Strings.T("restore"));
        var line = new Grid { ColumnSpacing = 6 };
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(more, 1);
        line.Children.Add(row);
        line.Children.Add(more);
        return line;
    }

    MenuFlyout RestoreMenu(string refObj, bool isDir, string originalPath, Action? versions)
    {
        var m = new MenuFlyout();
        m.Items.Add(Ui.MenuItem(Strings.T("restoreBeside"), () => _ = RestoreAsync(refObj, isDir, originalPath, false, point?.Time)));
        m.Items.Add(Ui.MenuItem(Strings.T("restoreOriginal"), () => _ = RestoreAsync(refObj, isDir, originalPath, true, point?.Time)));
        if (versions is not null) m.Items.Add(Ui.MenuItem(Strings.T("versions"), versions));
        return m;
    }

    /// <summary>Todas as versões diferentes de um arquivo, de todos os backups, numa janelinha.</summary>
    async Task ShowVersionsAsync(string source, string path, string name)
    {
        var rel = path.Length > source.TrimEnd('\\').Length ? path[(source.TrimEnd('\\').Length + 1)..] : name;
        var list = Ui.Column(6, Ui.Row(10, Ui.Spinner(), Ui.Text(Strings.T("searchingVersions"))));
        var dlg = new ContentDialog
        {
            XamlRoot = XRoot,
            Title = Strings.F("versionsOf", name),
            Content = new ScrollViewer { Content = list, MaxHeight = 420 },
            CloseButtonText = Strings.T("close"),
        };
        var showing = dlg.ShowAsync();
        var r = await ServiceClient.CallAsync(Cmd.Versions, new VersionsRequest(source, rel), ServiceClient.Long);
        list.Children.Clear();
        var versions = r?.Ok == true ? Ipc.From<List<FileVersion>>(r.Data) ?? new() : null;
        if (versions is null) list.Children.Add(Ui.Bar(InfoBarSeverity.Error, null, ServiceClient.ErrorText(r)));
        else if (versions.Count == 0) list.Children.Add(Ui.Text(Strings.T("noVersions"), 0.8));
        else
        {
            list.Children.Add(Ui.Caption(Strings.F("versionsCount", versions.Count)));
            foreach (var v in versions)
            {
                var vv = v;
                var title = Format.Day(v.PointTime) + " " + Format.Time(v.PointTime);
                var detail = Format.Size(v.Size) + (v.Modified is { } m ? " · " + Strings.F("modifiedAt", Format.When(m, DateTimeOffset.Now)) : "");
                var menu = new MenuFlyout();
                menu.Items.Add(Ui.MenuItem(Strings.T("restoreBeside"), () => { dlg.Hide(); _ = RestoreAsync(vv.Ref, false, path, false, vv.PointTime); }));
                menu.Items.Add(Ui.MenuItem(Strings.T("restoreOriginal"), () => { dlg.Hide(); _ = RestoreAsync(vv.Ref, false, path, true, vv.PointTime); }));
                var row = new Grid { ColumnSpacing = 12 };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var texts = Ui.Column(0, Ui.Text(title), Ui.Caption(detail));
                var btn = new DropDownButton { Content = Strings.T("restore"), Flyout = menu, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(btn, 1);
                row.Children.Add(texts);
                row.Children.Add(btn);
                list.Children.Add(row);
            }
        }
        await showing;
    }

    async Task RestoreAsync(string refObj, bool isDir, string originalPath, bool original, DateTimeOffset? when)
    {
        if (when is not { } time) return;
        await Task.Delay(150);   // deixa a janelinha de versões fechar antes de abrir a confirmação
        var label = Format.Day(time) + " " + Format.Time(time);
        if (original && !await Ui.ConfirmAsync(XRoot, Strings.T("restoreOriginal"),
                Strings.F("restoreOriginalConfirm", originalPath, label), Strings.T("replace"), Strings.T("cancel")))
            return;
        var r = await ServiceClient.CallAsync(Cmd.Restore,
            new RestoreRequest(refObj, isDir, originalPath, original, time, Strings.T("restoredWord")));
        if (r?.Ok != true)
        {
            tlBars?.Children.Clear();
            tlBars?.Children.Add(Ui.Bar(InfoBarSeverity.Error, Strings.T("restoreFailed"), ServiceClient.ErrorText(r), closable: true));
            return;
        }
        status = await ServiceClient.StatusAsync() ?? status;
        ShowHome();   // a tela inicial mostra o andamento e o botão "Mostrar na pasta"
    }
}
