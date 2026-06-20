module Notifier.Program

open System
open System.Runtime.InteropServices
open Notifier.Toast
open Windows.Data.Xml.Dom
open Windows.UI.Notifications

/// Declare this process's AUMID so Windows ties our toasts to the Start Menu
/// shortcut the Register tool stamped with the same AUMID. Without this call an
/// unpackaged Win32 process has a synthesized per-process AUMID that matches no
/// shortcut, and Windows silently drops every toast — even though CreateToastNotifier
/// is handed the AUMID explicitly. PreserveSig=false turns a failing HRESULT into
/// an exception. (shell32: SetCurrentProcessExplicitAppUserModelID)
[<DllImport("shell32.dll", PreserveSig = false)>]
extern void SetCurrentProcessExplicitAppUserModelID([<MarshalAs(UnmanagedType.LPWStr)>] string appId)

/// The AUMID this notifier shows toasts under. It MUST match the AUMID set on the
/// Start Menu shortcut by the Register tool, or Windows shows nothing.
[<Literal>]
let DefaultAumid = "Daniel.CliNotifier"

let private usage =
    """notifier - raise a native Windows toast that launches a URI on click.

Usage:
  notifier --title <text> [--message <text>] [--action <uri>] [--button "<label>=<uri>"]...

Options:
  --title   <text>          Required. First line of the toast.
  --message <text>          Optional. Second line of the toast.
  --action  <uri>           Optional. URI launched when the toast body is clicked
                            (e.g. "obsidian://open?path=...", "viewmd:C%3A%5Cnotes%5Cx.md").
  --button  "<label>=<uri>" Optional, repeatable. Adds an action button that launches
                            <uri>. Split on the FIRST '=' so URIs may contain '='.
  --aumid   <id>            Optional. Override the App User Model ID (default: Daniel.CliNotifier).
  -h, --help                Show this help.

All clicks use protocol activation, so the target URI is launched by the shell even
after this process has exited."""

/// Parsed command-line options.
type private Args =
    { Title: string option
      Message: string option
      Action: string option
      Buttons: Button list
      Aumid: string
      Help: bool }

let private empty =
    { Title = None; Message = None; Action = None; Buttons = []; Aumid = DefaultAumid; Help = false }

/// Split "Label=uri" on the first '=' only, so the URI may itself contain '='.
let private parseButton (spec: string) : Button =
    match spec.IndexOf('=') with
    | -1 -> { Content = spec; Arguments = "" }
    | i -> { Content = spec.Substring(0, i); Arguments = spec.Substring(i + 1) }

let rec private parse (acc: Args) (argv: string list) : Result<Args, string> =
    match argv with
    | [] -> Ok acc
    | ("-h" | "--help") :: _ -> Ok { acc with Help = true }
    | "--title" :: v :: rest -> parse { acc with Title = Some v } rest
    | "--message" :: v :: rest -> parse { acc with Message = Some v } rest
    | "--action" :: v :: rest -> parse { acc with Action = Some v } rest
    | "--aumid" :: v :: rest -> parse { acc with Aumid = v } rest
    | "--button" :: v :: rest -> parse { acc with Buttons = acc.Buttons @ [ parseButton v ] } rest
    | (("--title" | "--message" | "--action" | "--aumid" | "--button") as opt) :: [] ->
        Error (sprintf "Option '%s' requires a value." opt)
    | unknown :: _ -> Error (sprintf "Unknown argument: %s" unknown)

/// Show a toast under the given AUMID. The only side effect in the program.
let private show (aumid: string) (toast: Toast) =
    // Declare our AUMID before creating the notifier, so Windows resolves the toast
    // against the registered Start Menu shortcut instead of silently dropping it.
    SetCurrentProcessExplicitAppUserModelID(aumid)
    let xml = XmlDocument()
    xml.LoadXml(Toast.build toast)
    let notifier = ToastNotificationManager.CreateToastNotifier(aumid)
    notifier.Show(ToastNotification(xml))

[<EntryPoint>]
let main argv =
    match parse empty (List.ofArray argv) with
    | Error msg ->
        eprintfn "%s\n\n%s" msg usage
        2
    | Ok args when args.Help ->
        printfn "%s" usage
        0
    | Ok args ->
        match args.Title with
        | None ->
            eprintfn "Error: --title is required.\n\n%s" usage
            2
        | Some title ->
            let toast =
                { Title = title
                  Message = args.Message
                  Launch = args.Action
                  Buttons = args.Buttons }
            try
                show args.Aumid toast
                0
            with ex ->
                eprintfn "Failed to show toast: %s" ex.Message
                1
