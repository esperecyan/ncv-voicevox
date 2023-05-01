using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Windows.Forms;
using Esperecyan.NCVVoicevox.Properties;

namespace Esperecyan.NCVVoicevox;

internal class EngineServer : IDisposable
{
    /// <summary>
    /// VOICEVOXエンジンの起動失敗時に標準エラーに含まれる文字列。
    /// </summary>
    private static readonly string EngineStartupErrorOutput = "Application startup failed.";

    private static readonly int MinEphemeralPort = 49152;
    private static readonly int MaxEphemeralPort = 65535;

    /// <summary>
    /// <see cref="Dispose"/> が呼ばれる以外でVOICEVOXエンジンが終了したときに呼ばれるイベントハンドラ。
    /// </summary>
    internal event EventHandler<EventArgs> ExitedUnexpectedly = (_, _) => { };

    internal int Port { get; private set; }

    private readonly Process process;

    private static int FindUnusedTCPPort()
    {
        var usedPorts = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners()
            .Select(listener => listener.Port);

        var random = new Random();
        int port;
        do
        {
            port = random.Next(EngineServer.MinEphemeralPort, EngineServer.MaxEphemeralPort + 1);
        } while (usedPorts.Contains(port));

        return port;
    }

    internal EngineServer()
    {
        this.Port = EngineServer.FindUnusedTCPPort();

        var info = new ProcessStartInfo(
            Path.Join(Path.GetDirectoryName(Settings.Default.VoicevoxPath), Settings.Default.EngineFileRelativePath)
        )
        {
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var argument in new[] { "--port", this.Port.ToString() })
        {
            info.ArgumentList.Add(argument);
        }
        this.process = Process.Start(info) ?? throw new Exception("Process.Start() が null を返しました。");

        this.process.EnableRaisingEvents = true;
        this.process.Exited += this.EngineProcess_Exited;
    }

    public void Dispose()
    {
        this.process.Exited -= this.EngineProcess_Exited;
        this.process.Kill();
    }

    private void EngineProcess_Exited(object? sender, EventArgs e)
    {
        var error = this.process.StandardError.ReadToEnd();
        if (error.Contains(EngineServer.EngineStartupErrorOutput))
        {
            MessageBox.Show(
                $"「{Settings.Default.EngineFileRelativePath}」の起動に失敗しました。\n\n" + error,
                App.Title
            );
        }

        this.ExitedUnexpectedly(this, EventArgs.Empty);
    }
}
