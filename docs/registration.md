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

This makes Windows recognise the AUMID `Daniel.CliNotifier` so toasts show branded
and persist in Action Center. There are two methods; `register.exe` uses the
**registry** method by default:

```powershell
register.exe --target C:\tools\notifier\notifier.exe
```

- `--target <path>` is the full path to the `notifier.exe` that will raise toasts.
- Omit `--target` to default to `notifier.exe` **next to `register.exe`** — so if
  you keep both in the same folder, just run `register.exe`.

### Registry method (default)

Writes the AUMID registration under
`HKCU\Software\Classes\AppUserModelId\Daniel.CliNotifier` (a `DisplayName` and a
`CustomActivator` pointing at the stub CLSID) plus the activator's
`CLSID\…\LocalServer32`. Windows resolves the AUMID immediately, without depending
on the shell indexing a Start Menu shortcut. Output:

```
Registered (registry method).
  AppUserModelId: HKCU\Software\Classes\AppUserModelId\Daniel.CliNotifier
  Activator:      HKCU\Software\Classes\CLSID\{9C7A2E14-3B6D-4F8A-9E1C-5D2A8B4F6C0E}\LocalServer32
  Target:         C:\tools\notifier\notifier.exe
  AUMID:          Daniel.CliNotifier
```

### Shortcut method (`--shortcut`)

The classic Win32 approach: a Start Menu shortcut stamped with the AUMID and the
stub `ToastActivatorCLSID`. Use it if you want a Start Menu entry or prefer not to
write the registry registration:

```powershell
register.exe --target C:\tools\notifier\notifier.exe --shortcut
```

> The shortcut method depends on the shell indexing the new shortcut, which can be
> delayed or — on some Windows builds — not happen at all. If toasts silently never
> appear with `--shortcut`, use the default registry method instead.

> **The one invariant that matters:** the registered AUMID (`Daniel.CliNotifier`)
> must equal the AUMID `notifier.exe` shows toasts under. They share the same
> default, so this just works — unless you pass a custom `--aumid` to `notifier.exe`,
> in which case you must register with a matching AUMID.

### Unregister / re-register

Remove everything either method created (the shortcut **and** the registry keys):

```powershell
register.exe --unregister
```

Re-running `register.exe` is safe any time — it overwrites the existing
registration (for example after moving `notifier.exe`).

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
| No toast appears at all | Step 1 wasn't run, or `notifier.exe` uses a different `--aumid` than was registered. If you used `--shortcut`, the shell may not have indexed it — re-run with the default registry method. |
| Toast appears but doesn't persist in Action Center | The registration is missing its activator CLSID — re-run `register.exe`. |
| `viewmd:` link does nothing / opens nothing | Step 2 wasn't run, the path wasn't URL-encoded, or Typora isn't installed / on `PATH`. |
| Registration points at the wrong exe | You moved the binaries — re-run the relevant register command. |

For invoking the notifier from scripts and programs, see
[cli-usage.md](cli-usage.md).
