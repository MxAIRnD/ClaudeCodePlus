Research notes, 2026-10-15 (official docs, precedents in other Claude Code tools, read-only checks on the MSI laptop).

**The facts above are from the other laptop, not MSI.** MSI's store holds 992-993 `local_*.json` records, not 2108; no record `68ec5dbf…`; none of the figures-file ids named above. Re-check there, in the affected session: `/ctxbar`, then the live registry described below.

**Hypothesis A (the mod is not loaded there): likely, documented, with a fix that needs no restart.**
- Plugins load at session start: "Changes to settings or to disk don't reach this layer until you run `/reload-plugins` or start a new session" (plugin loading reference). `CLAUDE_CODE_PLUGIN_DIRS` is read "for the session" the same way.
- `/reload-plugins` works in desktop sessions since 2.1.260 when typed into the session's own prompt box; it is refused when it arrives over Remote Control (plugin CLI reference).
- The desktop app can also reload open sessions' plugins by itself when `installed_plugins.json` changes (changelog 2.1.274; an `installedPluginsWatcher` in the app, behind a feature flag). On MSI a session that started before the 12:03Z install does publish figures, which fits that or a restart.
- Test inside the session: `/ctxbar` ("Unknown command" = not loaded; `/plugin` does not exist in the desktop). Test from outside: the engine's `startedAt` in `~\.claude\sessions\<pid>.json` (an undocumented live registry with `hostSessionId`, `sessionId`, `name`, `status`, `startedAt`, `procStart`) against `installedAt` in `~\.claude\plugins\installed_plugins.json`.

**Hypothesis B (no store record): contradicted for Remote Control, and moot with an id-based mapping.**
- Sessions with Remote Control on do get records: 798 of 993 records on MSI carry `bridgeSessionIds`.
- Sessions started in a terminal, and Remote Control rows a terminal session registered, have no desktop record (anthropics/claude-code#101326, sessions started in terminal / Remote Control are not visible in the desktop app; anthropics/claude-code#101862, `/desktop` on a terminal session leaves two sidebar rows).
- B alone cannot explain "no figures file": the mod writes its file whatever the store holds. "No file and no record" points to A, or to a mod writing elsewhere (an SSH session writes on the remote machine; another `CLAUDE_CONFIG_DIR`).

**Hypothesis C (layout): no evidence either way.** The `SessionMode` footer slot is a documented in-app fallback (`/ctxbar footer`) that layout cannot defeat.

**The title-in-figures idea (step 4 above) does not work.** A mod reaches only the custom title (`session_title` in the classic `SessionStart` / `UserPromptSubmit` hook input, present after `--name`, `/rename` or a hook's `sessionTitle`). The auto-generated title, which most desktop sessions show (`titleSource: auto` in 710 of 993 records), is not exposed to mods.

**Better: map by id, not by title.** The app's page document (the UI Automation `Document` element named `<title> - Claude Code`) has a `ValuePattern` value of `https://claude.ai/epitaxy/local_<uuid>`, and `local_<uuid>` is the desktop store record's id. The engine of that session runs with `CLAUDE_CODE_HOST_SESSION_ID=local_<uuid>`. If the mod publishes under that id and the overlay reads it from the document's URL, the title and the store leave the mapping: a session with no `local_` URL is known to be another kind and shows nothing on purpose; a `local_` id with no file means the mod is not running in that session, and can be logged as such. Details in #3 (Context line shows the previous session's figures while a new session is starting) and #4 (Figures file goes stale after /clear, /resume or /branch).

**#4 can look like this issue:** a session that ran `/clear`, `/resume` or `/branch` after the mod loaded keeps writing under the old engine id while the record names the new one.

**Proposed:** a README paragraph "Line missing in a session" (`/ctxbar`; `/reload-plugins` typed in the session, not over Remote Control; or a new session); the id-based mapping with a diagnostic log line; close B as "not reproduced" unless the check on the other laptop shows a record-less local session.
