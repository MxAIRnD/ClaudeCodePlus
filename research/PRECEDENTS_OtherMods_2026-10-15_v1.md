# Precedents: other mods, status lines and desktop companions (v1)

Research for Claude Code Plus (repo `C:\Projects\ClaudeCodePlus`, main at `bcf9b05`), 2026-10-15.
Scope: how other Claude Code mods, status-line tools, wrappers and companions solve the problems behind
issue #1 ("Button hidden on the new-session screen when no folder is selected"), issue #2 ("Context line
missing in a session: no figures file, no store record"), issue #3 ("Context line shows the previous
session's figures while a new session is starting") and the possible 4th problem (the engine session id
changes inside one desktop session, so the figures file can go stale), and what can be reused.

Read-only work: no code changes, no commits, no GitHub posts. Machine: MSI (pidDomain `win32:msi`),
Claude desktop 2.37337.1 (packaged), engine 2.1.295, Windows 11.

**Evidence marks** used on every claim:

- **[read]**: verified by reading the page, file, issue or source code named.
- **[snippet]**: seen only in a search snippet or a summary; not read in full.
- **[observed]**: seen on this machine by a read-only probe (UI Automation read, file listing, JSON
  read, `grep` of the engine binary). Probe scripts are in `%TEMP%\ccp-research-precedents\`.
- **[inference]**: my reasoning from the evidence; not a finding.

---

## 0. Summary

1. **No existing tool makes the overlay unnecessary.** The big status-line tools (claude-hud 28.5k
   stars, ccusage 19.0k, ccstatusline 13.3k, claude-powerline 1.2k) draw only in a terminal: the
   `statusLine` command renders nothing in the desktop Code tab (users say so in anthropics/claude-code
   #90708, "Desktop app: bring back the context-usage indicator (or make the footer widget
   configurable)", and #91712, "Let the Code tab's usage ring report session context, not only the 5-hour
   window") [read]. Since 2026-10-13/14 the desktop app no longer shows context in its usage ring at all;
   it moved to the Session Rail panel (#102094, "context-window usage no longer shown anywhere"; #102279,
   "the usage ring shows the account's session limit, not the window's context"; #102288 and #102254,
   "context usage now takes 3 clicks") [read]. The
   only in-app alternative is a mod band above the prompt; at least six such mods appeared between
   2026-10-02 and 2026-10-15 (context-band, fredrikbonde/claude-context-bar, ContextLab, justerification,
   lrdub, k-wolfe99) plus Anthropic's sample `token-weather` [read]. None draws in the footer row.
2. **The mod's figures source is already the right one.** `$.session.usage()` returns the same figures as
   the official status line, and costs no request without a breakdown (Anthropic's token-weather README)
   [read]. Transcript parsing (VS Code extension edenaion/claude-context-bar, hatohato-lab meter) has to
   guess the window size from the model id [read]. OpenTelemetry has no context-fill metric [read].
3. **Strongest reusable finding: the shown session can be identified by id, not by title.** The page's
   UIA Document element carries the URL `https://claude.ai/epitaxy/local_<uuid>` in its ValuePattern
   [observed], because Chromium returns the document URL as a document's UIA value [read: Chromium
   source]. `local_<uuid>` is the desktop store record id. The engine gets the same id in its environment
   as `CLAUDE_CODE_HOST_SESSION_ID` [observed], and writes a live registry `~/.claude/sessions/<pid>.json`
   with `hostSessionId`, `sessionId` (engine id), `name` (title), `status`, `startedAt` [observed]; three
   third-party tools already read that registry (claude-watch, claude-streamdeck, hatohato-lab) [read].
   Keying the figures by `hostSessionId` would remove the title race (#3), the store dependency (#2-B)
   and the id-change staleness (4th problem).
4. **The 4th problem is real and documented.** `/clear` assigns a new engine session id (env-vars and
   OpenTelemetry docs), and a mod's `session.start` does not run again after `/clear`, `/resume` or
   `/branch` (mods reference) [read]. `register.tsx` learns the id only in `session.start`
   (`register.tsx:147`, `:157-168`, `:290-305`), so after `/clear` it keeps writing the old id's file
   [read + inference]. Anthropic documents the fix pattern: also hook `classic.SessionStart` with
   `source: ['clear', 'resume', 'fork']` [read].
5. **#2 hypothesis A has a cheap test and a fix without restart.** Plugins load at session start; a
   running session picks up a new plugin only with `/reload-plugins` (plugin docs) [read]; desktop
   sessions accept `/reload-plugins` (#96223, "Desktop sessions load the install-time cache copy of
   local-directory marketplace plugins instead of loading in place"; #100926, "plugin `userConfig` rows
   missing until `/reload-plugins` in desktop-hosted and headless sessions") [read]. Engine start time is in
   `~/.claude/sessions/<pid>.json` (`startedAt`), install time in `installed_plugins.json`
   (`installedAt`) [observed].
6. **#1: AutomationId is no way out.** Chromium maps UIA AutomationId to the HTML `id` attribute only
   [read: source], and the app's ids are React/Base UI generated (`_r_4n4_`, `base-ui-_r_4ng_`), its
   class names Tailwind utility strings [observed]. Keep accessible names with structural fallbacks,
   and log the observed names with the app version when no anchor is found.

---

## 1. Tools catalogue

"Desktop" means: does it show anything inside the Claude desktop app's Code tab. Stars and dates are from
the GitHub API on 2026-10-15 (`pushed_at`; release when one exists) [read].

| Name | URL | What it shows | Figures source | Desktop Code tab | Last activity, stars | Reuse verdict |
|---|---|---|---|---|---|---|
| ccstatusline | https://github.com/sirmalloc/ccstatusline | Configurable status line: model, git, tokens, context %, limits, service health | `statusLine` stdin JSON plus transcript JSONL scan [read README] | No: `statusLine` is terminal-only (#90708, #91712) [read] | v2.2.32 2026-10-09; 13,295 | None for figures. Display convention: "stale-data fallback" and "`?` output when status data is unavailable" [read] |
| claude-hud | https://github.com/jarrodwatts/claude-hud | Context, rate limits, tools, agents, todos below the input | A plugin that installs a status-line command; stdin JSON, some elements from the transcript [read README] | No (status line) | v0.11.2 2026-10-13; 28,545 | None |
| ccusage | https://github.com/ccusage/ccusage | Usage/cost reports; `ccusage statusline` (beta) | "token usage and costs from local data" (the session transcripts) [read README; the transcript part is inference]; statusline stdin for `ccusage statusline` | No | v20.0.34 2026-10-15; 19,014 | None |
| claude-powerline | https://github.com/Owloops/claude-powerline | Powerline status line, context, limits | `statusLine` stdin JSON (`context_window.context_window_size`), transcript for cache timer [read README] | No | v1.32.3 2026-10-10; 1,174 | Convention: segments "Hidden when native data is unavailable" [read] |
| CCometixLine | https://github.com/Haleclipse/CCometixLine | Rust status line, context window info | "Token usage percentage based on transcript analysis" [read README] | No | v1.1.2 2026-03-14; 3,473 | None |
| cc-statusline | https://github.com/chongdashu/cc-statusline | Generated bash status line, context %, ccusage stats | stdin JSON via `jq`, ccusage [snippet + README head read] | No | pushed 2026-02-16; 645 | None |
| claude-code-statusline | https://github.com/rz1989s/claude-code-statusline | Multi-line bash status line | stdin JSON; "Windows Native: Not Supported" [read README] | No | v2.27.1 2026-07-04; 479 | None |
| best-claude-hud | https://github.com/GaoSSR/best-claude-hud | Rust status-line HUD | Official `context_window` fields first, transcript only as fallback; "All-zero usage placeholders written after an interrupted response are ignored"; a new session "shows `0% · 0 tokens` and never scans older project history" [read README] | No | v0.1.11 2026-08-08; 1,074 | Convention for #3: keep the last valid reading of the same session, never another session's |
| howmuchleft | https://github.com/stricttools/howmuchleft | Three gradient bars: context, 5-hour, weekly | stdin JSON [read README] | No | v0.17.0 2026-10-06; 1 | None |
| Claude-Code-Usage-Monitor | https://github.com/Maciek-roboblog/Claude-Code-Usage-Monitor | Terminal monitor of plan usage, forecasts | Transcripts plus a `--statusline` hook capturing official `rate_limits`; "stale or expired captures fall back to labeled local estimates"; exit code `20` "indeterminate/no active session" [read README] | No | v4.0.0 2026-06-27; 8,742 | Convention: label stale values instead of showing them as current |
| claude-context-bar (VS Code) | https://github.com/edenaion/claude-context-bar | VS Code status-bar item per Claude Code tab | Reads `<configDir>/projects/*.jsonl`; window size inferred from the model id because "Claude session files record only the Model ID"; hides sessions idle for 3 min and "ghost sessions" superseded after `/clear` [read README] | No (VS Code only) | pushed 2026-09-02; 19 | Precedent for #3/4th: hide superseded sessions at once |
| claude-code-context-meter (hatohato-lab) | https://github.com/hatohato-lab/claude-code-context-meter | On-demand table of every running session's context, memory, name | Transcript tail usage; maps processes to sessions via `~/.claude/sessions/<pid>.json` first, then `--resume` argv, then `CLAUDE_CODE_SESSION_ID`; notes the formats are undocumented ("if they change it does not break, values just stop appearing") [read README, Japanese] | Script outside the app; covers desktop sessions too [inference] | pushed 2026-09-02; 0 | Precedent for using the pid registry |
| context-band | https://github.com/JiahaoXu10Arthur/context-band | One dim line above the prompt: context %, 5-hour, weekly, per-model windows | Mod; refreshes after every turn; `/config` switches [read README] | Yes (README screenshot "in Claude Code Desktop"; posted as a workaround in #102094) [read] | created 2026-10-14, pushed 2026-10-15; 1 | In-app alternative, but the band, not the footer |
| claude-context-bar (fredrikbonde) | https://github.com/fredrikbonde/claude-context-bar | Stacked bar per `/context` category above the prompt | Mod; "local `summary` estimate (no token-count API requests)"; refreshes at start, after tool calls, turns, compaction [read README] | Unknown (install from a terminal; built on 2.1.294) | pushed 2026-10-14; 1 | Refresh triggers worth copying (after each tool call) |
| claude-context-bar (ContextLab) | https://github.com/ContextLab/claude-context-bar | Stacked bar under the prompt with legend | Mod; local estimate [read README] | Unknown (terminal-oriented layout) | pushed 2026-10-07; 1 | None |
| claude-code-context-meter (justerification) | https://github.com/justerification/claude-code-context-meter | Band above the prompt: context, 5h, 7d, cost, Recap button | Mod; installs via `CLAUDE_CODE_PLUGIN_DIRS` + `CLAUDE_CODE_PLUGIN_DIR_WATCH`, "Both are read when a session starts"; "built and used on 2.1.286 in the desktop app, on macOS" [read README] | Yes [read] | pushed 2026-10-06; 0 | Evidence for #2-A (env read only at session start) |
| claude-code-context-meter-mod (lrdub) | https://github.com/lrdub/claude-code-context-meter-mod | Context bar against a 150k budget | Mod; "in the terminal it sits on the status line; in the desktop app, which does not draw mod status lines, it is a band above the prompt" [read README] | Yes (band) | pushed 2026-10-14; 0 | Confirms `$.ui.status` is not drawn in desktop |
| claude-context-bar (k-wolfe99) | https://github.com/k-wolfe99/claude-context-bar | Model, context bar, prompt-cache countdown and prices | Mod plus a status-line script; reads the transcript on resume; shows `⏱ --:--` when the transcript can't be read ("in the desktop app with a very long session, over about 4 MB") [read README] | Partly (mod) | v1.2.0 2026-10-02; 1 | Placeholder convention for "size known, time unknown" |
| claude-mods (Arunjay4213) | https://github.com/Arunjay4213/claude-mods | context-lens, quota-meter, token-ledger, budget-guard | Mods reading `$.session.usage()`; pinned line under the prompt; README still asks for `CLAUDE_CODE_ENABLE_FUNCTION_HOOKS=1` [read README] | No (pinned status lines; terminal) | pushed 2026-09-15; 9 | None; its author filed #94424 (an event when usage changes) [read] |
| token-weather (Anthropic sample) | https://github.com/anthropics/claude-code-playground/tree/main/claude-code/mods/token-weather | Weather-style context forecast above the prompt | `$.session.usage().context`: "the same figures the status line shows (`total_input_tokens`, `context_window_size`, `used_percentage`)"; "free, because the mod doesn't ask for a breakdown" [read README] | Yes (band; mods draw in desktop per docs) | repo pushed 2026-10-14; 234 | Confirms our figures source |
| Paste-in workaround (vrszabolcs-hash) | https://github.com/anthropics/claude-code/issues/102094 (comment 2026-10-15) | Context % plus one-click effort buttons above the prompt | Mod text pasted into a session [read comment] | Yes | 2026-10-15 | None |
| ai-code-usage-tray | https://github.com/saime428/ai-code-usage-tray | Windows tray: usage, limits, sessions of several tools | Desktop store `claude-code-sessions/**/*.json`, transcripts; folds `priorCliSessionIds` into the current conversation; for a closed tool "the last figures stay, marked with their age" [read README + `lib/usage.js`] | Outside the app (tray) | pushed 2026-10-13; 5 | Precedent for the 4th problem and for stale-with-age |
| claude-watch | https://github.com/rashid2003/claude-watch | macOS menu bar: usage limits, sessions | `~/.claude/sessions/<pid>.json` as "Claude Code's registry of running sessions", including `hostSessionId` ("the desktop chat (desktop runs only)"); liveness checks the pid and its start time "so a recycled pid doesn't count" [read source] | Outside the app | pushed 2026-10-10; 0 | Reuse the registry join and the pid-reuse guard |
| claude-streamdeck (spike notes) | https://github.com/lack435/claude-streamdeck/blob/main/docs/spike-findings.md | Stream Deck keys for usage and waiting agents | Windows desktop store plus "live PIDs in `~/.claude/sessions/<pid>.json`... a matching live PID ⇒ running" [read] | Outside the app | pushed 2026-08-27; 0 | Same join, on Windows |
| magpie | https://github.com/yetone/magpie | Menu-bar model router; reads desktop titles | Desktop record fields `cliSessionId`, `unarchivedCliSessionId`, `preClearCliSessionId`, `priorCliSessionIds` [read `desktop_titles.go`] | Outside the app | pushed 2026-10-15; 13,315 | Field list for the 4th problem |
| agent-git (mechanism notes) | https://github.com/Einsia/agent-git/blob/main/docs/mechanism-probing/desktop-apps.md | Notes on desktop apps' session storage | Reads `local_*.json` sidecars; field list; refuses to write them: "The format is unstable, and it is migrating" [read] | n/a | pushed 2026-10-13; 412 | Risk statement for any store reader |
| claude-code-dashboard | https://github.com/digorgonzola/claude-code-dashboard | Live dashboard of sessions | Desktop store index `cliSessionId → local_ id`, cached 15 s; deep link `claude://code/continue?session=` needs the `local_` id [read `lib/desktop.js`] | Outside the app | pushed 2026-10-15; 0 | Small precedent |
| Desktop app's own readout | https://code.claude.com/docs/en/desktop | Usage ring; context now in the Session Rail | Engine | Yes, but: ring shows plan usage since 2026-10-13/14 (#102094, #102279); Session Rail bar does not update (#102230) [read] | 2.37337.1 | The reason the overlay exists |
| VS Code extension's indicator | https://code.claude.com/docs/en/vs-code | Context indicator in the prompt box | Engine | No (VS Code); reported as showing only above about 50% [snippet] | n/a | None |
| `claude agents --json` | https://code.claude.com/docs/en/agent-view | Live sessions: `pid`, `status`, `sessionId`, `name`, `cwd` | Official: "the supported way to read session state from outside Claude Code, for example from a status bar" [read] | Lists desktop sessions too [inference from the registry, unverified] | 2.1.x | Supported alternative to reading `~/.claude/sessions` directly; needs the `claude` CLI, which is not on PATH here (only the desktop's bundled engine) [observed] |
| OpenTelemetry metrics | https://code.claude.com/docs/en/monitoring-usage | `claude_code.token.usage` per request, `session.id` attribute | OTLP/Prometheus exporter [read] | n/a | n/a | Not a figures source: no window size, no fill [read] |

**Verdict on Q1.** Nothing replaces the overlay for an always-visible line in the footer row: status-line
tools cannot draw in the desktop app, and mods can draw only in their own render sites (band above the
prompt, panes, `SessionMode` label), not between two of the app's chips [read: mods overview, "Where mods
run"; mods reference render sites]. A mod-only band (context-band style) is a fallback the mod already
has (`/ctxbar top`), with two known desktop defects: the band is hidden while the next-step box is shown
(#102249, "Desktop Code tab (2.37337.1): mod AbovePrompt band is hidden while the next-step box is shown")
and mods' `$.ui.status` lines are not drawn in desktop (lrdub README) [read]. The figures source should
stay `$.session.usage()`; the robustness gain is in **which session** the figures belong to, not in the
figures.

---

## 2. Per issue

### 2.0 A shared finding first: the shown session's id is readable, and the engine knows it too

These observations change the recommendation for #2, #3 and the 4th problem, so they come first.

1. **The page URL names the desktop record.** A read-only UIA probe of the foreground Claude window found
   two `Document` elements (both `AutomationId = RootWebArea`, `FrameworkId = Chrome`): the shell page
   (`file:///C:/Program%20Files/WindowsApps/Claude_2.37337.1.0_x64__pzs8sxrjxfjjc/app/resources/app.asar/.vite/renderer/main_window/index.html`,
   empty Name) and the session page, Name `Session overlay matching issue - Claude Code`, **ValuePattern
   value `https://claude.ai/epitaxy/local_03b846e4-5f76-450e-8dd9-a4dbc37d0883`** [observed]. That
   `local_…` is the file name of the session's desktop store record [observed].
2. **Why it works.** Chromium's `AXPlatformNodeWin::GetValueAttributeAsBstr` returns
   `GetTreeData().url` for any platform document (`ui/accessibility/platform/ax_platform_node_win.cc`,
   around line 8394 at commit `b46ad50`) [read: source]. The Document's Name is the page title: Blink's
   root name is the document element's `aria-label`, else `document->title()`
   (`third_party/blink/renderer/modules/accessibility/ax_node_object.cc`, around line 7801) [read:
   source]. The route `epitaxy/local_<uuid>` matches what users see elsewhere: "Copy link" gives
   `claude://claude.ai/epitaxy/local_<uuid>` (anthropics/claude-code #101545, "[BUG] Desktop: sidebar
   right-click lost "Copy session ID"; "Copy link" gives the app's local_ id, not the CLI session id")
   [read], and the app's Local Storage keys include `composer-draft:epitaxy-local_<uuid>` (agent-git
   notes) [read].
3. **The engine knows the same id.** The engine process of a desktop session has
   `CLAUDE_CODE_HOST_SESSION_ID=local_<uuid>`, `CLAUDE_CODE_SESSION_ID=<engine id>`,
   `CLAUDE_CODE_ENTRYPOINT=claude-desktop` and `CLAUDE_CODE_DESKTOP_APP_VERSION=2.37337.1` in its
   environment, inherited by its tool subprocesses [observed in this session's Bash environment]. The
   engine binary reads `CLAUDE_CODE_HOST_SESSION_ID` to fill `hostSessionId` in its pid file [observed:
   string in `claude.exe` 2.1.295]. A mod could read it with `$.env.get('CLAUDE_CODE_HOST_SESSION_ID')`
   (`$.env` has `get` per the mods reference) [read]; that the value is visible to `$.env.get` inside
   the engine is **unverified** [inference].
4. **The engine keeps a live registry.** `~/.claude/sessions/<pid>.json` holds, per running engine:
   `pid`, `sessionId`, `cwd`, `startedAt`, `procStart`, `version`, `kind`, `entrypoint`,
   `hostSessionId`, `name`, `nameSource`, `status` (`busy`/`idle`), `updatedAt`, `bridgeSessionId` (when
   Remote Control is on) and more [observed]. On this machine all 6 live desktop engines map 1:1 to
   store records: `hostSessionId` = record file, `sessionId` = record `cliSessionId`, `name` = record
   `title` [observed]. The engine merges updates into this file (`[concurrentSessions] updatePidFile`)
   [observed: binary strings]. It is undocumented; the documented interface over the same data is
   `claude agents --json` ("the supported way to read session state from outside Claude Code, for
   example from a status bar") [read: agent-view docs].
5. **Others use it.** claude-watch (macOS), claude-streamdeck (Windows) and hatohato-lab's meter join the
   registry with the desktop store or transcripts [read]. A reporter in #102198 ("[BUG] Desktop (SSH
   remote) restart spawns a fresh CLI with the same session id instead of resuming; one session id
   shared by concurrent sessions across all cwds") used it to show three concurrent desktop engines
   reporting the same `sessionId` with different `hostSessionId` values [read].

**What this enables** [inference]: the overlay reads the shown session's `local_` id from the Document's
ValuePattern (add `ValuePattern.ValueProperty` to the probe's `CacheRequest`, `FooterProbe.cs:66-74`; the
probe already fetches Documents, `FooterProbe.cs:22-26`), and the mod writes its figures under the same
`local_` id (or writes it inside the file). Then the overlay needs neither the title nor the desktop store
to pick the figures. Microsoft's note that ValuePattern current values need an element with a full
reference applies to `.Current` [snippet: Microsoft Learn ValuePattern.Current]; a cached property should
work with `AutomationElementMode.None` [inference, to be tested].

### 2.1 Issue #1 ("Button hidden on the new-session screen when no folder is selected")

**Precedents found**

- **AutomationId is not a stable anchor here.** Chromium sets UIA AutomationId from the HTML `id` only
  (`GetAuthorUniqueId()` returns `kHtmlId`; the root web area gets the constant `RootWebArea`)
  [read: Chromium `ax_platform_node_win.cc` ~5331, `ax_platform_node_delegate.cc` 632-634]. Community
  automation users report the same: "UIA Automation ID ... matches the element's HTML id", and it breaks
  when an app update removes the id (Blue Prism forum, Qt bug QTBUG-99485) [snippet]. In this app the
  ids are generated per render: `_r_0_`, `_r_4n4_` (mode chip), `base-ui-_r_4ng_` (model chip) [observed],
  which is the React `useId` / Base UI pattern [inference]. `data-testid` is not exposed to UIA at all
  (no UIA property maps arbitrary `data-*` attributes; AriaProperties carries ARIA only) [read: source,
  property switch], and UIA ClassName is the HTML class attribute (`kClassName`) [read: source], which
  here is a Tailwind utility string (`cds-reset group/btn relative isolate inline-flex ...`) [observed].
- **Microsoft's guidance**: AutomationId "uniquely identifies a UI Automation element from its siblings"
  and "does not guarantee a unique identity throughout the tree; it typically needs container and scope
  information to be useful" [read: "Use the AutomationID Property"]. The same page warns that a
  descendants search "may iterate through hundreds or even thousands of elements" and recommends
  starting from the application window or a lower container [read].
- **Playwright for Electron** launches the app itself (`electron.launch` with `executablePath`); the docs
  describe no attach to a running packaged app, and call Electron support "experimental" [read]. Not
  usable for a read-only companion of the packaged app.
- **Self-diagnostics**: pywinauto's documented way to see what a window offers is
  `print_control_identifiers()`, which prints the candidate names for every control [read]. Chromium's
  own accessibility tests use UIA tree dumps (`ax_tree_formatter_uia_win.cc`) [read: file exists in the
  code search result; content not read].
- **Names drift.** The model chip's name read today is `Model: Fable 5.1 Max` [observed], while
  `FooterLocator.cs:9-11` describes "Model: Fable 5.1" followed by a separate effort chip [read]; the
  prefix match still holds. Anthropic closed #35257 ("Accessibility: 22 unlabeled icon-only buttons
  missing aria-labels (WCAG 4.1.2)") [snippet: title and state only], so labels are being added and
  changed by accessibility work [inference].
- **Localization risk.** The desktop app ships 43 locale files (`am.json` … `zh-Hant.json`) in
  `app\resources` [observed]. The anchor names ("Add another folder", "Environment: ", "Model: ") are not
  in those shell files [observed], so they come from the web UI; whether they are translated when the
  app language changes is **unknown**.

**What they imply.** No precedent offers a stable machine id for these chips. Name-based anchors with
structural fallbacks (what `ChipRowLocator` already does: "Add another folder", then the "worktree" box,
then the "Environment: …" chip plus its row, `ChipRowLocator.cs:44-97`) are the norm. The missing piece
is diagnosability when they fail.

**Recommendation**

- First re-check #1 on 2.37337.1 (see open question 1): if the newer layout's "Environment: …" chip is
  present with no folder picked, #1 is fixed for that layout by `f833f53` and the "No folder" fallback is
  needed only for the older layout.
- Keep the issue's own proposal (fall back to the "No folder" button on the environment chip's row, never
  the one in a session header) for the older layout; nothing better was found.
- **Adapt pywinauto's idea**: when no anchor is found while a Claude window is in front, log once per app
  version the names and bounds of the buttons on the lowest composer row, with the app version. The
  version is in the shell Document's URL (`Claude_2.37337.1.0_x64__…`) [observed] or the process image
  path [inference]. This turns "the button silently disappeared after an update" into a one-line report.
- Optional: a name table keyed by app version (and locale, once known), so a renamed chip is a data
  change, not a code change [inference].

### 2.2 Issue #2 ("Context line missing in a session: no figures file, no store record")

**Hypothesis A: the mod is not loaded in that session**

- Documented: "Plugins load at session start"; changes "don't reach this layer until you run
  `/reload-plugins` or start a new session" (plugin loading docs) [read]. "If you install or update a mod
  from your shell while a session is open, run `/reload-plugins` in that session to load it. Otherwise it
  loads the next time you start Claude Code" (mods overview) [read]. `CLAUDE_CODE_PLUGIN_DIRS` is "read
  when a session starts" (justerification README) [read]; the env-vars docs list it as "Plugin
  directories to load for the session" [read].
- `/reload-plugins` works in desktop sessions: #96223 ("[BUG] Desktop sessions load the install-time
  cache copy of local-directory marketplace plugins instead of loading in place") and #100926
  ("`$.config.list()` / `$.config.set` have no plugin `userConfig` rows until `/reload-plugins` in
  desktop-hosted and headless sessions") both run it there [read]. A mod's `session.start` runs "again
  after a reload of that mod" (mods reference) [read], so `/reload-plugins` would make the mod publish.
- Contrary or complicating reports: #101640 ("Mods (function-hook plugins) load but never run in desktop
  Code tab; CLAUDE_CODE_PLUGIN_DIRS ignored", macOS, desktop 2.31226.1) [read]; #99130 ("Mods off remotely
  on 2.1.288 despite docs saying on by default since 2.1.287 (rollout switch serves off)"), where the
  debug log says `hooks module <name> not loaded: hooks modules are turned off for installed plugins in
  this process: the rollout switch served off` [read]. On Windows 2.37337.1 a mod loaded with
  `CLAUDE_CODE_PLUGIN_DIRS` does run in the desktop (#102249) [read], as does context-bar here (figures
  files exist for all 6 live desktop engines) [observed].
- #96223 also says desktop sessions load the **install-time cache copy** of a local-directory marketplace
  plugin, not the source folder, even after `/reload-plugins` (macOS 2.1.280; a comment reproduces it on
  Windows 11 26300 with engine 2.1.295) [read]. The docs say such plugins load in place [read]. On this
  machine the copied plugins' cache folders carry `.in_use/<pid>` markers for every live engine pid,
  while `plugins\cache\claude-code-plus\context-bar\0.1.0` has no `.in_use` folder [observed], which
  suggests context-bar loads in place here; inconclusive [inference].
- How outside tools tell "not loaded": the documented signals are the `/plugin` line `N mods active ·
  names` (terminal only) and the debug log (`--debug` only) [read: mods overview, troubleshoot]. No
  desktop debug logs exist under `~\.claude\debug` here [observed]. No precedent publishes a
  per-session "mod loaded" heartbeat; the published mods just draw or don't [inference from the READMEs
  read].

**Hypothesis B: no store record (Remote Control or another session kind)**

- Terminal and Remote Control sessions have no desktop record: #101326 ("Sessions started in terminal /
  Remote Control aren't visible in the Claude desktop app (and vice versa)") [read]. #101862 ("[BUG]
  /desktop on a terminal session with Remote Control on leaves two Desktop sidebar rows for one
  conversation") describes a "Remote Control row that the terminal session registered" next to a new
  local row [read]. Mods "run in the session on your machine" for Remote Control, but draw only "in the
  terminal on your machine" (mods overview, "Where mods run") [read], and #99217 ("Mod ui.render
  (AbovePrompt/Pane) not drawn in desktop/iOS views of a Remote Control session") confirms hooks keep
  running on the host while the desktop view draws nothing [read].
- Scheduled and forked desktop sessions do have records here (167 records with `scheduledTaskId`, 1 with
  `forkedFromSessionId`, out of 993) [observed].
- Titles are a weak key: a commenter on #101545 runs "200+ Claude Code threads" where "Routine runs share
  titles" and "Titles are not ids" [read].

**Hypothesis C (footer too narrow)**: no precedent found; the only related finding is that the footer
chip names drift (2.1 above).

**Recommendation**

- **Prefer id over title (adapt the registry precedent).** Instead of the issue's idea "let the mod write
  `title` into the figures file and match titles", have the mod write `hostSessionId`
  (`CLAUDE_CODE_HOST_SESSION_ID`) into the file or use it as the file name, and have the overlay match
  the `local_` id from the Document URL (2.0). A session whose URL is not `epitaxy/local_…` is then known
  to be another kind and can show nothing on purpose [inference; the URL of a Remote Control row is
  unverified, open question 3].
- **Diagnose A cheaply.** For the shown `local_` id, look up the live engine in
  `~/.claude/sessions/*.json` by `hostSessionId`. If one exists and no figures file for it exists (or
  none written since `startedAt`), the mod is not running in that engine; compare `startedAt` with
  `installed_plugins.json` `installedAt` (`2026-10-15T12:03:14Z` for context-bar) to say "started before
  the mod was installed" [observed fields; inference for the rule]. Use the pid-reuse guard from
  claude-watch (check the pid's start time, `procStart`) [read].
- **Fix A without closing the session**: `/reload-plugins` in that session [read: docs and #96223,
  #100926]. The README's "close and reopen" advice can say so.

### 2.3 Issue #3 ("Context line shows the previous session's figures while a new session is starting")

**Precedents found**

- **Nobody borrows another entity's value.** Every tool read either hides, shows a placeholder, or keeps
  the same session's last value: claude-powerline hides segments "when native data is unavailable";
  ccstatusline prints `?` "when status data is unavailable" with a "stale-data fallback"; best-claude-hud
  keeps the last valid reading of the same session and shows `0% · 0 tokens` for a new one;
  Claude-Code-Usage-Monitor labels stale captures; ai-code-usage-tray keeps "the last figures ... marked
  with their age"; edenaion's VS Code bar hides idle and superseded sessions [read: READMEs]. The official
  status-line docs say `used_percentage` "may be `null` early in the session" and `current_usage` is
  `null` "before the first API call" and right after `/compact` [read].
- **Monitoring systems mark staleness instead of carrying values over**: Prometheus uses the newest
  sample "less than the lookback period ago" (5 minutes by default) and marks a series stale "soon after"
  its target is removed [read: Prometheus querying basics].
- **The store's state during start**: on this machine (MSI) all 22 records without `cliSessionId` are
  `transcriptUnavailable: true` (17 never focused, 5 focused), i.e. old sessions whose pointer was nulled
  as described in #92825 ("[BUG] Claude Code Desktop: session transcripts silently become permanently
  unavailable (cliSessionId nulled, no local recovery path)"), not sessions that were starting [observed;
  read]. So the issue's evidence line "MSI's store holds 22 records with no `cliSessionId`" does not show
  the starting state; whether a new record lacks `cliSessionId` for a few seconds is still unverified.
  magpie's reader notes that "a record with no cliSessionId runs under its sessionId without
  `local_`" (desktop 2.7032) [read: source comment]; one record here has `cliSessionId` equal to its
  `local_` uuid [observed].
- Related "Starting session" reports exist for remote sessions only: #88998 ("[BUG] Remote session hangs
  on "Starting session…" — server logs connection, then 50–118s of silence, then SIGTERM and reconnect
  loop") [snippet: title only]. Auto-titling arrives late or not at all in some builds: #78415 ("[BUG]
  Session auto-naming stopped after Claude Desktop update — sessions stay "Untitled session"", closed)
  [snippet: title only]; 262 of 993 records here have no `titleSource` [observed].

**What they imply.** The issue's proposal ("never fall back to a different session's figures", tests,
README fix) matches every precedent. The open question in the issue ("can the page title be read during
Starting session?") becomes less important if the overlay keys on the URL's `local_` id: the id exists as
soon as the route does, before any title or `cliSessionId` [inference; open question 2].

**Recommendation**

- Keep the issue's proposal: in `FocusedSessionFinder.Pick` (`FocusedSessionFinder.cs:71-98`) drop the
  newest-of-all fallback (`return titled ?? any`, line 97) at least whenever a page title or URL is
  known, and add the two tests the issue lists.
- Then adapt 2.0: resolve the shown session by `local_` id from the URL; with no figures for that id yet,
  draw the placeholder (`—  — / 1M`, as the mod already does for "no reading") or nothing; never another
  id's file.
- If the stale-but-same-session case matters (e.g. engine restarting), show the last value dimmed with
  its age rather than hiding it (ai-code-usage-tray, Claude-Code-Usage-Monitor) [read; inference for the
  styling].

### 2.4 The possible 4th problem: the engine session id changes inside one desktop session

**Precedents and documentation found**

- **`/clear` assigns a new session id.** `CLAUDE_CODE_SESSION_ID` "is updated on `/clear`" (env-vars
  docs); OpenTelemetry's `event.sequence` "keeps counting across `/clear`, which assigns a new
  `session.id`" (monitoring docs); `SessionStart` hooks get `source` `startup`, `resume`, `clear`,
  `compact` or `fork` (hooks docs) [read]. The status-line docs say cost "Resets to $0 when `/clear` starts
  a new session" [read].
- **A mod does not see that through `session.start`.** "`session.start`: Once for each loaded mod, before
  the first prompt, and again after a reload of that mod. Not after `/clear`, `/resume`, or `/branch`";
  `session.end` fires on those with `e.reason` `clear` or `resume` (mods reference) [read]. `$.state`
  values reset on `/clear`, `/resume`, `/branch` (interface docs) [read].
- **The documented pattern**: "Load a saved value again after `/clear`" uses
  `on('classic.SessionStart', { source: ['clear', 'resume', 'fork'] }, ...)` next to `session.start`
  (interface docs) [read]. A matcher is required for a second registration of an event: an event allows
  "only one matcher-less on() registration per plugin" (#101450, "[DOCS] Mods: document that each event
  allows only one matcher-less on() registration per plugin") [read].
- **The desktop record follows the change.** Desktop record readers name the lineage fields
  `preClearCliSessionId` and `priorCliSessionIds` next to `cliSessionId` and `unarchivedCliSessionId`
  (magpie) [read]; "When a conversation runs out of context or is rewound, Claude Desktop carries on in a
  new transcript and lists the earlier ones in `priorCliSessionIds`" (ai-code-usage-tray) [read];
  ai-profiles: "the app starts a new transcript when it can't resume the last" [read]. Here 4 of 993
  records have `priorCliSessionIds` [observed].
- **Engine ids can even collide.** #102198 shows three concurrent desktop engines (SSH environment) with
  one `sessionId` and different `hostSessionId` values, and a restart that reuses the id [read].

**What it means for Claude Code Plus** [read: code; inference for the runtime effect]: `register.tsx`
keeps the id in a module variable set once (`let session`, line 147; `learnSession`, lines 157-168,
called from `session.start`, lines 290-305). After `/clear` or `/resume` in the same engine the mod goes on
writing `<old id>.json`, while the desktop record's `cliSessionId` moves to the new id, so the overlay's
title match finds the new id and no file: the line disappears for that session until the mod reloads.
The `isHidden` and `placement` atoms (lines 28-30) also reset on `/clear`, and are re-read from `$.store`
only in `session.start` (lines 298-302), so `/ctxbar off` would silently turn back on after `/clear`.
Whether `$.session.id()` itself returns the new id after `/clear` is **unverified**.

**Recommendation**

- **Reuse the documented pattern**: add `on('classic.SessionStart', { source: ['clear', 'resume',
  'fork'] }, …)` that re-runs `learnSession`, reloads `isHidden`/`placement` from `$.store`, refreshes and
  publishes; or read `$.session.id()` at every `publish` [read: docs; inference for the code].
- **Key the file by the desktop id**: write `<configDir>\context-bar\<hostSessionId>.json` (or include
  `hostSessionId` and keep the engine id inside), since `hostSessionId` stays fixed across `/clear` and
  differs even when engine ids collide (#102198) [observed env; read issue; inference for stability across
  `/clear`, open question 4].
- **Overlay fallback while both exist**: when matching by engine id, also accept a file named by any
  id in the record's `priorCliSessionIds`/`preClearCliSessionId` only if it is newer than the current
  id's file; otherwise hide [inference].

---

## 3. Best practices distilled

1. **Identify the session by an id, not by its title.** Titles repeat and arrive late; ids do not
   (#101545 comment "Titles are not ids"; dashboard and deep links key on the `local_` id) [read]. Here
   the id is readable from the page's UIA value [observed] and from the engine's environment [observed].
2. **Never show one entity's figures under another.** Hide, show a placeholder, or keep the same entity's
   last value marked stale (claude-powerline, ccstatusline, best-claude-hud, ai-code-usage-tray,
   Claude-Code-Usage-Monitor; Prometheus staleness) [read].
3. **Follow the id through `/clear`, `/resume`, `/branch`.** A mod hooks `classic.SessionStart` with
   `source: ['clear', 'resume', 'fork']` next to `session.start`, and reloads `$.state` from `$.store`
   there (mods interface docs) [read].
4. **Use the engine's own figures, without a breakdown, after each turn.** `$.session.usage()` equals the
   status line's figures and is free without a breakdown (token-weather README) [read]; transcript parsing
   must guess the window from the model id (edenaion README) [read].
5. **Treat local stores as undocumented and changing.** agent-git refuses to write `local_*.json` because
   "The format is unstable, and it is migrating" [read]; hatohato-lab designs its reader so a format
   change only blanks the values [read]. Read-only, tolerant parsing, and the supported interface
   (`claude agents --json`) where one exists [read: agent-view docs].
6. **Guard pid-keyed data against pid reuse.** Check the process start time as well as the pid
   (claude-watch; the registry carries `procStart`) [read; observed].
7. **Plugins load at session start; tell users about `/reload-plugins`.** Documented for marketplace
   installs and env-var plugin folders alike [read: plugin loading docs, mods overview].
8. **Prefer accessible names with structural fallbacks over generated ids**, and scope searches to a
   container (Microsoft "Use the AutomationID Property") [read]. Generated React/Base UI ids and Tailwind
   class strings are not anchors [observed].
9. **Make anchor failures diagnosable.** Dump the candidate names when a lookup fails (pywinauto
   `print_control_identifiers()`) and record the app version with them [read; inference for the version].
10. **Expect Chromium accessibility to be lazy.** Electron enables accessibility "automatically ... in the
    presence of assistive technology" and otherwise keeps it off (Electron docs) [read]; Chromium turns on
    fuller modes when a UIA client asks for properties such as Name, ControlType, AutomationId
    (`NotifyAPIObserverForPropertyRequest` in `ax_platform_node_win.cc`) [read: source]. The first probe
    after an app start can see a thin tree [inference].

---

## 4. Open questions for the owner

1. **Does #1 still reproduce on 2.37337.1?** With no folder picked, is the "Environment: …" chip present
   on the new-session composer? One UIA dump of that screen (button names and bounds) settles it.
2. **What is the Document's URL (ValuePattern) on the new-session screen and during "Starting
   session"?** If it already shows `epitaxy/local_<uuid>` while the title is blank, the URL key solves #3
   by itself. Same dump as question 1, plus one taken right after the first prompt.
3. **What URL does a Remote Control row, a cloud session and a scheduled run show?** Needed to tell "not a
   local session" apart from "mod not loaded" (#2 hypothesis B).
4. **After `/clear` in a throwaway desktop session**: does `CLAUDE_CODE_HOST_SESSION_ID` stay the same,
   does `~/.claude/sessions/<pid>.json` get the new `sessionId`, does the record get
   `preClearCliSessionId`, and does `$.session.id()` return the new id? This confirms the 4th problem
   and the fix.
5. **Can a mod read `CLAUDE_CODE_HOST_SESSION_ID` through `$.env.get`?** A one-line `/ctxbar` debug print
   would show it.
6. **Split view**: when two sessions are shown side by side in one window, are there two session
   Documents, and which one is "in front"? (The app has split panes; not probed.)
7. **Is reading `~/.claude/sessions/*.json` acceptable**, given it is undocumented, or should the overlay
   call the bundled engine's `claude agents --json` (supported, but a process start per poll)?
8. **For the "Gemini tab reload investigation" session in #2**: does it have a live engine in
   `~/.claude/sessions/` with `hostSessionId`, and what is its `startedAt` against the context-bar
   `installedAt` (`2026-10-15T12:03:14Z`)? That decides hypothesis A vs B in a minute.
9. **Localization**: does the Code tab translate "Model: ", "Environment: ", "Add another folder" when the
   app language changes? If yes, the name tables need a locale key.
10. **Mod draw fallback**: is a band above the prompt acceptable as a fallback when the overlay is not
    running, given #102249 hides it while the next-step box is shown?

---

## 5. Sources

Accessed 2026-10-15. "read" = verified by reading (the whole page or the relevant section); "snippet" =
seen only in a search result or summary; "observed" = local read-only probe.

### Official documentation (code.claude.com)

| URL | Title | Status |
|---|---|---|
| https://code.claude.com/docs/en/statusline | Customize your status line | read (fields, update triggers, null cases) |
| https://code.claude.com/docs/en/plugins/mods/overview | Mods overview | read (install, `/reload-plugins`, "Where mods run") |
| https://code.claude.com/docs/en/plugins/mods/create | Create a mod | read (hot reload, dev-mods folder) |
| https://code.claude.com/docs/en/plugins/mods/reference | Mods reference | read (session events, `$.session`, `$.env`) |
| https://code.claude.com/docs/en/plugins/mods/interface | Draw in the interface with a mod | read (Keep state, reload after `/clear`) |
| https://code.claude.com/docs/en/plugins/mods/troubleshoot | Troubleshoot a mod | read (debug log, `hooks module ... not loaded:`) |
| https://code.claude.com/docs/en/plugins/mods/events | Mod events | read (one registration per matcher) |
| https://code.claude.com/docs/en/plugins/mods/api | Mods API | read (commands, timers) |
| https://code.claude.com/docs/en/plugins/loading | Plugin loading | read (session start, in-place vs copied, `@inline`) |
| https://code.claude.com/docs/en/plugins/install | Install and manage plugins | read (`/reload-plugins` notices) |
| https://code.claude.com/docs/en/plugins/cli-reference | Plugin CLI reference | read (`/reload-plugins`, `installPath`/`readFromFolder`) |
| https://code.claude.com/docs/en/plugins-reference | Plugins reference | read (grep for loading rules) |
| https://code.claude.com/docs/en/env-vars | Environment variables | read (`CLAUDE_CODE_PLUGIN_DIRS`, `CLAUDE_CODE_PLUGIN_DIR_WATCH`, `CLAUDE_CODE_SESSION_ID`) |
| https://code.claude.com/docs/en/hooks | Hooks reference | read (`SessionStart` sources) |
| https://code.claude.com/docs/en/sessions | Manage sessions | read (`/clear`, `/branch`) |
| https://code.claude.com/docs/en/desktop | Desktop application | read (usage ring, CLI comparison) |
| https://code.claude.com/docs/en/remote-control | Remote Control | read (desktop and session list) |
| https://code.claude.com/docs/en/agent-view | Agent view | read (`claude agents --json`) |
| https://code.claude.com/docs/en/cross-session-messaging | Cross-session messaging | read (grep only) |
| https://code.claude.com/docs/en/monitoring-usage | Monitoring usage (OpenTelemetry) | read (metrics, `/clear` and `session.id`) |
| https://code.claude.com/docs/en/vs-code | VS Code extension | snippet (context indicator) |

### anthropics/claude-code issues (state on 2026-10-15)

| # | Title | State | Status |
|---|---|---|---|
| 102094 | [BUG] Desktop app: context-window usage no longer shown anywhere — ring and usage popover list only plan windows | open | read, with comments |
| 102279 | [BUG] Desktop: the usage ring shows the account's session limit, not the window's context, and the per-session context line is gone from the usage popup | open | read |
| 102288 | Desktop app: context usage now takes up to 3 clicks to see (session donut switched to weekly usage, context moved to Session Rail) | open (labelled duplicate) | read, with comments |
| 102254 | Desktop app: context usage now takes 3 clicks instead of a glance | open | read |
| 102230 | [BUG] Session Rail context progress bar does not update | open | read |
| 90708 | Desktop app: bring back the context-usage indicator (or make the footer widget configurable) | open | read (body, first comment) |
| 91712 | Let the Code tab's usage ring report session context, not only the 5-hour window | closed (completed) | read (body) |
| 91075 | Desktop app: compact context indicator silently changed meaning — now tracks plan usage, not context window | closed (completed) | read (body) |
| 20041 | [Feature Request] Context usage indicator for Claude Desktop | closed (not planned) | read (body) |
| 101640 | Mods (function-hook plugins) load but never run in desktop Code tab; CLAUDE_CODE_PLUGIN_DIRS ignored | open | read |
| 96223 | [BUG] Desktop sessions load the install-time cache copy of local-directory marketplace plugins instead of loading in place | open | read, with comment |
| 102240 | Seeded plugins need a manual /reload-plugins in every fresh session | open | read |
| 100926 | `$.config.list()` / `$.config.set` have no plugin `userConfig` rows until `/reload-plugins` in desktop-hosted and headless sessions | open | read |
| 99130 | Mods off remotely on 2.1.288 despite docs saying on by default since 2.1.287 (rollout switch serves off) | open | read, with comments |
| 99217 | Mod ui.render (AbovePrompt/Pane) not drawn in desktop/iOS views of a Remote Control session | open | read, with comments |
| 102249 | [BUG] Desktop Code tab (2.37337.1): mod AbovePrompt band is hidden while the next-step box is shown | open | read |
| 99423 | VS Code extension: mods (plugin UI hooks) do not draw status line / toast / panes | open | read |
| 94424 | Function hooks: an event when session usage or a rate-limit window changes, so trackers do not have to poll | open | read (body start) |
| 101450 | [DOCS] Mods: document that each event allows only one matcher-less on() registration per plugin | open | read |
| 100150 | [BUG] Plugin-dir watcher idle poll walks the whole CLAUDE_CODE_PLUGIN_DIRS folder on the main thread every 30 s, freezing input | open | read (body start) |
| 24057 | MCP servers, hooks, and plugins should auto-reload when config changes — no restart needed | open | read (body start) |
| 32399 | [Feature Request] Add plugin hot-reload capability without session restart | closed (duplicate of #18174) | read |
| 101326 | Sessions started in terminal / Remote Control aren't visible in the Claude desktop app (and vice versa) | open (labelled duplicate) | read |
| 101862 | [BUG] /desktop on a terminal session with Remote Control on leaves two Desktop sidebar rows for one conversation | open | read |
| 101545 | [BUG] Desktop: sidebar right-click lost "Copy session ID"; "Copy link" gives the app's local_ id, not the CLI session id | open | read, with comment |
| 92825 | [BUG] Claude Code Desktop: session transcripts silently become permanently unavailable (cliSessionId nulled, no local recovery path) | open | read (body start) |
| 80773 | [BUG] claude://resume duplicates a session already open as a native Desktop tab; no deep link to focus an existing native session | open | read (body start) |
| 90035 | [FEATURE] Un-gate claude://code/continue?session=<local_id> — validation and lookup are already implemented | open | read (body start) |
| 102198 | [BUG] Desktop (SSH remote) restart spawns a fresh CLI with the same session id instead of resuming; one session id shared by concurrent sessions across all cwds | open | read |
| 89871 | [BUG] Desktop sidebar: `lastActivityAt` is persisted for a turn that never happened, so an idle session outranks live ones forever and a restart does not fix it | open | read (body start) |
| 88998 | [BUG] Remote session hangs on "Starting session…" | open | snippet (title) |
| 78415 | [BUG] Session auto-naming stopped after Claude Desktop update — sessions stay "Untitled session" | closed | snippet (title) |
| 35257 | Accessibility: 22 unlabeled icon-only buttons missing aria-labels (WCAG 4.1.2) | closed | snippet (title) |
| 71583, 71587, 99905, 99906 | mobile/web context display; desktop session status; prompt_cache and per-model cost in `$.session.usage()` | various | snippet (titles) |

No relevant issues were found in anthropics/claude-plugins-official for "context", "statusline", "desktop",
"reload" beyond unrelated plugin bugs [read: search result titles].

### Third-party repositories (READMEs or files read through the GitHub API)

| URL | What was read | Status |
|---|---|---|
| https://github.com/sirmalloc/ccstatusline | README | read |
| https://github.com/jarrodwatts/claude-hud | README | read |
| https://github.com/ccusage/ccusage | README | read |
| https://github.com/Owloops/claude-powerline | README | read |
| https://github.com/Haleclipse/CCometixLine | README | read |
| https://github.com/chongdashu/cc-statusline | README (head) | read |
| https://github.com/rz1989s/claude-code-statusline | README (grep) | read |
| https://github.com/GaoSSR/best-claude-hud | README | read |
| https://github.com/stricttools/howmuchleft | README | read |
| https://github.com/Maciek-roboblog/Claude-Code-Usage-Monitor | README (grep) | read |
| https://github.com/edenaion/claude-context-bar | README, How It Works | read |
| https://github.com/hatohato-lab/claude-code-context-meter | README (Japanese) | read |
| https://github.com/JiahaoXu10Arthur/context-band | README | read |
| https://github.com/fredrikbonde/claude-context-bar | README | read |
| https://github.com/ContextLab/claude-context-bar | README | read |
| https://github.com/justerification/claude-code-context-meter | README | read |
| https://github.com/lrdub/claude-code-context-meter-mod | README | read |
| https://github.com/k-wolfe99/claude-context-bar | README | read |
| https://github.com/Arunjay4213/claude-mods | README | read |
| https://github.com/anthropics/claude-code-playground | README; `claude-code/mods/token-weather/README.md` | read |
| https://github.com/saime428/ai-code-usage-tray | README; `lib/usage.js` | read |
| https://github.com/rashid2003/claude-watch | `Sources/WatchCore/TerminalSessions.swift` | read |
| https://github.com/lack435/claude-streamdeck | `docs/spike-findings.md` | read |
| https://github.com/yetone/magpie | `internal/sessions/desktop_titles.go` | read |
| https://github.com/bartekczyz/ai-profiles | `apps/ai-profiles/src-tauri/src/sessions/claude/desktop.rs` | read (doc comments) |
| https://github.com/Einsia/agent-git | `docs/mechanism-probing/desktop-apps.md` | read (relevant sections) |
| https://github.com/digorgonzola/claude-code-dashboard | `lib/desktop.js` | read |

### Chromium, Electron, Microsoft and other references

| URL | Title | Status |
|---|---|---|
| https://github.com/chromium/chromium/blob/b46ad502002301bc51ea8b485316a919c84c8c06/ui/accessibility/platform/ax_platform_node_win.cc | Chromium `AXPlatformNodeWin` (AutomationId, ClassName, ValuePattern for documents, AX-mode triggers) | read (relevant functions) |
| https://github.com/chromium/chromium/blob/b46ad502002301bc51ea8b485316a919c84c8c06/ui/accessibility/platform/ax_platform_node_delegate.cc | `GetAuthorUniqueId` returns `kHtmlId` | read |
| https://github.com/chromium/chromium/blob/b46ad502002301bc51ea8b485316a919c84c8c06/third_party/blink/renderer/modules/accessibility/ax_node_object.cc | Root web area name from `document->title()` | read (relevant lines) |
| https://learn.microsoft.com/en-us/dotnet/framework/ui-automation/use-the-automationid-property | Use the AutomationID Property | read |
| https://learn.microsoft.com/en-us/dotnet/api/system.windows.automation.valuepattern.current | ValuePattern.Current Property | snippet |
| https://www.electronjs.org/docs/latest/tutorial/accessibility | Electron: Accessibility | read |
| https://playwright.dev/docs/api/class-electron | Playwright: Electron | read |
| https://pywinauto.readthedocs.io/en/latest/getting_started.html | pywinauto: Getting Started | read |
| https://prometheus.io/docs/prometheus/latest/querying/basics/ | Prometheus: Querying basics (staleness) | read |
| https://docs.activitywatch.net/en/latest/watchers.html | ActivityWatch: Watchers | read (no mechanism given for browser URLs) |
| https://bugreports-test.qt.io/browse/QTBUG-99485 | Qt bug: Chromium AutomationId equals HTML id | snippet |
| https://community.blueprism.com/t5/Product-Forum/UI-Mode-and-UIA-Automation-ID/m-p/87798 | Blue Prism: UIA Automation ID equals HTML id | snippet |

### Local observations (MSI, read-only)

| What | How | Status |
|---|---|---|
| Document URL `https://claude.ai/epitaxy/local_<uuid>`, AutomationIds `_r_…_`, ClassNames | `%TEMP%\ccp-research-precedents\uia_peek.ps1`, `uia_peek2.ps1` (UIA reads only) | observed |
| Engine env `CLAUDE_CODE_HOST_SESSION_ID`, `CLAUDE_CODE_SESSION_ID`, `CLAUDE_CODE_DESKTOP_APP_VERSION` | this subagent's Bash environment (inherited from the desktop engine, pid 34536) | observed |
| `~/.claude/sessions/<pid>.json` fields and 1:1 match with store records | `sessions_peek.py` | observed |
| Store statistics (993 records; 22 without `cliSessionId`, all `transcriptUnavailable`; 4 with `priorCliSessionIds`) | `store_stats.py` (no titles printed) | observed |
| Engine strings `CLAUDE_CODE_PLUGIN_DIRS`, `CLAUDE_CODE_HOST_SESSION_ID`, `updatePidFile`, `/reload-plugins` notices | `grep -a` on `%APPDATA%\Claude\claude-code\2.1.295\…\claude.exe` | observed |
| `.in_use/<pid>` markers in copied plugin caches, none for context-bar | directory listing of `~\.claude\plugins\cache` | observed |
| 43 locale files in the app's `resources` | directory listing of the MSIX package | observed |

