using System;
using System.Diagnostics;

namespace Esperecyan.NCVVoicevox;

/// <summary>
/// 特定の名前のプロセスがすべて終了することを監視します。
/// </summary>
internal class ProcessWatcher
{
    internal event EventHandler? AllExited;
    internal bool IsAllExited;

    private readonly string name;

    internal ProcessWatcher(string name)
    {
        this.name = name;
        this.Watch();
    }

    private void Watch()
    {
        var processes = Process.GetProcessesByName(this.name);
        if (processes.Length == 0)
        {
            this.IsAllExited = true;
            this.AllExited?.Invoke(this, EventArgs.Empty);
            Debug.WriteLine(1);
            return;
        }

        processes[0].EnableRaisingEvents = true;
        processes[0].Exited += (_, _) => this.Watch();
    }
}
