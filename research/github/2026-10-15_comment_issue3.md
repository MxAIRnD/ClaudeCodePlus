Research notes, 2026-10-15 (official docs, precedents in other Claude Code tools, read-only UI Automation probes on the MSI laptop with app 2.37337.1).

**Confirmed, and the proposed rule matches every precedent.** The mod API types say a session's context `tokens` and `percent` are "absent until the first response of the live window: a fresh session, or one just compacted"; the status-line docs say the same of `used_percentage` and `current_usage`. So for a starting session the only right outputs are "—" or nothing. Every tool read (claude-powerline, ccstatusline, best-claude-hud, ai-code-usage-tray, Claude-Code-Usage-Monitor) hides, shows a placeholder, or keeps the same session's last value marked stale; none borrows another session's value.

**One correction to the evidence above.** The 22 records without `cliSessionId` on MSI are all old sessions (the newest created 2026-05-30) with `transcriptUnavailable: true`, the state anthropics/claude-code#92825 describes (session transcripts silently become permanently unavailable, `cliSessionId` nulled). They are leftovers, not sessions caught while starting. Whether a new record lacks `cliSessionId` for a few seconds is still unverified; check 2 above stays necessary.

**The page title is readable at any moment.** Chromium names the UI Automation `Document` after `document.title` and raises a Name-changed event when it changes; the titled Document is present whenever a session is shown. What the app puts there during "Starting session" (the previous title, an empty one, or a generic one) is undocumented; check 1 above stays necessary.

**A structural fix: key on the session's id, not its title.** The same `Document` element has a `ValuePattern` value of `https://claude.ai/epitaxy/local_<uuid>`, where `local_<uuid>` is the desktop store record's id (verified twice on MSI; Chromium returns the document URL as a document's UI Automation value). The engine of that session runs with `CLAUDE_CODE_HOST_SESSION_ID=local_<uuid>` and `CLAUDE_CODE_SESSION_ID=<engine id>`, and keeps a live registry `~\.claude\sessions\<pid>.json` mapping the two. Plan:
- Mod: publish the figures under the host id (`$.env.get('CLAUDE_CODE_HOST_SESSION_ID')`), the engine id as the fallback, both ids inside the file.
- Overlay: `FooterProbe` caches `ValuePattern.ValueProperty` for Documents; `FooterLocator.ShownSessionId` parses `^https://claude\.ai/epitaxy/(local_[0-9a-f-]{36})$`; `ContextBarForm.ReadFigures` opens `<figuresRoot>\<local id>.json`. No title, no store record, no fallback: no `local_` URL means hidden; an id without a file means hidden (or "—", owner's call) and logged.
- `FocusedSessionFinder` retires, or stays as a fallback with the strict `Pick` proposed above.
- The hide, then "—", then figures sequence is the honest one, and the same `best-claude-hud` uses for a new session.

**Until the capture:** the patch proposed above stands as the interim (no fallback when a title is known; hide for a namesake without `cliSessionId`; the two tests; the README sentence). If the capture shows the page title stale during the start, a title match alone still picks the previous session for a few seconds, one more reason for the id key.

**Caveat:** the URL route `epitaxy/local_` is as undocumented as the title suffix, but it is user-facing: "Copy link" in the sidebar gives `claude://claude.ai/epitaxy/local_<uuid>` (anthropics/claude-code#101545).

Related: #4 (Figures file goes stale after /clear, /resume or /branch).
