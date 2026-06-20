# Registering the notifier

There are two one-time registrations. Both write only to your user profile
(`HKCU` and your Start Menu) — **no administrator rights required**. You only do
these once per machine (or after moving the executables).

| Step | Command | When you need it |
|------|---------|------------------|
| 1. Notifier | `register.exe` | Always — without it toasts silently never appear. |
| 2. `viewmd:` handler | `viewmd.exe --register` | Only if a toast opens a `.md` in Typora. |

## Before you start

You need the built executables (`notifier.exe`, `register.exe`, `viewmd.exe`),
either from a [release](../README.md#releases) zip or from a local
[build](../README.md#build). Put them somewhere stable, e.g. `C:\tools\notifier\`
— the registration records that path, so re-register if you move them.

The [install script](../README.md#install) downloads the executables and adds
them to your PATH for you, but it deliberately does **not** register — that stays
the explicit step below (unless you opt in with the installer's `-Register` flag).

## 1. Register the notifier

This creates a Start Menu shortcut stamped with the AUMID and a stub
`ToastActivatorCLSID`, which is what makes Windows show the toast branded and keep
it in Action Center.

```powershell
register.exe --target C:\tools\notifier\notifier.exe
```

- `--target <path>` is the full path to the `notifier.exe` that will raise toasts.
- Omit `--target` to default to `notifier.exe` **next to `register.exe`** — so if
  you keep both in the same folder, just run `register.exe`.

On success it prints the shortcut path, target, AUMID, and CLSID:

```
Registered.
  Shortcut: C:\Users\<you>\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\CLI Notifier.lnk
  Target:   C:\tools\notifier\notifier.exe
  AUMID:    Daniel.CliNotifier
  CLSID:    {9C7A2E14-3B6D-4F8A-9E1C-5D2A8B4F6C0E}
```

> **The one invariant that matters:** the AUMID on this shortcut
> (`Daniel.CliNotifier`) must equal the AUMID `notifier.exe` shows toasts under.
> They share the same default, so this just works — unless you pass a custom
> `--aumid` to `notifier.exe`, in which case you must register with a matching
> AUMID.

### Undo / re-register

Delete the shortcut to undo:

```powershell
Remove-Item "$env:APPDATA\Microsoft\Windows\Start Menu\Programs\CLI Notifier.lnk"
```

Re-running `register.exe` simply overwrites the shortcut — safe to run again any
time (for example after moving `notifier.exe`).

## 2. Register the `viewmd:` handler (optional)

Only needed if a toast launches a `viewmd:` URI to open a markdown document in
Typora. Register the scheme once:

```powershell
viewmd.exe --register
```

This writes `HKCU\Software\Classes\viewmd` pointing at the current `viewmd.exe`
(re-run it if you move the file). To remove it:

```powershell
viewmd.exe --unregister
```

Skip this entirely if your toasts only use third-party deep links like
`obsidian://` or `vscode://` — those need no handler.

## Verify

```powershell
notifier.exe --title "Hello" --message "registration works"
```

A toast should appear and then persist in Action Center. To check the `viewmd:`
handler independently, paste a URI into the Run dialog (<kbd>Win</kbd>+<kbd>R</kbd>):

```
viewmd:C%3A%5Cnotes%5Cx.md
```

## Troubleshooting

| Symptom | Likely cause |
|---------|--------------|
| No toast appears at all | Step 1 wasn't run, or `notifier.exe` uses a different `--aumid` than was registered. |
| Toast appears but doesn't persist in Action Center | The shortcut is missing its `ToastActivatorCLSID` — re-run `register.exe`. |
| `viewmd:` link does nothing / opens nothing | Step 2 wasn't run, the path wasn't URL-encoded, or Typora isn't installed / on `PATH`. |
| Registration points at the wrong exe | You moved the binaries — re-run the relevant register command. |

For invoking the notifier from scripts and programs, see
[cli-usage.md](cli-usage.md).
