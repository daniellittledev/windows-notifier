namespace Register

open System
open System.Runtime.InteropServices

/// COM interop needed to create a Start Menu shortcut carrying an AUMID and a stub
/// ToastActivatorCLSID. This is the only fiddly part of the whole utility.
///
/// We declare the full vtables of the COM interfaces (in order) even though we only
/// call a handful of methods — the slot order must be exact for the calls we do make.
module Interop =

    /// PROPERTYKEY: identifies a property in an IPropertyStore.
    [<Struct; StructLayout(LayoutKind.Sequential)>]
    type PropertyKey =
        val mutable Fmtid: Guid
        val mutable Pid: uint32
        new(fmtid, pid) = { Fmtid = fmtid; Pid = pid }

    /// Minimal PROPVARIANT. We only ever store pointer-sized payloads (a string or a
    /// CLSID pointer), so the explicit layout just needs the discriminator and the
    /// pointer slot; reserved fields default to zero.
    [<StructLayout(LayoutKind.Explicit, Size = 16)>]
    type PropVariant =
        [<FieldOffset(0)>]
        val mutable Vt: uint16
        [<FieldOffset(8)>]
        val mutable Ptr: IntPtr

    // VARTYPE values used below.
    [<Literal>]
    let VT_LPWSTR = 31us

    [<Literal>]
    let VT_CLSID = 72us

    [<ComImport>]
    [<Guid("000214F9-0000-0000-C000-000000000046")>]
    [<InterfaceType(ComInterfaceType.InterfaceIsIUnknown)>]
    type IShellLinkW =
        abstract GetPath: [<MarshalAs(UnmanagedType.LPWStr)>] pszFile: Text.StringBuilder * cch: int * pfd: IntPtr * fFlags: uint32 -> unit
        abstract GetIDList: ppidl: byref<IntPtr> -> unit
        abstract SetIDList: pidl: IntPtr -> unit
        abstract GetDescription: [<MarshalAs(UnmanagedType.LPWStr)>] pszName: Text.StringBuilder * cch: int -> unit
        abstract SetDescription: [<MarshalAs(UnmanagedType.LPWStr)>] pszName: string -> unit
        abstract GetWorkingDirectory: [<MarshalAs(UnmanagedType.LPWStr)>] pszDir: Text.StringBuilder * cch: int -> unit
        abstract SetWorkingDirectory: [<MarshalAs(UnmanagedType.LPWStr)>] pszDir: string -> unit
        abstract GetArguments: [<MarshalAs(UnmanagedType.LPWStr)>] pszArgs: Text.StringBuilder * cch: int -> unit
        abstract SetArguments: [<MarshalAs(UnmanagedType.LPWStr)>] pszArgs: string -> unit
        abstract GetHotkey: pwHotkey: byref<uint16> -> unit
        abstract SetHotkey: wHotkey: uint16 -> unit
        abstract GetShowCmd: piShowCmd: byref<int> -> unit
        abstract SetShowCmd: iShowCmd: int -> unit
        abstract GetIconLocation: [<MarshalAs(UnmanagedType.LPWStr)>] pszIconPath: Text.StringBuilder * cch: int * piIcon: byref<int> -> unit
        abstract SetIconLocation: [<MarshalAs(UnmanagedType.LPWStr)>] pszIconPath: string * iIcon: int -> unit
        abstract SetRelativePath: [<MarshalAs(UnmanagedType.LPWStr)>] pszPathRel: string * dwReserved: uint32 -> unit
        abstract Resolve: hwnd: IntPtr * fFlags: uint32 -> unit
        abstract SetPath: [<MarshalAs(UnmanagedType.LPWStr)>] pszFile: string -> unit

    [<ComImport>]
    [<Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99")>]
    [<InterfaceType(ComInterfaceType.InterfaceIsIUnknown)>]
    type IPropertyStore =
        abstract GetCount: cProps: byref<uint32> -> unit
        abstract GetAt: iProp: uint32 * pkey: byref<PropertyKey> -> unit
        abstract GetValue: key: byref<PropertyKey> * pv: byref<PropVariant> -> unit
        abstract SetValue: key: byref<PropertyKey> * propvar: byref<PropVariant> -> unit
        abstract Commit: unit -> unit

    [<ComImport>]
    [<Guid("0000010b-0000-0000-C000-000000000046")>]
    [<InterfaceType(ComInterfaceType.InterfaceIsIUnknown)>]
    type IPersistFile =
        abstract GetClassID: pClassID: byref<Guid> -> unit
        [<PreserveSig>]
        abstract IsDirty: unit -> int
        abstract Load: [<MarshalAs(UnmanagedType.LPWStr)>] pszFileName: string * dwMode: uint32 -> unit
        abstract Save: [<MarshalAs(UnmanagedType.LPWStr)>] pszFileName: string * fRemember: bool -> unit
        abstract SaveCompleted: [<MarshalAs(UnmanagedType.LPWStr)>] pszFileName: string -> unit
        abstract GetCurFile: [<MarshalAs(UnmanagedType.LPWStr)>] ppszFileName: byref<string> -> unit

    /// CLSID_ShellLink — the COM object that implements IShellLinkW.
    let ClsidShellLink = Guid("00021401-0000-0000-C000-000000000046")

    // The shell property keys, both in the AppUserModel format ID.
    let private appUserModelFmtid = Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3")

    /// PKEY_AppUserModel_ID
    let PkeyAumid = PropertyKey(appUserModelFmtid, 5u)

    /// PKEY_AppUserModel_ToastActivatorCLSID
    let PkeyToastActivatorClsid = PropertyKey(appUserModelFmtid, 26u)

    /// Frees the resources held by a PROPVARIANT (the LPWSTR or the CLSID block).
    [<DllImport("ole32.dll")>]
    extern int PropVariantClear(PropVariant& pvar)

    /// Create a VT_LPWSTR PROPVARIANT. Free it with PropVariantClear.
    let propVariantFromString (value: string) =
        let mutable pv = PropVariant()
        pv.Vt <- VT_LPWSTR
        pv.Ptr <- Marshal.StringToCoTaskMemUni(value)
        pv

    /// Create a VT_CLSID PROPVARIANT. Free it with PropVariantClear.
    let propVariantFromClsid (value: Guid) =
        let mutable pv = PropVariant()
        let block = Marshal.AllocCoTaskMem(16)
        Marshal.StructureToPtr<Guid>(value, block, false)
        pv.Vt <- VT_CLSID
        pv.Ptr <- block
        pv
