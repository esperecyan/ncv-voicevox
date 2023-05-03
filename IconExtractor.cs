using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace Esperecyan.NCVVoicevox;

internal static class IconExtractor
{

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int ExtractIconEx(
        string lpszFile,
        int nIconIndex,
        out IntPtr phiconLarge,
        out IntPtr phiconSmall,
        int nIcons
    );

    internal static Icon ExtractFromFile(string path, int index)
    {
        IconExtractor.ExtractIconEx(path, index, out var phiconLarge, out var phiconSmall, nIcons: 1);
        Icon.FromHandle(phiconSmall).Dispose();
        var icon = Icon.FromHandle(phiconLarge);
        icon.Dispose();

        return icon;
    }
}
