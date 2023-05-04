using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Application = System.Windows.Application;
using System.Windows.Forms;
using NAudio.Wave;
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
                + " " + assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
    }

    private static Icon ExtractIconFromFile(string path, int index)
    {
        App.ExtractIconEx(path, index, out var phiconLarge, out var phiconSmall, nIcons: 1);
        Icon.FromHandle(phiconSmall).Dispose();
        var icon = Icon.FromHandle(phiconLarge);
        icon.Dispose();

        return icon;
    }

    /// <summary>
    /// 音声出力デバイス名の一覧を取得します。
    /// </summary>
    /// <returns><see cref="WaveOut.DeviceNumber"/> へ指定するインデックス順で返します。</returns>
    private static IEnumerable<string> GetOutputDeviceNames()
    {
        var indexNamePairs = new List<string>();
        for (var i = 0; i < WaveOut.DeviceCount; i++)
        {
            indexNamePairs.Add(WaveOut.GetCapabilities(i).ProductName);
        }
        return indexNamePairs;
    }

    /// <summary>
    /// ユーザー設定から、選択中の音声出力デバイス名を取得します。
    /// </summary>
    /// <returns>既定のデバイスが選択されていた場合、もしくは存在しないデバイス名が選択されていた場合は、<c>null</c> を返します。</returns>
    private static string? GetCurrentOutputDeviceName()
    {
        var name = Settings.Default.OuputDeviceName;
        return !string.IsNullOrEmpty(name) && App.GetOutputDeviceNames().Contains(name) ? name : null;
    }

    /// <summary>
    /// ユーザー設定で選択中の音声出力デバイス名をもとに、<see cref="WaveOut.DeviceNumber"/> へ指定する値を取得します。
    /// </summary>
    /// <returns>既定のデバイスが選択されていた場合、もしくは存在しないデバイス名が選択されていた場合は、<c>-1</c> を返します。</returns>
    private static int GetCurrentOutputDeviceIndex()
    {
        var name = App.GetCurrentOutputDeviceName();
        return name == null ? -1 : App.GetOutputDeviceNames().ToList().IndexOf(name);
    }

    /// <summary>
    /// メニュー内の音声出力デバイス選択アイテムを更新します。
    /// </summary>
    /// <param name="menuItems"></param>
    private static void UpdateDeviceMenuItems(ToolStripItemCollection menuItems)
    {
        var currentDeviceName = App.GetCurrentOutputDeviceName();

        foreach (var item
            in menuItems.Cast<ToolStripMenuItem>().Where(item => item.Name.StartsWith("output-device")).ToList())
        {
            menuItems.Remove(item);
        }

        var labels = new[] { "既定のデバイス" }.Concat(App.GetOutputDeviceNames());
        for (var i = 0; i < labels.Count(); i++)
        {
            var name = labels.ElementAt(i);
            var item = new ToolStripMenuItem(name)
            {
                Name = "output-device" + (i == 0 ? "-default" : ""),
                Checked = currentDeviceName == (i == 0 ? null : name),
            };
            if (!item.Checked)
            {
                item.Click += (_, _) =>
                {
                    Settings.Default.OuputDeviceName = item.Name == "output-device-default" ? null : item.Text;
                    Settings.Default.Save();
                };
            }
            menuItems.Insert(menuItems.Count - 1, item);
        }
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

        var waveOut = new WaveOut();

        var engineServer = new EngineServer(Settings.Default.EnginePort);
        var proxyServer = new ProxyServer(Settings.Default.ProxyServerPort, Settings.Default.EnginePort);

        proxyServer.ProxyEvent += async (_, args) =>
        {
            if (!args.Response.IsSuccessStatusCode
                || args.Response.RequestMessage?.RequestUri?.LocalPath != "/synthesis")
            {
                return;
            }

            waveOut.DeviceNumber = App.GetCurrentOutputDeviceIndex();
            waveOut.Init(new WaveFileReader(await args.Response.Content.ReadAsStreamAsync()));
            waveOut.Play();
        };

        var contextMenuStrip = new ContextMenuStrip();
        contextMenuStrip.Items.Add(new ToolStripMenuItem("終了", image: null, (sender, e) => this.Shutdown()));

        contextMenuStrip.Opening += (_, _) => App.UpdateDeviceMenuItems(contextMenuStrip.Items);

        var notifyIcon = new NotifyIcon()
        {
            Text = App.Title,
            Icon = App.ExtractIconFromFile(Settings.Default.VoicevoxPath, 0),
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
    }
}
