using System.Runtime.InteropServices;

namespace DeskBox.Platform;

/// <summary>
/// Native interop for the documented Open With association APIs
/// (SHAssocEnumHandlers / IAssocHandler). These are Shell-owned objects that
/// read association data only; no third-party context menu handler is ever
/// loaded through this path, so the shell-extension process isolation
/// contract is preserved. Vtable order and IIDs are verified against the
/// Windows SDK 10.0.26100 shobjidl_core.h header.
/// </summary>
internal static class ShellOpenWithInterop
{
    internal const uint AssocFilterRecommended = 0x1;

    internal static readonly Guid IID_IDataObject =
        new("0000010e-0000-0000-c000-000000000046");

    private static readonly Guid IID_IShellItem =
        new("43826d1e-e718-42ee-bc55-a1e261c37bfe");

    // IID_IShellItemArray and BHID_DataObject verified against the Windows
    // SDK 10.0.26100 headers (shobjidl_core.h, ShlGuid.h).
    private static readonly Guid IID_IShellItemArray =
        new("b63ea76d-1f85-456f-a19c-48159efa858b");

    private static readonly Guid BHID_DataObject =
        new("b8c0bd9f-ed24-455c-83e6-d5390c4fe8c4");

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    internal static extern void SHAssocEnumHandlers(
        string extension,
        uint filter,
        [MarshalAs(UnmanagedType.Interface)]
        out ShellOpenWithEnumHandlers handlers);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHCreateItemFromParsingName(
        string path,
        IntPtr bindContext,
        ref Guid riid,
        out IntPtr shellItem);

    [DllImport("shell32.dll", PreserveSig = false)]
    private static extern void SHCreateShellItemArrayFromShellItem(
        IntPtr shellItem,
        ref Guid riid,
        out IntPtr shellItemArray);

    [DllImport("ole32.dll")]
    internal static extern void CoTaskMemFree(IntPtr buffer);

    /// <summary>
    /// Builds the Shell data object for one item so it can be handed to
    /// <c>IAssocHandler::Invoke</c>, which accepts an IDataObject. The
    /// documented route is IShellItemArray.BindToHandler(BHID_DataObject);
    /// passing the IShellItem pointer straight to Invoke is an access
    /// violation, and SHCreateShellItemArrayFromShellItem does not accept
    /// IID_IDataObject. Returns the data object pointer; release it with
    /// <see cref="ReleaseDataObject"/> after the invoke returns.
    /// </summary>
    internal static bool TryCreateDataObjectForPath(
        string path,
        out IntPtr dataObject)
    {
        dataObject = IntPtr.Zero;
        Guid shellItemId = IID_IShellItem;
        Guid arrayId = IID_IShellItemArray;
        IntPtr shellItem = IntPtr.Zero;
        IntPtr arrayPointer = IntPtr.Zero;
        try
        {
            SHCreateItemFromParsingName(
                path,
                IntPtr.Zero,
                ref shellItemId,
                out shellItem);
            SHCreateShellItemArrayFromShellItem(
                shellItem,
                ref arrayId,
                out arrayPointer);
            if (Marshal.GetObjectForIUnknown(arrayPointer)
                is not ShellItemArray array)
            {
                return false;
            }

            try
            {
                Guid handlerId = BHID_DataObject;
                Guid dataObjectId = IID_IDataObject;
                array.BindToHandler(
                    IntPtr.Zero,
                    ref handlerId,
                    ref dataObjectId,
                    out dataObject);
                return dataObject != IntPtr.Zero;
            }
            finally
            {
                Marshal.ReleaseComObject(array);
            }
        }
        catch (Exception ex) when (
            ex is COMException or InvalidCastException or
            InvalidOperationException)
        {
            ReleaseDataObject(ref dataObject);
            return false;
        }
        finally
        {
            if (arrayPointer != IntPtr.Zero)
            {
                Marshal.Release(arrayPointer);
            }

            if (shellItem != IntPtr.Zero)
            {
                Marshal.Release(shellItem);
            }
        }
    }

    internal static void ReleaseDataObject(ref IntPtr dataObject)
    {
        if (dataObject != IntPtr.Zero)
        {
            Marshal.Release(dataObject);
            dataObject = IntPtr.Zero;
        }
    }

    private const uint OaifAllowRegistration = 0x00000001;
    private const uint OaifExec = 0x00000004;
    private const int HResultErrorCancelled = unchecked((int)0x800704C5);

    /// <summary>
    /// Shows the system Open With dialog (SHOpenWithDialog — the documented
    /// API for exactly this; the <c>openas</c> verb via ShellExecuteEx is
    /// rejected with access denied from this process, proven by the
    /// open-with repro harness). Modal on the calling STA thread; user
    /// cancellation counts as success.
    /// </summary>
    internal static bool TryShowOpenAsDialog(
        IntPtr ownerWindow,
        string filePath)
    {
        var info = new OpenAsInfo
        {
            File = filePath,
            Class = null,
            Flags = OaifAllowRegistration | OaifExec
        };

        try
        {
            SHOpenWithDialog(ownerWindow, ref info);
            return true;
        }
        catch (COMException ex) when (ex.HResult == HResultErrorCancelled)
        {
            return true;
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode,
        PreserveSig = false)]
    private static extern void SHOpenWithDialog(
        IntPtr parentWindow,
        ref OpenAsInfo info);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OpenAsInfo
    {
        [MarshalAs(UnmanagedType.LPWStr)]
        public string File;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string? Class;

        public uint Flags;
    }

    [ComImport]
    [Guid("973810ae-9599-4b88-9e4d-6ee98c9552da")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ShellOpenWithEnumHandlers
    {
        // IEnumAssocHandlers exposes only Next in the SDK header. The array
        // parameter must opt into LPArray marshaling explicitly: interface
        // method arrays default to SAFEARRAY, and the SafeArray marshaler
        // dies with a CLR internal error on IntPtr[] (crash, not exception —
        // proven 2026-10-16 by the open-with repro).
        void Next(
            uint celt,
            [MarshalAs(UnmanagedType.LPArray), Out] IntPtr[] rgelt,
            out uint pceltFetched);
    }

    [ComImport]
    [Guid("b63ea76d-1f85-456f-a19c-48159efa858b")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ShellItemArray
    {
        // First method after IUnknown per shobjidl_core.h.
        void BindToHandler(
            IntPtr bindContext,
            ref Guid handlerId,
            ref Guid iid,
            out IntPtr bound);
    }

    [ComImport]
    [Guid("F04061AC-1659-4a3f-A954-775AA57FC083")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ShellOpenWithAssocHandler
    {
        // Order per shobjidl_core.h: GetName, GetUIName, GetIconLocation,
        // IsRecommended, MakeDefault, Invoke(IDataObject), CreateInvoker.
        void GetName(out IntPtr displayName);
        void GetUIName(out IntPtr uiDisplayName);
        void GetIconLocation(out IntPtr iconPath, out int iconIndex);
        [PreserveSig] int IsRecommended();
        void MakeDefault(string description);
        void Invoke(IntPtr dataObject);
        void CreateInvoker(
            IntPtr dataObject,
            out IntPtr invoker);
    }
}
