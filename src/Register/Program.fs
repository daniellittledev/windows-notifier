module Register.Program

open System
open System.IO
open Microsoft.Win32
open Register.Interop

/// MUST match the AUMID the notifier shows toasts under (Notifier.DefaultAumid).
[<Literal>]
let Aumid = "Daniel.CliNotifier"

/// Stub ToastActivatorCLSID: a fixed, random GUID. Its job is to be present on the
/// registration so toasts display branded and persist in Action Center. We only ever
/// raise protocol-activation toasts, so nothing needs to implement COM activation behind
/// it (the registry method still registers a LocalServer32 for it, but clicks activate
/// via protocol, not COM).
[<Literal>]
let StubClsid = "9C7A2E14-3B6D-4F8A-9E1C-5D2A8B4F6C0E"

/// Friendly name shown in the Start Menu / Action Center.
[<Literal>]
let DisplayName = "CLI Notifier"

let private usage =
    """register - one-time setup so toasts appear branded and persist in Action Center.

Registers the AUMID 'Daniel.CliNotifier' (ToastActivatorCLSID
{9C7A2E14-3B6D-4F8A-9E1C-5D2A8B4F6C0E}) by one of two methods:

  registry (default)    Writes HKCU\Software\Classes\AppUserModelId\Daniel.CliNotifier
                        (DisplayName + CustomActivator) plus the activator's
                        CLSID\...\LocalServer32. Windows resolves the AUMID immediately,
                        without depending on the shell indexing a Start Menu shortcut.
  shortcut (--shortcut) Creates a Start Menu shortcut stamped with the AUMID and the
                        ToastActivatorCLSID (the classic Win32 approach).

Usage:
  register [--target <path-to-notifier.exe>] [--shortcut]
  register --unregister

Options:
  --target <path>  The notifier.exe the registration points at. Defaults to
                   'notifier.exe' next to this executable.
  --shortcut       Use the Start Menu shortcut method instead of the registry method.
  --unregister     Remove everything this tool registers (shortcut and registry keys).
  -h, --help       Show this help."""

// --- paths -------------------------------------------------------------------

let private shortcutPath () =
    let programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs)
    Path.Combine(programs, DisplayName + ".lnk")

let private resolveTarget (targetOpt: string option) =
    match targetOpt with
    | Some path -> Path.GetFullPath(path)
    | None -> Path.Combine(AppContext.BaseDirectory, "notifier.exe")

// --- shortcut method ---------------------------------------------------------

/// Build a Start Menu shortcut and stamp the AUMID + stub CLSID onto it.
let private createShortcut (targetExe: string) (path: string) =
    let link = Activator.CreateInstance(Type.GetTypeFromCLSID(ClsidShellLink)) :?> IShellLinkW
    link.SetPath(targetExe)
    link.SetArguments("")
    link.SetWorkingDirectory(Path.GetDirectoryName(targetExe))
    link.SetDescription(DisplayName)

    let store = link :?> IPropertyStore

    // AUMID
    let mutable pkAumid = PkeyAumid
    let mutable pvAumid = propVariantFromString Aumid
    store.SetValue(&pkAumid, &pvAumid)
    PropVariantClear(&pvAumid) |> ignore

    // Stub ToastActivatorCLSID
    let mutable pkClsid = PkeyToastActivatorClsid
    let mutable pvClsid = propVariantFromClsid (Guid(StubClsid))
    store.SetValue(&pkClsid, &pvClsid)
    PropVariantClear(&pvClsid) |> ignore

    store.Commit()

    let persist = link :?> IPersistFile
    persist.Save(path, true)

// --- registry method ---------------------------------------------------------

/// Register the AUMID under HKCU so Windows resolves our toasts to this app directly,
/// without depending on a Start Menu shortcut being indexed by the shell (which is
/// unreliable and delayed). Mirrors the Windows App SDK's RegisterAumidAndComServer:
/// the AppUserModelId key (DisplayName + CustomActivator) makes the AUMID resolvable,
/// and the CLSID's LocalServer32 backs that activator.
let private registerAppUserModelId (targetExe: string) =
    use aumidKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\AppUserModelId\" + Aumid)
    aumidKey.SetValue("DisplayName", DisplayName, RegistryValueKind.String)
    aumidKey.SetValue("CustomActivator", sprintf "{%s}" StubClsid, RegistryValueKind.String)

    use serverKey =
        Registry.CurrentUser.CreateSubKey(sprintf @"Software\Classes\CLSID\{%s}\LocalServer32" StubClsid)
    serverKey.SetValue("", targetExe, RegistryValueKind.String)

// --- unregister --------------------------------------------------------------

/// Remove everything either method may have created. Safe to run when nothing is
/// registered — missing entries are ignored.
let private unregister (path: string) =
    if File.Exists path then File.Delete path
    Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\AppUserModelId\" + Aumid, false)
    Registry.CurrentUser.DeleteSubKeyTree(sprintf @"Software\Classes\CLSID\{%s}" StubClsid, false)

// --- args --------------------------------------------------------------------

type private Options =
    { Target: string option
      UseShortcut: bool
      Unregister: bool
      Help: bool }

let private defaults =
    { Target = None; UseShortcut = false; Unregister = false; Help = false }

let rec private parse (acc: Options) (argv: string list) : Result<Options, string> =
    match argv with
    | [] -> Ok acc
    | ("-h" | "--help") :: _ -> Ok { acc with Help = true }
    | "--target" :: v :: rest -> parse { acc with Target = Some v } rest
    | "--target" :: [] -> Error "Option '--target' requires a value."
    | "--shortcut" :: rest -> parse { acc with UseShortcut = true } rest
    | "--unregister" :: rest -> parse { acc with Unregister = true } rest
    | unknown :: _ -> Error(sprintf "Unknown argument: %s" unknown)

[<EntryPoint; STAThread>]
let main argv =
    match parse defaults (List.ofArray argv) with
    | Error msg ->
        eprintfn "%s\n\n%s" msg usage
        2
    | Ok o when o.Help ->
        printfn "%s" usage
        0
    | Ok o when o.Unregister ->
        try
            let path = shortcutPath ()
            unregister path
            printfn "Unregistered '%s'." Aumid
            printfn "  Removed shortcut (if present): %s" path
            printfn "  Removed registry keys:         AppUserModelId\\%s, CLSID\\{%s}" Aumid StubClsid
            0
        with ex ->
            eprintfn "Unregister failed: %s" ex.Message
            1
    | Ok o ->
        let targetExe = resolveTarget o.Target

        if not (File.Exists targetExe) then
            eprintfn "Warning: target executable not found at:\n  %s\nThe registration will still be written; pass --target if this path is wrong." targetExe

        try
            if o.UseShortcut then
                createShortcut targetExe (shortcutPath ())
                printfn "Registered (shortcut method)."
                printfn "  Shortcut: %s" (shortcutPath ())
                printfn "  Target:   %s" targetExe
                printfn "  AUMID:    %s" Aumid
                printfn "  CLSID:    {%s}" StubClsid
            else
                registerAppUserModelId targetExe
                printfn "Registered (registry method)."
                printfn "  AppUserModelId: HKCU\\Software\\Classes\\AppUserModelId\\%s" Aumid
                printfn "  Activator:      HKCU\\Software\\Classes\\CLSID\\{%s}\\LocalServer32" StubClsid
                printfn "  Target:         %s" targetExe
                printfn "  AUMID:          %s" Aumid

            0
        with ex ->
            eprintfn "Registration failed: %s" ex.Message
            1
