/** The status line's context figures as the bar last saw them. */
export type ContextReading = {
  /** Input tokens of the last response (uncached + cache-written + cache-read); null before the first response. */
  tokens: number | null
  /** The model's context window, in tokens. */
  window: number
  /** `tokens` over `window` as the engine rounds it, 0..100; null with no reading. */
  percent: number | null
  /** The token count at which auto-compaction runs; null when unknown or off. */
  threshold: number | null
  /** When the figures were read, `$.clock.now()` milliseconds. */
  at: number
}

/**
 * The figures file's JSON: what the mod publishes to
 * `<config dir>\context-bar\<host session id, else session id>.json` after every change, and
 * what the Claude Code Plus overlay reads for the session the app shows.
 */
export type PublishedFigures = ContextReading & {
  /** The engine's session id (the transcript's name); a new one after /clear, /resume or /branch. */
  sessionId: string
  /**
   * The desktop app's id of the session (`local_<uuid>`, `CLAUDE_CODE_HOST_SESSION_ID`): the
   * file's name, fixed for the session's life; null in a session the desktop app did not start.
   */
  hostSessionId: string | null
  /** The session's working folder. */
  cwd: string
  /** The session's model. */
  model: string
  /** Whether the person hid the bar (`/ctxbar off`). */
  isHidden: boolean
}

/**
 * Where the bar is drawn: nowhere in the app (`overlay`: the Claude Code Plus footer line
 * reads the published file), the prompt footer's mode-label slot, or the band above the prompt.
 */
export type Placement = 'overlay' | 'footer' | 'top'

declare module 'claude-code' {
  interface PluginState {
    'context-bar': {
      reading: ContextReading | null
      isHidden: boolean
      placement: Placement
    }
  }
}
