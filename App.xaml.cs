using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Application = System.Windows.Application;
using System.Windows.Forms;
using Esperecyan.NCVVoicevox.Properties;

namespace Esperecyan.NCVVoicevox;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    internal static readonly string Title;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int ExtractIconEx(
        string lpszFile,
        int nIconIndex,
        out IntPtr phiconLarge,
        out IntPtr phiconSmall,
        int nIcons
    );

    static App()
    {
        var assembly = Assembly.GetExecutingAssembly();
        App.Title = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product
                + " " + assembly.GetName().Version;
    }

    private static Icon ExtractIconFromFile(string path, int index)
    {
        App.ExtractIconEx(path, index, out var phiconLarge, out var phiconSmall, nIcons: 1);
        Icon.FromHandle(phiconSmall).Dispose();
        var icon = Icon.FromHandle(phiconLarge);
        icon.Dispose();

        return icon;
    }

    private App()
    {
        Settings.Default.Upgrade();

        if (Settings.Default.VoicevoxPath != null && !File.Exists(Settings.Default.VoicevoxPath))
        {
            Settings.Default.VoicevoxPath = null;
            Settings.Default.Save();
        }

        if (Settings.Default.VoicevoxPath == null)
        {
            var openFileDialog = new OpenFileDialog()
            {
                Filter = Settings.Default.ReferencedFileName + "|" + Settings.Default.ReferencedFileName,
            };
            if (openFileDialog.ShowDialog() != DialogResult.OK)
            {
                this.Shutdown();
                return;
            }
            Settings.Default.VoicevoxPath = openFileDialog.FileName;
            Settings.Default.Save();
        }

        var engineServer = new EngineServer();

        var contextMenuStrip = new ContextMenuStrip();
        contextMenuStrip.Items.Add(new ToolStripMenuItem("終了", image: null, (sender, e) => this.Shutdown()));

        var notifyIcon = new NotifyIcon()
        {
            Text = App.Title,
            Icon = App.ExtractIconFromFile(Settings.Default.VoicevoxPath, 0),
            ContextMenuStrip = contextMenuStrip,
            Visible = true,
        };

        notifyIcon.Click += (_, _) => typeof(NotifyIcon)
            .GetMethod("ShowContextMenu", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(notifyIcon, parameters: null);

        engineServer.ExitedUnexpectedly += (_, _) =>
        {
            notifyIcon.Dispose();
            Environment.Exit(0);
        };

        this.Exit += (_, _) =>
        {
            engineServer.Dispose();
            notifyIcon.Dispose();
        };
    }
}
