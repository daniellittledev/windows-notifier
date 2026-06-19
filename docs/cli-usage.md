# Calling the notifier from a CLI

`notifier.exe` is fire-and-forget: a CLI tool or script shells out to it with a
few flags, a toast appears, and the process exits. Nothing stays running, and
there is no callback into your script — clicks are handled by the shell via
[protocol activation](../README.md#how-it-works-protocol-activation-only).

## Prerequisites

1. **Register once.** Run `register.exe` a single time per machine so the toast's
   AUMID/Start-Menu shortcut exists. Without it the toast silently fails to
   appear. See the [README](../README.md#1-register-once-branding--action-center-persistence).
2. **Make `notifier.exe` callable.** Put it on `PATH`, or call it by full path
   (e.g. `C:\tools\notifier\notifier.exe`).

## The contract

```
notifier --title <text> [--message <text>] [--action <uri>] [--button "<label>=<uri>"]... [--aumid <id>]
```

| Flag | Required | Meaning |
|------|----------|---------|
| `--title <text>` | yes | First line of the toast (bold). |
| `--message <text>` | no | Second line of the toast. |
| `--action <uri>` | no | URI launched when the toast **body** is clicked. |
| `--button "<label>=<uri>"` | no, repeatable | Adds a button that launches `<uri>`. Split on the **first** `=`, so the URI may itself contain `=`. |
| `--aumid <id>` | no | Override the App User Model ID (default `Daniel.CliNotifier`); must match what `register.exe` stamped. |
| `-h`, `--help` | — | Print usage. |

### Exit codes

| Code | Meaning |
|------|---------|
| `0` | Toast shown. |
| `1` | Failed to show the toast (e.g. WinRT error). |
| `2` | Bad arguments (missing `--title`, unknown flag, missing value). |

Scripts can branch on these — a non-zero exit means the notification did not go out.

## Click targets

- **App deep links** (`obsidian://…`, `vscode://…`) need no handler — the shell
  launches them directly. Use them verbatim as `--action` / button URIs.
- **Markdown documents** go through the custom `viewmd:` scheme, which opens the
  file in Typora. The path **must be URL-encoded** in the URI; the handler
  decodes it. Example: `C:\notes\x.md` → `viewmd:C%3A%5Cnotes%5Cx.md`.
- Do **not** point a toast at a `file:` URI — `file:` activation from toasts is
  unreliable/blocked.

## Examples

### cmd.exe / `.bat`

```bat
notifier.exe --title "Build finished" --message "All tests green" ^
  --action "obsidian://open?path=Vault/Build.md"
```

### PowerShell

```powershell
notifier.exe --title "Deploy done" --message "prod is live" `
  --button "Open logs=vscode://file/C:/logs/deploy.txt" `
  --button "View notes=viewmd:C%3A%5Cnotes%5Cdeploy.md"
```

URL-encode a dynamic path before embedding it in a `viewmd:` URI:

```powershell
$path = "C:\out\report.md"
$uri  = "viewmd:" + [uri]::EscapeDataString($path)
notifier.exe --title "Render complete" --action $uri
```

### Git Bash / WSL (calling the Windows exe)

```bash
notifier.exe --title "Job done" --message "exit 0"
```

## Wrapping another command

### Bash — notify on success or failure

```bash
if make build; then
  notifier.exe --title "build ✓" --message "$(date)"
else
  notifier.exe --title "build ✗" --message "see log" \
    --action "vscode://file/$PWD/build.log"
fi
```

### PowerShell — a small helper

```powershell
function Notify($title, $msg) { notifier.exe --title $title --message $msg }

$sw = [Diagnostics.Stopwatch]::StartNew()
Invoke-LongTask
Notify "task done" "finished in $($sw.Elapsed)"
```

## Calling from a program

Any language that can spawn a process can use it. Pass each flag and value as a
separate argument (no manual quoting needed when you use an argument list).

### C# / .NET

```csharp
using System.Diagnostics;

Process.Start("notifier.exe", new[]
{
    "--title",  "Render complete",
    "--action", "viewmd:" + Uri.EscapeDataString(@"C:\out\report.md"),
});
```

### Node.js

```js
const { execFile } = require("node:child_process");

execFile("notifier.exe", [
  "--title", "Tests passed",
  "--message", "1234 ok",
  "--button", `Open report=viewmd:${encodeURIComponent("C:\\out\\report.md")}`,
]);
```

### Python

```python
import subprocess, urllib.parse

path = r"C:\out\report.md"
uri  = "viewmd:" + urllib.parse.quote(path, safe="")
subprocess.run(["notifier.exe", "--title", "Done", "--action", uri], check=False)
```

## Common pitfalls

- **No toast appears** → AUMID mismatch. The `--aumid` (default `Daniel.CliNotifier`)
  must equal the AUMID `register.exe` put on the shortcut, and registration must
  have run.
- **`viewmd:` opens the wrong/empty document** → the path wasn't URL-encoded, or
  Typora isn't installed / on `PATH`.
- **Button does nothing** → its `arguments` URI isn't a valid protocol the shell
  can launch. Test the raw URI by pasting it into the Run dialog (Win+R).
- **`notifier.exe` not found** → it isn't on `PATH`; call it by full path.
