## Summary

After `/clear`, `/resume` or `/branch` in a desktop session the engine goes on under a **new session id**, but the context-bar mod keeps publishing to the file of the **old** id. The desktop record's `cliSessionId` moves to the new id, so the overlay finds no figures file for the session in front and hides the line (or shows a stale file, if one existed under the new id). Not yet reproduced live: derived on 2026-10-15 from the official documentation and the code.

## Documentation

- Mods reference, Session table: `session.start` fires "Once for each loaded mod, before the first prompt, and again after a reload of that mod. Not after `/clear`, `/resume`, or `/branch`." After those commands the process continues under another session id and `$.state` resets to its defaults (mods troubleshoot page, "A value resets after `/clear`, `/resume`, or `/branch`"; the `SessionEndInput` type: "`clear` is how a hook sees a `/clear`: the conversation ends, the process goes on under a new session id, and no `session.start` fires for it").
- `CLAUDE_CODE_SESSION_ID` "is updated on `/clear`" (environment variables page).
- The documented pattern for following the change: `on('classic.SessionStart', { source: ['clear', 'resume', 'fork'] }, …)` (mods interface page, "Load a saved value again after `/clear`"). A matcher is required for a second registration of the same event.
- The desktop app records the old engine id in the session record's `priorCliSessionIds` (and `preClearCliSessionId`); 4 of 993 records on one machine show it.

## Effect in `mods/context-bar/hooks/register.tsx`

- `session` (id, cwd, model, file path) is learned once, in `session.start` (`register.tsx:147`, `:157-168`, `:290-305`), and never again.
- After `/clear` the `reading` atom resets to null, so `publish` skips until the next measurement, then writes the new conversation's figures to `<old id>.json`.
- The `isHidden` and `placement` atoms reset to their defaults too and are re-read from `$.store` only in `session.start`, so `/ctxbar off` silently turns back on after `/clear`.
- The overlay (`FocusedSessionFinder`) keys on the record's current `cliSessionId`: no file under it, no line. Falling back to `priorCliSessionIds` would show the previous conversation's figures, the same error as #3 (Context line shows the previous session's figures while a new session is starting).

## Proposed fix

1. Add `on('classic.SessionStart', { source: ['clear', 'resume', 'fork'] }, …)` that re-runs `learnSession`, reloads `isHidden` and `placement` from `$.store`, refreshes and publishes.
2. In `publish`, compare `await $.session.id()` with the cached id and re-learn on a difference.
3. Key the figures file by the desktop session id instead of the engine id. The engine of a desktop session runs with `CLAUDE_CODE_HOST_SESSION_ID=local_<uuid>` (readable with `$.env.get`; the name must be a string literal), which is the desktop record's id and is expected to stay fixed across `/clear` (to verify, below). Fall back to the engine id when the variable is absent (a terminal session); keep both ids inside the file. The overlay then never sees the change. The same `local_` id is readable from the app's page document, see #3.
4. Tests in `mods/context-bar/tests/bar.test.ts`: after a `classic.SessionStart` with `source: 'clear'` and `session.id` answering a new id, the next write carries the new engine id; `isHidden` stays true when the store says so.

## To check before fixing

In a throwaway desktop session, before and after `/clear`: does `CLAUDE_CODE_HOST_SESSION_ID` stay the same; does `~\.claude\sessions\<pid>.json` (the engine's live registry: `hostSessionId`, `sessionId`, `name`, `status`, `startedAt`) get the new `sessionId`; does the store record get `preClearCliSessionId`; what do `$.session.id()` and `$.env.get('CLAUDE_CODE_HOST_SESSION_ID')` return inside the mod (a temporary debug print in `/ctxbar`).

## Relation to the other issues

- #2 (Context line missing in a session): a session that ran `/clear` after the mod loaded looks exactly like hypothesis A or B there (no file under the id the overlay looks for).
- #3: the structural fix proposed there (key on the desktop session id from the page document's URL) covers this issue on the overlay side; the mod-side hook is still needed for the in-app state (`/ctxbar off`, placement).

## Environment

Windows 11, Claude desktop 2.37337.1 (packaged), engine 2.1.295, overlay build `bcf9b05`, mod `context-bar` 0.1.0. Documentation read on 2026-10-15 (mods reference as of v2.1.290, changelog to 2.1.300, mod API types written by engine 2.1.295).
