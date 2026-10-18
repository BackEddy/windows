using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Backeddy.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Backeddy.App;

// "O que entra no backup": a pasta do usuário e os outros discos internos; a pessoa desmarca o que não quer.
public sealed partial class MainWindow
{
    void ShowExclusions()
    {
        var st = status;
        if (st is null) { Route(); return; }
        var excluded = new HashSet<string>(st.Excluded.Select(Norm), StringComparer.OrdinalIgnoreCase);
        var boxes = new List<(CheckBox Box, string Path)>();
        CheckBox Box(string label, string path, string? detail = null)
        {
            var cb = new CheckBox { IsChecked = !excluded.Contains(Norm(path)) };
            cb.Content = detail is null ? label : Ui.Column(0, Ui.Text(label), Ui.Caption(detail));
            boxes.Add((cb, path));
            return cb;
        }

        var col = Ui.Column(14,
            Ui.Link(Strings.T("back"), ShowHome),
            Ui.Title(Strings.T("whatIsBackedUp")),
            Ui.Text(Strings.T("exclusionsIntro"), 0.85));

        var profile = st.Sources.FirstOrDefault(s => s.Label == "profile")?.Path ?? Profile;
        col.Children.Add(Ui.Strong(Strings.F("yourFolderPath", profile)));
        var folders = Ui.Column(4);
        bool hasOneDrive = false;
        try
        {
            foreach (var dir in new DirectoryInfo(profile).EnumerateDirectories().OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                if (dir.Name.StartsWith("OneDrive", StringComparison.OrdinalIgnoreCase)) { hasOneDrive = true; continue; }
                if ((dir.Attributes & (FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint)) != 0) continue;
                folders.Children.Add(Box(dir.Name, dir.FullName));
            }
        }
        catch (Exception) { }
        col.Children.Add(folders);
        col.Children.Add(Ui.Caption(Strings.T("exclusionsAlwaysOut")));
        if (hasOneDrive) col.Children.Add(Ui.Bar(InfoBarSeverity.Informational, null, Strings.T("oneDriveNote")));

        var drives = st.Sources.Where(s => s.Label == "drive").ToList();
        if (drives.Count > 0)
        {
            col.Children.Add(Ui.Strong(Strings.T("otherDisks")));
            var dcol = Ui.Column(4);
            foreach (var d in drives)
            {
                string? detail = null;
                try
                {
                    var di = new DriveInfo(d.Path);
                    var name = string.IsNullOrWhiteSpace(di.VolumeLabel) ? Strings.T("unnamedDisk") : di.VolumeLabel;
                    detail = name + " · " + Strings.F("diskUsed", Format.Size(di.TotalSize - di.TotalFreeSpace));
                }
                catch (Exception) { }
                dcol.Children.Add(Box(d.Path.TrimEnd('\\'), d.Path, detail));
            }
            col.Children.Add(dcol);
        }

        var feedback = Ui.Column(8);
        col.Children.Add(Ui.Row(10,
            Ui.Button(Strings.T("save"), async () =>
            {
                // mantém exclusões que não aparecem nesta lista (pastas mais fundas, discos desligados)
                var shown = new HashSet<string>(boxes.Select(b => Norm(b.Path)), StringComparer.OrdinalIgnoreCase);
                var list = excluded.Where(e => !shown.Contains(e))
                    .Concat(boxes.Where(b => b.Box.IsChecked != true).Select(b => Norm(b.Path)))
                    .ToList();
                var r = await ServiceClient.CallAsync(Cmd.SetExclusions, new ExclusionsPayload(list));
                feedback.Children.Clear();
                if (r?.Ok == true)
                {
                    status = await ServiceClient.StatusAsync() ?? status;
                    feedback.Children.Add(Ui.Bar(InfoBarSeverity.Success, null, Strings.T("savedNextBackup")));
                }
                else feedback.Children.Add(Ui.Bar(InfoBarSeverity.Error, null, ServiceClient.ErrorText(r)));
            }, accent: true),
            Ui.Button(Strings.T("cancel"), ShowHome)));
        col.Children.Add(feedback);
        Show(col, Page.Other);
    }

    static string Norm(string p) => p.Length <= 3 ? p.TrimEnd('\\') + "\\" : p.TrimEnd('\\');
}
