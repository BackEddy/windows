using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace Backeddy.App;

/// <summary>Pecinhas para montar as telas em código (sem XAML para cada tela).</summary>
static class Ui
{
    static Style? Res(string key) => Application.Current.Resources.TryGetValue(key, out var s) ? s as Style : null;

    public static TextBlock Title(string text) => new() { Text = text, Style = Res("TitleTextBlockStyle"), TextWrapping = TextWrapping.Wrap };
    public static TextBlock Subtitle(string text) => new() { Text = text, Style = Res("SubtitleTextBlockStyle"), TextWrapping = TextWrapping.Wrap };
    public static TextBlock Strong(string text) => new() { Text = text, Style = Res("BodyStrongTextBlockStyle"), TextWrapping = TextWrapping.Wrap };
    public static TextBlock Text(string text, double opacity = 1) => new() { Text = text, TextWrapping = TextWrapping.Wrap, Opacity = opacity };
    public static TextBlock Caption(string text) => new() { Text = text, Style = Res("CaptionTextBlockStyle"), TextWrapping = TextWrapping.Wrap, Opacity = 0.75 };

    public static Button Button(string text, Action onClick, bool accent = false)
    {
        var b = new Button { Content = text };
        if (accent && Res("AccentButtonStyle") is { } s) b.Style = s;
        b.Click += (_, _) => onClick();
        return b;
    }

    public static HyperlinkButton Link(string text, Action onClick)
    {
        var b = new HyperlinkButton { Content = text, Padding = new Thickness(0) };
        b.Click += (_, _) => onClick();
        return b;
    }

    public static StackPanel Column(double spacing, params UIElement[] children)
    {
        var p = new StackPanel { Spacing = spacing };
        foreach (var c in children) p.Children.Add(c);
        return p;
    }

    public static StackPanel Row(double spacing, params UIElement[] children)
    {
        var p = new StackPanel { Orientation = Orientation.Horizontal, Spacing = spacing };
        foreach (var c in children) p.Children.Add(c);
        return p;
    }

    /// <summary>Um "cartão" clicável que ocupa a largura toda: título em negrito e uma linha de explicação.</summary>
    public static Button Card(string glyph, string title, string detail, Action onClick)
    {
        var grid = new Grid { ColumnSpacing = 14 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var icon = new FontIcon { Glyph = glyph, FontSize = 26, VerticalAlignment = VerticalAlignment.Center };
        var texts = Column(2, Strong(title), Caption(detail));
        Grid.SetColumn(texts, 1);
        grid.Children.Add(icon);
        grid.Children.Add(texts);
        var b = new Button
        {
            Content = grid,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(16, 12, 16, 12),
        };
        b.Click += (_, _) => onClick();
        return b;
    }

    public static InfoBar Bar(InfoBarSeverity severity, string? title, string message, bool closable = false) => new()
    {
        Severity = severity,
        Title = title ?? "",
        Message = message,
        IsOpen = true,
        IsClosable = closable,
    };

    public static ProgressRing Spinner() => new() { IsActive = true, Width = 20, Height = 20 };

    /// <summary>Pergunta com dois botões. true = confirmou.</summary>
    public static async Task<bool> ConfirmAsync(XamlRoot root, string title, string message, string yes, string no)
    {
        var d = new ContentDialog
        {
            XamlRoot = root,
            Title = title,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
            PrimaryButtonText = yes,
            CloseButtonText = no,
            DefaultButton = ContentDialogButton.Close,
        };
        return await d.ShowAsync() == ContentDialogResult.Primary;
    }

    public static async Task InfoAsync(XamlRoot root, string title, string message, string ok)
    {
        var d = new ContentDialog
        {
            XamlRoot = root,
            Title = title,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
            CloseButtonText = ok,
        };
        await d.ShowAsync();
    }

    public static MenuFlyoutItem MenuItem(string text, Action onClick)
    {
        var i = new MenuFlyoutItem { Text = text };
        i.Click += (_, _) => onClick();
        return i;
    }
}
