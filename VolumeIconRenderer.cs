using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Esperecyan.NCVVoicevox;

/// <summary>
/// 「volume」という名前の項目に、スピーカーアイコンを表示します。
/// </summary>
internal class VolumeIconRenderer : ToolStripProfessionalRenderer
{
    private static readonly int Size = 24;
    private static readonly Icon Icon;

    static VolumeIconRenderer()
    {
        VolumeIconRenderer.Icon = IconExtractor.ExtractFromFile(
            Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.SystemX86), "DDORes.dll"),
            index: 1
        );
    }

    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
    {
        base.OnRenderImageMargin(e);
        var volumeItem = e.ToolStrip.Items.Cast<ToolStripItem>().First(item => item.Name == "volume");
        e.Graphics.DrawIcon(VolumeIconRenderer.Icon, new Rectangle(
            x: 1,
            y: volumeItem.Bounds.Y + volumeItem.Height / 2 - VolumeIconRenderer.Size / 2,
            width: VolumeIconRenderer.Size,
            height: VolumeIconRenderer.Size
        ));
    }
}
