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
    /// <summary>
    /// 起動失敗時に標準エラーに含まれる文字列。
    /// </summary>
    private static readonly string StartupErrorOutput = "Application startup failed.";

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int ExtractIconEx(
        string lpszFile,
        int nIconIndex,
        out IntPtr phiconLarge,
        out IntPtr phiconSmall,
        int nIcons
    );

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

        var engineProcess = Process.Start(new ProcessStartInfo(
            Path.Join(Path.GetDirectoryName(Settings.Default.VoicevoxPath), Settings.Default.EngineFileRelativePath)
        )
        {
            RedirectStandardError = true,
            CreateNoWindow = true,
        }) ?? throw new Exception("Process.Start() が null を返しました。");

        var contextMenuStrip = new ContextMenuStrip();
        contextMenuStrip.Items.Add(new ToolStripMenuItem("終了", image: null, (sender, e) => this.Shutdown()));

        var assembly = Assembly.GetExecutingAssembly();
        var title = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product
                + " " + assembly.GetName().Version;

        var notifyIcon = new NotifyIcon()
        {
            Text = title,
            Icon = App.ExtractIconFromFile(Settings.Default.VoicevoxPath, 0),
            ContextMenuStrip = contextMenuStrip,
            Visible = true,
        };

        notifyIcon.Click += (_, _) => typeof(NotifyIcon)
            .GetMethod("ShowContextMenu", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(notifyIcon, parameters: null);

        engineProcess.EnableRaisingEvents = true;
        engineProcess.Exited += (sender, _) =>
        {
            var error = engineProcess.StandardError.ReadToEnd();
            if (error.Contains(App.StartupErrorOutput))
            {
                MessageBox.Show(
                    $"「{Settings.Default.EngineFileRelativePath}」の起動に失敗しました。\n\n" + error,
                    title
                );
            }
            notifyIcon.Dispose();
            Environment.Exit(0);
        };

        this.Exit += (_, _) =>
        {
            if (!engineProcess.HasExited)
            {
                engineProcess.Kill();
            }
            notifyIcon.Dispose();
        };

    }
}
