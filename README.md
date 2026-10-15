# Claude Code Plus

An **"All projects"** button and a **context-usage line** for the Claude desktop app on Windows.

The Code tab's new-session folder menu lists only the **8 most recent** folders. That limit is
built into the app, with no setting to change it. Claude Code Plus adds a small button right
after the new-session chip row (environment · folder · branch · worktree · add-folder). A click
opens a searchable list of **every** project folder you have used with Claude, most recent first.
Pick one (Enter or double-click) and it opens as a new Code session in that folder, through the
app's own `claude://code/new?folder=` link.

- **Read-only.** It reads Claude's local session data (the desktop session store and
  `~\.claude\projects`) and never writes to it. It never changes, patches or injects into the
  app.
- **Quick list.** A session record is read only as far as its folder and time, and what it
  yielded stays in memory while the button runs: a press reads again only the records whose
  size or write time changed. One scan runs when the button starts, so the first press is quick
  too. Nothing is written to disk for this.
- **How the button finds its place.** Windows UI Automation reads the foreground Claude window.
  In the newer layout (the "environments" start card) the button sits after the composer's
  environment and permissions chips ("Environment: …", "Permissions: …"); in the older one,
  after the chip named "Add another folder". It shows only while the new-session screen is in
  front, hides rather than cover one of the app's buttons, and never takes the focus.
- **Exit.** Right-click the button and choose "Exit" to remove it until the next sign-in.

## The context line
A running session's prompt footer (the row under the prompt box: "Bypass permissions …
Fable 5.1 · Extra") gets a thin gradient line between the permission-mode chip and the model
chip: the used part of the context window, sky blue at the window's start through green, lime,
gold and orange to red at its end (so the color at the line's tip reads the level), the rest of
the window as the same gradient faintly, a white knob at the tip, a faint tick where
auto-compaction runs, and the percentage and the counts ("37%  370K / 1M") before it. It takes
no height of its own. The schemes and track variants are `BarStyle` presets; the one drawn is
`BarStyle.Default`.

Two parts make it:

- **The mod** (`mods/context-bar`, a Claude Code plugin) runs inside each session and writes
  the engine's own context figures, after every model response, to
  `~\.claude\context-bar\<session id>.json`. It draws nothing in the app unless asked
  (`/ctxbar footer` or `/ctxbar top` draw a fallback there; `/ctxbar overlay` is the default;
  `/ctxbar off` hides the line, in the app and in the overlay; `/ctxbar` prints the figures).
- **The overlay** (`src/Feature.ContextBar`, part of `ClaudeCodePlus.exe --button`) finds the
  footer's chips through UI Automation, tells which session the app shows from the app's page
  title ("<session title> - Claude Code") matched against the desktop session store (the
  newest `lastFocusedAt` among namesakes, or of all sessions when the title matches none),
  reads that session's figures file and draws the line in a click-through, always-on-top
  layered window. Read-only towards the app, like the button.

Install the mod once for every session, from a terminal (not from a Claude Code session inside
the desktop app, where `/plugin` is not available):

```
claude plugin marketplace add C:\Projects\ClaudeCodePlus
claude plugin install context-bar@claude-code-plus
```

A session that has the mod loaded publishes its figures; one without it shows no line.

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
| `src/Feature.ContextBar` | the context line: footer placement by UI Automation, the shown session, the figures file, the layered window |
| `mods/context-bar` | the Claude Code mod that publishes each session's context figures |
| `src/Feature.Picker` | the list window: search, Up/Down, Enter / double-click opens |
| `src/Feature.Catalog` | reads the project folders from Claude's local data, merged and ranked |
| `src/Feature.DeepLink` | builds the `claude://code/new?folder=` link and opens it |
| `src/Core.Infrastructure` | composition (DI) and the two run modes |
| `src/App.Host` | the exe (`ClaudeCodePlus.exe`) |
| `tests/*` | xUnit suites per module, cross-module flows, dependency rules |

## Caveat
The button depends on the app's accessible names: "Environment: …" (checked on Claude desktop
2.37337.1, 2026-10-14) and, for the older layout, "Add another folder" and "worktree" (checked
on 2.26454.2). If an app update renames them, the button stops appearing until
`ChipRowLocator`'s constants are updated. The context line depends on the footer's "Model: …"
chip (checked on 2.37337.1, 2026-10-15) the same way, see `FooterLocator`.
