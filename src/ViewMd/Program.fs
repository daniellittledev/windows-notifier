module ViewMd.Program

open System
open System.Diagnostics
open Microsoft.Win32

/// The custom URL scheme this handler owns.
[<Literal>]
let Scheme = "viewmd"

/// The external markdown viewer launched for the document.
[<Literal>]
let Viewer = "typora.exe"

let private usage =
    """viewmd - open a markdown document in Typora from a 'viewmd:' URI.

Usage:
  viewmd "viewmd:<url-encoded-path>"   Open the decoded path in Typora.
  viewmd --register                    Register this exe as the 'viewmd:' handler (HKCU).
  viewmd --unregister                  Remove the 'viewmd:' handler registration.
  viewmd -h | --help                   Show this help.

Example:
  viewmd "viewmd:C%3A%5Cnotes%5Cbuild.md"  ->  typora.exe "C:\notes\build.md"
"""

/// Register this executable under HKCU as the handler for the custom scheme.
let private register () =
    let exePath = Environment.ProcessPath
    use root = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + Scheme)
    root.SetValue("", "URL:" + Scheme + " Protocol")
    root.SetValue("URL Protocol", "")
    use command = root.CreateSubKey(@"shell\open\command")
    command.SetValue("", sprintf "\"%s\" \"%%1\"" exePath)
    printfn "Registered '%s:' -> %s" Scheme exePath

/// Remove the scheme registration.
let private unregister () =
    Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\" + Scheme, false)
    printfn "Unregistered '%s:'" Scheme

/// Strip the scheme prefix, URL-decode the remainder, and open it in the viewer.
let private openUri (uri: string) =
    let prefix = Scheme + ":"
    let encoded =
        if uri.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
        then uri.Substring(prefix.Length)
        else uri
    let path = Uri.UnescapeDataString(encoded)
    let psi = ProcessStartInfo(Viewer, UseShellExecute = true)
    psi.ArgumentList.Add(path)
    Process.Start(psi) |> ignore

[<EntryPoint>]
let main argv =
    match argv with
    | [| "-h" |] | [| "--help" |] ->
        printfn "%s" usage
        0
    | [| "--register" |] ->
        try register (); 0
        with ex -> eprintfn "Registration failed: %s" ex.Message; 1
    | [| "--unregister" |] ->
        try unregister (); 0
        with ex -> eprintfn "Unregister failed: %s" ex.Message; 1
    | [| uri |] ->
        try openUri uri; 0
        with ex -> eprintfn "Failed to open '%s': %s" uri ex.Message; 1
    | _ ->
        eprintfn "Expected a single 'viewmd:' URI.\n\n%s" usage
        2
