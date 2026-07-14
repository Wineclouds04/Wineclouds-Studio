using System.Runtime.InteropServices;

namespace WinecloudsStudio.Services.Interop;

/// <summary>
/// DWM (Desktop Window Manager) P/Invoke methods.
/// Ported from EVE-O-Preview reference project.
/// </summary>
public static class DwmNativeMethods
{
    [DllImport("dwmapi.dll", PreserveSig = false)]
    public static extern void DwmEnableBlurBehindWindow(IntPtr hWnd, DWM_BLURBEHIND pBlurBehind);

    [DllImport("dwmapi.dll", PreserveSig = false)]
    public static extern void DwmExtendFrameIntoClientArea(IntPtr hWnd, MARGINS pMargins);

    [DllImport("dwmapi.dll", PreserveSig = false)]
    public static extern bool DwmIsCompositionEnabled();

    [DllImport("dwmapi.dll", PreserveSig = false)]
    public static extern void DwmGetColorizationColor(
        out int pcrColorization,
        [MarshalAs(UnmanagedType.Bool)] out bool pfOpaqueBlend);

    [DllImport("dwmapi.dll", PreserveSig = false)]
    public static extern void DwmEnableComposition(bool bEnable);

    [DllImport("dwmapi.dll", PreserveSig = false)]
    public static extern IntPtr DwmRegisterThumbnail(IntPtr dest, IntPtr source);

    [DllImport("dwmapi.dll", PreserveSig = false)]
    public static extern void DwmUnregisterThumbnail(IntPtr hThumbnail);

    [DllImport("dwmapi.dll", PreserveSig = false)]
    public static extern void DwmUpdateThumbnailProperties(IntPtr hThumbnail, DWM_THUMBNAIL_PROPERTIES props);

    [DllImport("dwmapi.dll", PreserveSig = false)]
    public static extern void DwmQueryThumbnailSourceSize(IntPtr hThumbnail, out System.Drawing.Size size);
}

[StructLayout(LayoutKind.Sequential)]
public class DWM_BLURBEHIND
{
    public uint dwFlags;
    [MarshalAs(UnmanagedType.Bool)]
    public bool fEnable;
    public IntPtr hRgnBlur;
    [MarshalAs(UnmanagedType.Bool)]
    public bool fTransitionOnMaximized;
}

[StructLayout(LayoutKind.Sequential)]
public class MARGINS
{
    public int cxLeftWidth;
    public int cxRightWidth;
    public int cyTopHeight;
    public int cyBottomHeight;
}
