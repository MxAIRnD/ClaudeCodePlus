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
