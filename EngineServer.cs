using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using Esperecyan.NCVVoicevox.Properties;

namespace Esperecyan.NCVVoicevox;

internal class EngineServer : IDisposable
{
    /// <summary>
    /// VOICEVOXエンジンの起動失敗時に標準エラーに含まれる文字列。
    /// </summary>
    private static readonly string EngineStartupErrorOutput = "Application startup failed.";

    /// <summary>
    /// <see cref="Dispose"/> が呼ばれる以外でVOICEVOXエンジンが終了したときに呼ばれるイベントハンドラ。
    /// </summary>
    internal event EventHandler<EventArgs> ExitedUnexpectedly = (_, _) => { };

    private readonly Process process;

    internal EngineServer(int port)
    {
        var info = new ProcessStartInfo(
            Path.Join(Path.GetDirectoryName(Settings.Default.VoicevoxPath), Settings.Default.EngineFileRelativePath)
        )
        {
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var argument in new[] { "--port", port.ToString() })
        {
            info.ArgumentList.Add(argument);
        }
        this.process = Process.Start(info) ?? throw new Exception("Process.Start() が null を返しました。");

        this.process.EnableRaisingEvents = true;
        this.process.Exited += this.EngineProcess_Exited;

        AppDomain.CurrentDomain.UnhandledException += (_, _) => this.Dispose();
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
