namespace Notifier

/// Pure construction of the toast XML payload.
///
/// Every activation is *protocol* activation: the body `launch` and each button's
/// `arguments` is a Windows URI that the shell launches on click. This is the only
/// activation type that works behind a stub ToastActivatorCLSID (see the Register tool),
/// and it works even when this exe is no longer running.
module Toast =

    /// A button on the toast. Both the label and the protocol URI it launches.
    type Button = { Content: string; Arguments: string }

    /// Everything needed to render one toast.
    type Toast =
        { /// First line of text (bold).
          Title: string
          /// Optional second line of text.
          Message: string option
          /// Optional URI launched when the toast body is clicked.
          Launch: string option
          /// Zero or more action buttons, each launching a URI.
          Buttons: Button list }

    /// Escape a value for safe inclusion in an XML attribute or text node.
    let private xmlEscape (s: string) =
        s.Replace("&", "&amp;")
         .Replace("<", "&lt;")
         .Replace(">", "&gt;")
         .Replace("\"", "&quot;")
         .Replace("'", "&apos;")

    /// Build the toast XML from the given content. Pure function over the inputs —
    /// no side effects, so it is trivially testable.
    let build (toast: Toast) : string =
        let launchAttr =
            match toast.Launch with
            | Some uri -> sprintf " launch=\"%s\" activationType=\"protocol\"" (xmlEscape uri)
            | None -> ""

        let textNodes =
            [ yield sprintf "        <text>%s</text>" (xmlEscape toast.Title)
              match toast.Message with
              | Some m -> yield sprintf "        <text>%s</text>" (xmlEscape m)
              | None -> () ]
            |> String.concat "\n"

        let actionsBlock =
            match toast.Buttons with
            | [] -> ""
            | buttons ->
                let actions =
                    buttons
                    |> List.map (fun b ->
                        sprintf "    <action content=\"%s\" activationType=\"protocol\" arguments=\"%s\" />"
                            (xmlEscape b.Content) (xmlEscape b.Arguments))
                    |> String.concat "\n"
                sprintf "\n  <actions>\n%s\n  </actions>" actions

        sprintf "<toast%s>\n  <visual>\n    <binding template=\"ToastGeneric\">\n%s\n    </binding>\n  </visual>%s\n</toast>"
            launchAttr textNodes actionsBlock
