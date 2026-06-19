module Register.Program

open System
open System.IO
open Register.Interop

/// MUST match the AUMID the notifier shows toasts under (Notifier.DefaultAumid).
[<Literal>]
let Aumid = "Daniel.CliNotifier"

/// Stub ToastActivatorCLSID: a fixed, random GUID with NO COM server behind it.
/// Its only job is to be present on the shortcut so toasts display branded and
/// persist in Action Center. Because nothing implements it, only protocol-activation
/// toasts work — which is all we ever raise.
[<Literal>]
let StubClsid = "9C7A2E14-3B6D-4F8A-9E1C-5D2A8B4F6C0E"

/// Friendly name shown in the Start Menu / Action Center.
[<Literal>]
let DisplayName = "CLI Notifier"

let private usage =
    """register - one-time setup so toasts appear branded and persist in Action Center.

Creates a Start Menu shortcut carrying:
  System.AppUserModel.ID                 -> Daniel.CliNotifier
  System.AppUserModel.ToastActivatorCLSID -> {9C7A2E14-3B6D-4F8A-9E1C-5D2A8B4F6C0E}

Usage:
  register [--target <path-to-notifier.exe>]

Options:
  --target <path>  Path the shortcut points at. Defaults to 'notifier.exe' next to
                   this executable.
  -h, --help       Show this help."""

/// Build the shortcut and stamp the AUMID + stub CLSID onto it.
let private createShortcut (targetExe: string) (shortcutPath: string) =
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
    persist.Save(shortcutPath, true)

let rec private parseTarget (argv: string list) =
    match argv with
    | [] -> Ok None
    | ("-h" | "--help") :: _ -> Ok(Some "help")
    | "--target" :: v :: _ -> Ok(Some v)
    | "--target" :: [] -> Error "Option '--target' requires a value."
    | unknown :: _ -> Error(sprintf "Unknown argument: %s" unknown)

[<EntryPoint; STAThread>]
let main argv =
    match parseTarget (List.ofArray argv) with
    | Error msg ->
        eprintfn "%s\n\n%s" msg usage
        2
    | Ok (Some "help") ->
        printfn "%s" usage
        0
    | Ok targetOpt ->
        let targetExe =
            match targetOpt with
            | Some path -> Path.GetFullPath(path)
            | None -> Path.Combine(AppContext.BaseDirectory, "notifier.exe")

        if not (File.Exists targetExe) then
            eprintfn "Warning: target executable not found at:\n  %s\nThe shortcut will still be created, but update --target if this path is wrong." targetExe

        let programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs)
        let shortcutPath = Path.Combine(programs, DisplayName + ".lnk")

        try
            createShortcut targetExe shortcutPath
            printfn "Registered."
            printfn "  Shortcut: %s" shortcutPath
            printfn "  Target:   %s" targetExe
            printfn "  AUMID:    %s" Aumid
            printfn "  CLSID:    {%s}" StubClsid
            0
        with ex ->
            eprintfn "Registration failed: %s" ex.Message
            1
