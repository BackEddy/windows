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
            tlEntries.Children.Add(EntryRow(true, name, null, new DirEntry(name, true, 0, null, item.Obj), item.Source,
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
        foreach (var e in entries.Take(MaxEntries))
        {
            var path = c.OriginalPath.TrimEnd('\\') + "\\" + e.Name;
            var detail = (e.Size > 0 ? Format.Size(e.Size) : "") +
                         (e.Modified is { } m ? (e.Size > 0 ? " · " : "") + Format.When(m, DateTimeOffset.Now) : "");
            var en = e;
            tlEntries.Children.Add(EntryRow(e.IsDir, e.Name, detail, e, path,
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
                var restoreHere = new DropDownButton { Content = c.Name, Flyout = RestoreMenu(new DirEntry(c.Name, true, 0, null, c.Obj), c.OriginalPath) };
                tlCrumbs.Children.Add(restoreHere);
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
    UIElement EntryRow(bool isDir, string name, string? detail, DirEntry entry, string originalPath, Action? open)
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
        else row.Flyout = RestoreMenu(entry, originalPath);

        var more = new Button { Content = new FontIcon { Glyph = "", FontSize = 14 }, Flyout = RestoreMenu(entry, originalPath) };
        ToolTipService.SetToolTip(more, Strings.T("restore"));
        var line = new Grid { ColumnSpacing = 6 };
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(more, 1);
        line.Children.Add(row);
        line.Children.Add(more);
        return line;
    }

    MenuFlyout RestoreMenu(DirEntry entry, string originalPath)
    {
        var m = new MenuFlyout();
        m.Items.Add(Ui.MenuItem(Strings.T("restoreBeside"), () => _ = RestoreAsync(entry, originalPath, false)));
        m.Items.Add(Ui.MenuItem(Strings.T("restoreOriginal"), () => _ = RestoreAsync(entry, originalPath, true)));
        return m;
    }

    async Task RestoreAsync(DirEntry entry, string originalPath, bool original)
    {
        if (point is null) return;
        var when = Format.Day(point.Time) + " " + Format.Time(point.Time);
        if (original && !await Ui.ConfirmAsync(XRoot, Strings.T("restoreOriginal"),
                Strings.F("restoreOriginalConfirm", originalPath, when), Strings.T("replace"), Strings.T("cancel")))
            return;
        var r = await ServiceClient.CallAsync(Cmd.Restore,
            new RestoreRequest(entry.Obj, entry.IsDir, originalPath, original, point.Time, Strings.T("restoredWord")));
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
