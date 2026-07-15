namespace WinecloudsStudio.Modules.WindowManager.Services.Interface;

/// <summary>
/// DWM thumbnail wrapper interface. Ported from EVE-O-Preview.
/// </summary>
public interface IDwmThumbnail
{
    void Register(IntPtr destination, IntPtr source);
    void Unregister();
    void Move(int left, int top, int right, int bottom);
    void Update();
}
