using WinecloudsStudio.Modules.WindowManager.Services.Interface;
using WinecloudsStudio.Modules.WindowManager.Services.Interop;
using System.Runtime.InteropServices;

namespace WinecloudsStudio.Modules.WindowManager.Services.Implementation;

/// <summary>
/// Wraps the DWM Live Thumbnail API.
/// Ported from EVE-O-Preview reference project.
/// </summary>
public class DwmThumbnail : IDwmThumbnail
{
    private readonly IWindowManager _windowManager;
    private IntPtr _handle;
    private DWM_THUMBNAIL_PROPERTIES? _properties;

    public DwmThumbnail(IWindowManager windowManager)
    {
        _windowManager = windowManager;
        _handle = IntPtr.Zero;
    }

    public void Register(IntPtr destination, IntPtr source)
    {
        _properties = new DWM_THUMBNAIL_PROPERTIES
        {
            dwFlags = DWM_TNP_CONSTANTS.DWM_TNP_VISIBLE
                      | DWM_TNP_CONSTANTS.DWM_TNP_RECTOPACITY
                      | DWM_TNP_CONSTANTS.DWM_TNP_RECTDESTINATION
                      | DWM_TNP_CONSTANTS.DWM_TNP_SOURCECLIENTAREAONLY,
            opacity = 255,
            fVisible = true,
            fSourceClientAreaOnly = true
        };

        if (!_windowManager.IsCompositionEnabled)
        {
            return;
        }

        try
        {
            _handle = DwmNativeMethods.DwmRegisterThumbnail(destination, source);
        }
        catch (ArgumentException)
        {
            // Source window may have closed between detection and registration
            _handle = IntPtr.Zero;
        }
        catch (COMException)
        {
            // DWM may not be available (e.g. switching user accounts)
            _handle = IntPtr.Zero;
        }
    }

    public void Unregister()
    {
        if (!_windowManager.IsCompositionEnabled || _handle == IntPtr.Zero)
        {
            return;
        }

        try
        {
            DwmNativeMethods.DwmUnregisterThumbnail(_handle);
        }
        catch (ArgumentException)
        {
        }
        catch (COMException)
        {
        }
    }

    public void Move(int left, int top, int right, int bottom)
    {
        if (_properties == null) return;
        _properties.rcDestination = new RECT(left, top, right, bottom);
    }

    public void Update()
    {
        if (!_windowManager.IsCompositionEnabled || _handle == IntPtr.Zero || _properties == null)
        {
            return;
        }

        try
        {
            DwmNativeMethods.DwmUpdateThumbnailProperties(_handle, _properties!);
        }
        catch (ArgumentException)
        {
        }
        catch (COMException)
        {
        }
    }
}
