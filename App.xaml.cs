using System;
using System.IO;
using System.Reflection;
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

    static App()
    {
        var assembly = Assembly.GetExecutingAssembly();
        App.Title = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product
                + " " + assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
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

        var ncvWatcher = new ProcessWatcher(Settings.Default.ParentProcessName);
        if (ncvWatcher.IsAllExited)
        {
            MessageBox.Show($"「{Settings.Default.ParentProcessName}」が起動していません。", App.Title);
            this.Shutdown();
            return;
        }

        var engineServer = new EngineServer(Settings.Default.EnginePort);

        var contextMenuStrip = new ContextMenuStrip()
        {
            Renderer = new VolumeIconRenderer(),
        };
        var menuItems = contextMenuStrip.Items;
        menuItems.Add(new ToolStripMenuItem("終了", image: null, (sender, e) => this.Shutdown()));

        var notifyIcon = new NotifyIcon()
        {
            Text = App.Title,
            Icon = IconExtractor.ExtractFromFile(Settings.Default.VoicevoxPath, 0),
            ContextMenuStrip = contextMenuStrip,
            Visible = true,
        };
        AppDomain.CurrentDomain.UnhandledException += (_, _) => notifyIcon.Dispose();

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

        ncvWatcher.AllExited += (_, _) =>
        {
            engineServer.Dispose();
            notifyIcon.Dispose();
            Environment.Exit(0);
        };
    }
}
