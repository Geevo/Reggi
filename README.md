# Reggi

A Windows Registry editor for your terminal — browse keys, search, and edit values.

![Reggi browsing HKEY_CURRENT_USER in Windows Terminal](docs/reggi.png)

## Give it a go

You'll need Windows and the .NET 10 SDK:

```powershell
dotnet run --project Reggi
```

Want a standalone exe? Publish the native Windows build:

```powershell
dotnet publish Reggi -c Release -r win-x64
```

Find `reggi.exe` in `Reggi/bin/Release/net10.0-windows/win-x64/publish/`. It runs without a .NET runtime installed. Native publishing needs the Visual Studio C++ build tools; if the linker or `vswhere.exe` can't be found, try **Developer PowerShell for Visual Studio**.

If Command Prompt stays blank when you run the exe, try the alternative console driver:

```bat
chcp 65001
reggi.exe --net-driver
```

For a vPro serial console, use `reggi.exe --vpro`. It selects that driver, switches to UTF-8 for the session, and translates its F1–F12 key codes. No separate `chcp` command needed.

To see where startup stops, add `--trace-startup` to either command. The trace is written to `%TEMP%\reggi-startup.log`.

## Getting around

Use `Tab` to switch between keys and values. The bar at the bottom shows the main shortcuts, and `F1` opens help in the app.

| Key | What it does |
| --- | --- |
| `↑` / `↓` | Move through the current pane |
| `Enter` | Open a key or edit a value |
| `→` / `←` | Expand or collapse a key; move into or out of it |
| `Space` | Toggle a key open or closed |
| `Backspace` | Go to the parent key |
| Type a name | Jump to a matching key in the keys pane |
| `Ctrl+F` / `F3` | Find something / find the next match |
| `F2` | Rename the selected key or value |
| `F5` | Reload the current key |
| `F6` / `F7` | Add a value / add a key |
| `Del` or `F8` | Delete the selected key or value |
| `Ctrl+Q` | Quit |

Search can look through key names, value names, and value data, starting at the selected key. `F3` picks up where the last search left off.

![Reggi's Find dialog](docs/reggi-find.png)

## Good to know

- Editing `HKEY_LOCAL_MACHINE` and much of `HKEY_CLASSES_ROOT` usually needs an elevated terminal. You can still browse without one.
- Deleting a key deletes everything under it. Reggi asks before deleting keys or values.
- DWORD and QWORD values can be edited in hex or decimal. For `REG_MULTI_SZ`, separate entries with semicolons; use `\;` for a literal semicolon, `\\` for a backslash, and `\0` for an empty entry.
- `F8` is an alternative to `Del` for terminals that don't pass the Delete key through reliably.

Run the tests with `dotnet test Reggi.sln`.
