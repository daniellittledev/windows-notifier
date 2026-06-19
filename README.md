# Windows CLI Notifier

A small Windows utility so CLI tools and scripts can raise native Windows toast
notifications, where clicking a notification (or one of its buttons) launches a
**Windows URI** — either a third-party app deep link (`obsidian://`, `vscode://`, …)
or a custom scheme we own (`viewmd:`) that opens a markdown document in Typora.

Fire-and-forget: the notifier raises a toast and exits. No tray app, no service,
no long-running process.

## How it works: protocol activation only

Windows resolves a toast click through an *activation type*. We use **protocol
activation** for everything: the shell launches a URI on click, and it works even
when our exe is no longer running.

For an unpackaged Win32 app, displaying branded toasts that persist in Action
Center requires a Start Menu shortcut carrying an AUMID and a
`ToastActivatorCLSID`. We use a **stub CLSID** (a fixed random GUID with no COM
server behind it). The consequence: only protocol-activation toasts work — no
foreground/background (in-process) activation, and no text-box inputs. We never
need either; buttons still work.

## Components

Three small, independent executables (F# / .NET 8).

| Project    | Output exe   | Role |
|------------|--------------|------|
| `Notifier` | `notifier.exe` | `args → toast`. Builds protocol-activation toast XML and shows it. |
| `Register` | `register.exe` | One-time setup. Creates the Start Menu shortcut with AUMID + stub CLSID. |
| `ViewMd`   | `viewmd.exe`   | Custom `viewmd:` scheme handler. URL-decodes a path and opens it in Typora. |

The AUMID is shared by contract (it is **not** linked between projects):

- `Register.Program.Aumid` and `Notifier.Program.DefaultAumid` must be identical.
- Both are `Daniel.CliNotifier`.

If they ever diverge, no toast appears — that is the single most common failure.

## Releases

Pushing a version tag builds the three tools as self-contained, single-file
`win-x64` executables (no .NET install required on the target machine) and
publishes them as a GitHub Release:

```powershell
git tag v1.0.0
git push origin v1.0.0
```

The `.github/workflows/release.yml` workflow zips `notifier.exe`,
`register.exe`, `viewmd.exe`, the README, and the LICENSE into
`windows-notifier-v1.0.0-win-x64.zip` and attaches it to the release with
auto-generated notes. Tags containing a hyphen (e.g. `v1.0.0-rc1`) are marked as
pre-releases.

It can also be run manually from the Actions tab (**release → Run workflow**),
entering the tag to create — useful for testing the pipeline. On a manual run the
tag is created at the selected ref if it doesn't already exist.

## Build

```powershell
dotnet build WindowsNotifier.sln -c Release
```

Requires the .NET 8 SDK on Windows. `Notifier` targets
`net8.0-windows10.0.19041.0` so the WinRT toast APIs are available directly via
CsWinRT projection — **no third-party notification library** (no SnoreToast,
BurntToast, or CommunityToolkit).

## Usage

### 1. Register once (branding + Action Center persistence)

```powershell
register.exe --target C:\tools\notifier\notifier.exe
```

`--target` defaults to `notifier.exe` next to `register.exe`.

### 2. Raise a toast

App deep link (no handler needed — the shell launches it):

```powershell
notifier.exe --title "Build finished" --message "All tests green" `
             --action "obsidian://open?path=Vault/Build.md"
```

With buttons (each `--button` is `Label=uri`, split on the first `=`):

```powershell
notifier.exe --title "Build finished" --message "All tests green" `
             --button "Open in app=obsidian://open?path=Vault/Build.md" `
             --button "View notes=viewmd:C%3A%5Cnotes%5Cbuild.md"
```

### 3. Markdown documents → Typora

Register the custom scheme once, then any `viewmd:` URI opens in Typora:

```powershell
viewmd.exe --register
```

`viewmd:<url-encoded-path>` strips the scheme, URL-decodes the path, and runs
`typora.exe "<path>"`. Testable on its own — paste `viewmd:C%3A%5Cnotes%5Cx.md`
into the Run dialog.

> Point toasts at `viewmd:`, **not** `file:` — `file:` activation from toasts is
> unreliable/blocked.

## Gotchas

- AUMID on the shortcut must equal the AUMID passed to `CreateToastNotifier`.
- The stub CLSID means no toast text-box inputs and no in-process activation.
  Buttons (actions) still work.
- URL-encode any path/argument embedded in a custom-scheme URI; the handler decodes it.
- Toasts render text + images only. "Showing a markdown document" always means
  launching an external viewer.

## Out of scope

Reply/text-box inputs, click callbacks routed into the notifier process, a tray
icon / persistent process / history UI, and in-app markdown rendering.
