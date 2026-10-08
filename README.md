# Claude Code Plus

An **"All projects"** button for the Claude desktop app on Windows.

The Code tab's new-session folder menu lists only the **8 most recent** folders. That limit is
built into the app, with no setting to change it. Claude Code Plus adds a small button right
after the new-session chip row (environment · folder · branch · worktree · add-folder). A click
opens a searchable list of **every** project folder you have used with Claude, most recent first.
Pick one (Enter or double-click) and it opens as a new Code session in that folder, through the
app's own `claude://code/new?folder=` link.

- **Read-only.** It reads Claude's local session data (the desktop session store and
  `~\.claude\projects`) and never writes to it. It never changes, patches or injects into the
  app.
- **How the button finds its place.** Windows UI Automation reads the foreground Claude window,
  and the button sits after the chip named "Add another folder". It shows only while the
  new-session screen is in front and never takes the focus.
- **Exit.** Right-click the button and choose "Exit" to remove it until the next sign-in.

## Requirements
Windows 10/11, the Claude desktop app, and the .NET 10 SDK (`global.json`) to build. The
published app needs the .NET 10 Desktop Runtime.

## Build and test
```
dotnet build ClaudeCodePlus.slnx
dotnet test ClaudeCodePlus.slnx
```

## Install
Start the installer from Run (Win+R) or File Explorer:

```
explorer.exe "<repo>\tools\Install-Button.cmd"
```

It publishes into `publish\`, creates the Startup shortcut "Claude Code Plus button"
(`ClaudeCodePlus.exe --button`) and starts the button. The result is written to
`tools\_install.log`. Check it with `tools\Verify-Button.cmd`, which writes `tools\_verify.log`.
Rerun the installer after every change: it stops the running button, republishes and starts it
again.

Don't run the installer from a Claude Code terminal inside the desktop app. The packaged app
redirects `%APPDATA%` writes into a private store, so the shortcut would not really be created.

`ClaudeCodePlus.exe` without `--button` opens the project list directly.

## Layout
| Project | Role |
|---|---|
| `src/Feature.ChipButton` | the button: placement by UI Automation, the always-on-top window |
| `src/Feature.Picker` | the list window: search, Up/Down, Enter / double-click opens |
| `src/Feature.Catalog` | reads the project folders from Claude's local data, merged and ranked |
| `src/Feature.DeepLink` | builds the `claude://code/new?folder=` link and opens it |
| `src/Core.Infrastructure` | composition (DI) and the two run modes |
| `src/App.Host` | the exe (`ClaudeCodePlus.exe`) |
| `tests/*` | xUnit suites per module, cross-module flows, dependency rules |

## Caveat
The button depends on the app's accessible names ("Add another folder", "worktree"), checked
on Claude desktop 2.26454.2. If an app update renames them, the button stops appearing until
`ChipRowLocator`'s constants are updated.
