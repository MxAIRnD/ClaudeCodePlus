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
  `~\.claude\context-bar\<session>.json`: the file is named by the desktop app's id of the
  session (`local_<uuid>`, from `CLAUDE_CODE_HOST_SESSION_ID`; the engine's own session id in a
  session the desktop did not start), and carries both ids. `/clear`, `/resume` and `/branch`
  move the engine to a new session id; the mod follows (the file keeps its name) and reads its
  saved placement and visibility again, which those commands reset. It draws nothing in the
  app unless asked (`/ctxbar footer` or `/ctxbar top` draw a fallback there; `/ctxbar overlay`
  is the default; `/ctxbar off` hides the line, in the app and in the overlay; `/ctxbar` prints
  the figures, the session's ids and the file it writes).
- **The overlay** (`src/Feature.ContextBar`, part of `ClaudeCodePlus.exe --button`) finds the
  footer's chips through UI Automation, tells which session the app shows from the page's URL
  (the session page's UI Automation document has the value
  `https://claude.ai/epitaxy/local_<uuid>`: the same id the mod names its file by), reads that
  session's figures file and draws the line in a click-through, always-on-top layered window.
  No page title, no session store, and never another session's figures. Read-only towards the
  app, like the button.

What the line shows for the session in front:

- **Nothing** while that session has no figures file: it is starting (the mod publishes after
  its first reading), the mod does not run in it (see below), or the page is not a local
  session's.
- **"— — / 1M"** once the mod has published and the session has no reading yet: a fresh
  session before its first response, or one just compacted.
- **The figures** after that.

Install the mod once for every session, from a terminal (not from a Claude Code session inside
the desktop app, where `/plugin` is not available):

```
claude plugin marketplace add C:\Projects\ClaudeCodePlus
claude plugin install context-bar@claude-code-plus
```

A session that has the mod loaded publishes its figures; one without it shows no line.

**Line missing in a session.** Type `/ctxbar` in that session. "Unknown command" means the mod
is not loaded there: the session started before the mod was installed or enabled (a running
session keeps the plugin set it loaded at start; on app build 2.37337.1 the desktop reloaded
the plugins of its idle sessions by itself when the mod was updated, 2026-10-15, but not of the
busy ones). Type `/reload-plugins` in the session's own prompt box, which loads it without a
restart (refused when typed over Remote Control), or start a new session. When `/ctxbar` answers, its second line names the file the mod writes; the
overlay reads `~\.claude\context-bar\<local_ id>.json` for the session in front and notes a
missing one in the diagnostics file.

**Diagnostics.** When a Claude window is in front and the button or the line cannot find its
place (a composer without the chip-row anchor, a session without a footer slot, a footer
without a session URL on the page, a shown session without a figures file), one line per
change, once the state has held for about five seconds, goes to
`%LOCALAPPDATA%\ClaudeCodePlus\diagnostics.log`: the app version, the page documents' URLs
(session ids cut short) and the names and bounds of the buttons on the row in question.
`tools\Capture-Documents.cmd`, started with `explorer.exe`, is a read-only 25-second capture of
the same page documents and of the newest session record, for a bug report (it prints no
titles, and ids cut short).

## Requirements
Windows 10/11, the Claude desktop app, and any .NET SDK from 8 up to build: `global.json`
accepts 8.0.100 or newer and takes the newest SDK installed, and the language is pinned to C# 12
so every SDK compiles the same code. The published app runs on the .NET Desktop Runtime 8 or any
newer one: it targets .NET 8 and rolls forward to 9 or 10 when 8 is absent (`RollForward` in
`src\Directory.Build.props`).

## Build and test
```
dotnet build ClaudeCodePlus.slnx
dotnet test ClaudeCodePlus.slnx
```

`ClaudeCodePlus.sln` is the same solution in the classic format, for an SDK older than 9.0.200
(which cannot read `.slnx`): `dotnet build ClaudeCodePlus.sln`. Name the file in every `dotnet`
command, since both sit in the root. The installer publishes the project file directly and works
with either SDK.

The mod's tests run against the engine itself:

```
claude plugin validate mods\context-bar
claude plugin test mods\context-bar
```

## Install
Start the installer from Run (Win+R) or File Explorer:

```
explorer.exe "<repo>\tools\Install-Button.cmd"
```

It publishes into `publish\`, creates the Startup shortcut "Claude Code Plus button"
(`ClaudeCodePlus.exe --button`) and starts the button. The result is written to
`tools\_install.log`. Check it with `tools\Verify-Button.cmd`, which writes `tools\_verify.log`.
Rerun the installer after every change: it stops the running button, moves the previous
`publish\` folder to the Recycle Bin, publishes afresh and starts the button again. The fresh
folder matters: a publish over an older one can keep stale package DLLs (seen when the target
moved from .NET 10 to .NET 8, when the button then crashed at start), so a publish done by hand
should go into an empty folder too.

Don't run the installer from a Claude Code terminal inside the desktop app. The packaged app
redirects `%APPDATA%` writes into a private store, so the shortcut would not really be created.

`ClaudeCodePlus.exe` without `--button` opens the project list directly.

## Layout
| Project | Role |
|---|---|
| `src/Feature.ChipButton` | the button: placement by UI Automation, the always-on-top window, its diagnostics |
| `src/Feature.ContextBar` | the context line: footer placement and the shown session's id by UI Automation, the figures file, the layered window, its diagnostics |
| `mods/context-bar` | the Claude Code mod that publishes each session's context figures |
| `src/Feature.Picker` | the list window: search, Up/Down, Enter / double-click opens |
| `src/Feature.Catalog` | reads the project folders from Claude's local data, merged and ranked |
| `src/Feature.DeepLink` | builds the `claude://code/new?folder=` link and opens it |
| `src/Core.Infrastructure` | composition (DI) and the two run modes |
| `src/App.Host` | the exe (`ClaudeCodePlus.exe`) |
| `tests/*` | xUnit suites per module, cross-module flows, dependency rules |
| `tools/` | the installer, its check, and the read-only page-document capture |

## Caveat
The button depends on the app's accessible names: "Environment: …" (checked on Claude desktop
2.37337.1, 2026-10-14) and, for the older layout, "Add another folder" and "worktree" (checked
on 2.26454.2). If an app update renames them, the button stops appearing until
`ChipRowLocator`'s constants are updated; the diagnostics file then says what the app showed
instead. The context line depends on the footer's "Model: …" chip and on the session page's
URL route `https://claude.ai/epitaxy/local_<uuid>` (both checked on 2.37337.1, 2026-10-15) the
same way, see `FooterLocator`.
