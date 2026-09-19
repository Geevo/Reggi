# RegTerm

A terminal registry editor for Windows, in the spirit of `regedit.exe`.
Ships as a single ~3 MB native binary with no .NET runtime install required.

```
▎ HKEY_CURRENT_USER\Software
┌ Keys ● ───────────────────────┐┌ Values (3) ──────────────────────────────────┐
│▾ HKEY_CURRENT_USER            ││Name              Type          Data          │
│  ▸ AppEvents                  ││(Default)         REG_SZ        (value not set)│
│  ▸ Control Panel              ││Theme             REG_SZ        dark          │
│    Environment                ││Size              REG_DWORD     0x0000000a (10)│
└───────────────────────────────┘└──────────────────────────────────────────────┘
 F1 Help │ ^F Find │ F3 Next │ F2 Rename │ F5 Reload │ F6 New Value │ F7 New Key │ Del Delete │ ^Q Quit
```

## Build and run

```bash
dotnet run --project RegTerm
```

## Publish a native binary

NativeAOT is enabled in the project file, so a plain publish produces the single
native executable:

```bash
dotnet publish RegTerm -c Release -r win-x64
```

The result is `RegTerm/bin/Release/net10.0-windows/win-x64/publish/regterm.exe`,
roughly 3 MB, with no runtime dependency.

AOT invokes the MSVC linker. If the publish fails with
`'vswhere.exe' is not recognized`, run it from a **Developer PowerShell for
Visual Studio**, or put the installer directory on `PATH` first:

```bash
$env:PATH = "C:\Program Files (x86)\Microsoft Visual Studio\Installer;$env:PATH"
```

To build without AOT (much faster for iteration), `dotnet build` is unaffected —
the AOT settings only apply on publish.

## Keys

| Key | Action |
| --- | --- |
| `Tab` | Switch between the Keys and Values panes |
| `Up` / `Down` | Move through the current pane |
| `Enter` | Keys pane: expand or step in.  Values pane: edit the value |
| `Right` | Expand key, or step into its first child |
| `Left` | Collapse key, or jump to its parent |
| `Space` | Toggle expand and collapse |
| `Backspace` | Go up one key |
| `A`–`Z` | Type-ahead jump in the Keys pane |
| `Ctrl+F` | Find keys, value names or data |
| `F3` | Find the next match |
| `F2` | Rename the selected key or value |
| `F5` | Reload the current key |
| `F6` | New value |
| `F7` | New key |
| `Del` (or `F8`) | Delete the selected key or value |
| `Ctrl+Q` | Quit |

`F8` is kept as an alias for `Del` because the Delete key is not delivered
reliably over every SSH and serial setup.

Writing to `HKEY_LOCAL_MACHINE` and most of `HKEY_CLASSES_ROOT` needs an
elevated terminal. Without elevation those keys are still browsable; failed
writes report the underlying error rather than failing silently.

## Architecture

```
RegTerm.Core/       Platform-neutral contracts, models and application logic.
  RegistryPath        Hive + subkey path
  RegistryValueItem   One value as read from disk
  RegistryValueCodec  The only place that formats and parses value data,
                      including the DWORD/QWORD number base
  KeyTree             Expand/collapse state, flattened into visible rows
  RegistrySearch      Resumable, cancellable depth-first search cursor
  SearchQuery         What to match, and where

RegTerm.Registry/   Windows Registry adapter. This is the only project that
                    references Microsoft.Win32.
  RegistryService     Microsoft.Win32 access; reads degrade, writes throw
  NativeRegistry      The single P/Invoke: RegRenameKey, for in-place key rename

RegTerm.UI/         Terminal.Gui presentation. It depends only on RegTerm.Core,
                    never on the Windows Registry adapter.
  Theme               Colour palette, applied after Application.Init
  KeyTreeSource       Renders the tree with indentation and chevrons
  ValueListSource     Renders aligned Name/Type/Data columns
  PathBarView         Current path across the top
  ValueHeaderView     Column headings
  Dialogs             Edit / New Key / New Value / Help
  SearchDialogs       Find form and the cancellable progress view
  NumberBaseSelector  Hex/Decimal radio that rewrites its bound field
  ScrollBars          Vertical scroll indicator wiring for a list pane
  Draw                Clipping and column-width helpers
  RegistryApplication Composes the views and routes input through IRegistryService

RegTerm/            Executable composition root. It is the only project that
                    knows both the UI and Registry adapter implementations.
```

`RegTerm.Tests/ArchitectureTests.cs` locks these dependency directions so a UI
reference to `Microsoft.Win32` or `RegTerm.Registry` fails the test suite.

## Behaviour

- Key rename uses the documented [`RegRenameKey`][regrenamekey] API. Value rename
  writes the new value before deleting the old one.
- Key names cannot contain backslashes. Value names can.
- Case-only renames are rejected because registry names are case-insensitive.
- DWORD and QWORD values are displayed as unsigned numbers. A `0x` prefix always
  selects hexadecimal input.
- `REG_MULTI_SZ` entries use semicolons in the editor. Escape a semicolon as `\;`,
  a backslash as `\\`, and an empty entry as `\0`.
- Search starts at the selected key. `F3` resumes after the previous match or a
  cancelled scan.

[regrenamekey]: https://learn.microsoft.com/en-us/windows/win32/api/winreg/nf-winreg-regrenamekey
