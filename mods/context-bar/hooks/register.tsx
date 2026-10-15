// context-bar: the context window's fill, published for the Claude Code Plus
// footer line and, on request, drawn in the app itself.
//
// The engine's own status-line figures ($.session.usage(): the input side of
// the last response against the model's window) are written after every
// change to <CLAUDE_CONFIG_DIR or ~/.claude>/context-bar/<session>.json,
// where the Claude Code Plus overlay (ClaudeCodePlus.exe --button) reads them
// and draws a thin gradient line in the app's prompt footer, between the
// permission-mode chip and the model chip. The file is named by the desktop
// app's id of the session (CLAUDE_CODE_HOST_SESSION_ID, local_<uuid>: the id
// the app's page URL carries, so the overlay reads the shown session's own
// figures and nobody else's), or by the engine's session id in a session the
// desktop did not start; both ids are inside the file. The figures move after
// each model response of a turn (turn.step), at every measurement the engine
// pushes (session.measure) and when a turn ends. /clear, /resume and /branch
// move the engine to a new session id without a new session.start and reset
// $.state: classic.SessionStart tells, the session is learned again (the file
// keeps its name) and the saved placement and visibility are read again.
//
// In-app drawing is optional, for a session without the overlay: `/ctxbar
// footer` draws the line in the prompt footer's mode-label slot (desktop: an
// inline run of ━ glyphs, each colored for its place along the window, then
// the figures; terminal: one more dim label), `/ctxbar top` in the band above
// the prompt (desktop: an Svg line stretched to the band's width; terminal: a
// row of ━ cells), `/ctxbar overlay` (the default) nothing in the app.
// `/ctxbar off` hides it, in the app and in the overlay, and `/ctxbar on`
// shows it again. Placement and visibility are kept across sessions in
// $.store. `/ctxbar` alone prints the figures, the session's ids and the file.

import { atom, read, update } from 'claude-code'
import type { Elements, EngineInterface, Register, SessionContextUsage } from 'claude-code'

import type { ContextReading, Placement, PublishedFigures } from '../types'

const reading = atom({ plugin: 'context-bar', key: 'reading' } as const, null)
const isHidden = atom({ plugin: 'context-bar', key: 'isHidden' } as const, false)
const placement = atom({ plugin: 'context-bar', key: 'placement' } as const, 'overlay')

// ---------- pure helpers (tested in tests/bar.test.ts) ----------

/** The gradient along the window: a position (0..1) and the RGB triple there. */
export const STOPS: readonly (readonly [number, readonly [number, number, number]])[] = [
  [0, [34, 211, 238]], // cyan   #22d3ee
  [0.25, [34, 197, 94]], // green  #22c55e
  [0.5, [163, 230, 53]], // lime   #a3e635
  [0.65, [250, 204, 21]], // yellow #facc15
  [0.8, [251, 146, 60]], // orange #fb923c
  [1, [239, 68, 68]], // red    #ef4444
]

/** The band svg's own width in user units: far wider than any slot, so the desktop clamps it to the slot. */
export const BAND_UNITS = 4000
const SVG_HEIGHT = 8
/** The footer line's length in glyphs when the window is full. */
export const FOOTER_CELLS = 24

const clamp01 = (t: number): number => Math.min(1, Math.max(0, Number.isFinite(t) ? t : 0))

const hex2 = (n: number): string =>
  Math.round(Math.min(255, Math.max(0, n)))
    .toString(16)
    .padStart(2, '0')

/** The gradient's color at `t` (0..1), as `#rrggbb`. */
export const colorAt = (t: number): string => {
  const x = clamp01(t)
  let i = 0
  while (i < STOPS.length - 2 && x > STOPS[i + 1]![0]) i += 1
  const [p0, a] = STOPS[i]!
  const [p1, b] = STOPS[i + 1]!
  const f = p1 === p0 ? 0 : (x - p0) / (p1 - p0)
  const mix = (k: 0 | 1 | 2): string => hex2(a[k] + (b[k] - a[k]) * f)
  return `#${mix(0)}${mix(1)}${mix(2)}`
}

/** 840 → "840", 8400 → "8.4K", 84200 → "84K", 1000000 → "1M". */
export const fmtTokens = (n: number): string => {
  if (n < 1000) return `${Math.round(n)}`
  if (n < 10000) return `${(n / 1000).toFixed(1).replace(/\.0$/, '')}K`
  if (n < 1000000) return `${Math.round(n / 1000)}K`
  return `${(n / 1000000).toFixed(2).replace(/\.?0+$/, '')}M`
}

/** The figures file: `<configDir>\context-bar\<name>.json`, the name being the host session id, else the engine's session id. */
export const figuresPath = (configDir: string, name: string): string =>
  `${configDir.replace(/[\\/]+$/, '')}\\context-bar\\${name}.json`

/** The figures file's text: what the Claude Code Plus footer line reads. */
export const figuresJson = (
  session: { id: string; hostId: string | null; cwd: string; model: string },
  r: ContextReading,
  hidden: boolean,
): string => {
  const figures: PublishedFigures = {
    sessionId: session.id,
    hostSessionId: session.hostId,
    cwd: session.cwd,
    model: session.model,
    tokens: r.tokens,
    window: r.window,
    percent: r.percent,
    threshold: r.threshold,
    at: r.at,
    isHidden: hidden,
  }
  return JSON.stringify(figures)
}

/** One line naming the session's ids and its file, for /ctxbar. */
export const describeSession = (s: { id: string; hostId: string | null; file: string } | null): string =>
  s === null ? 'context-bar: session not learned yet.' : `context-bar: session ${s.id}, desktop ${s.hostId ?? 'none'}, file ${s.file}`

/**
 * The band's desktop line: a 2.5px rounded gradient line over `fraction` of
 * the drawing's width and nothing behind it (the gradient is pinned to the
 * whole width, so the tip's color reads the level), a blurred copy under it
 * for a soft glow, and a faint tick at `tick` (0..1) where auto-compaction
 * runs, when known.
 *
 * The markup is BAND_UNITS wide with `preserveAspectRatio="none"`: the desktop
 * sizes the box to the markup's width up to the slot, so the drawing is
 * stretched to exactly the slot's width, whatever that is in pixels.
 */
export const barSvg = (fraction: number, tick: number | null): string => {
  const w = clamp01(fraction)
  const pct = `${(w * 100).toFixed(2)}%`
  const stops = STOPS.map(([p, [r, g, b]]) => `<stop offset="${p}" stop-color="rgb(${r},${g},${b})"/>`).join('')
  const rect = (extra: string): string =>
    `<rect x="0" y="2.75" width="${pct}" height="2.5" rx="5" ry="1.25" fill="url(#cbg)"${extra}/>`
  const line = w > 0 ? rect(' filter="url(#cbf)" opacity="0.8"') + rect('') : ''
  const mark =
    tick !== null && tick > 0 && tick < 1
      ? `<rect x="${(tick * 100).toFixed(2)}%" y="0.5" width="3" height="7" fill="#94a3b8" fill-opacity="0.45"/>`
      : ''
  return (
    `<svg xmlns="http://www.w3.org/2000/svg" width="${BAND_UNITS}" height="${SVG_HEIGHT}" ` +
    `viewBox="0 0 ${BAND_UNITS} ${SVG_HEIGHT}" preserveAspectRatio="none">` +
    `<defs><linearGradient id="cbg" gradientUnits="userSpaceOnUse" x1="0" y1="0" x2="100%" y2="0">${stops}</linearGradient>` +
    `<filter id="cbf" x="-1%" y="-150%" width="102%" height="400%"><feGaussianBlur stdDeviation="2 1.1"/></filter></defs>` +
    line +
    mark +
    `</svg>`
  )
}

/** One line of the figures, for /ctxbar. */
export const describeReading = (r: ContextReading): string => {
  const used =
    r.tokens === null
      ? `no reading yet (window ${fmtTokens(r.window)})`
      : `${fmtTokens(r.tokens)} of ${fmtTokens(r.window)} (${Math.round((100 * r.tokens) / r.window)}%)`
  const compact = r.threshold === null ? '' : `, auto-compact at ${fmtTokens(r.threshold)}`
  return `context-bar: ${used}${compact}`
}

// ---------- the figures ----------

type SessionFacts = { id: string; hostId: string | null; cwd: string; model: string; file: string }

/**
 * The session's facts, read in session.start (a hot reload runs it again) and
 * again after /clear, /resume or /branch, when the engine's id changes.
 */
let session: SessionFacts | null = null

const toReading = (context: SessionContextUsage, old: ContextReading | null, at: number): ContextReading => ({
  tokens: context.tokens ?? null,
  window: context.window,
  percent: context.percent ?? null,
  threshold: context.breakdown?.autoCompactThreshold ?? old?.threshold ?? null,
  at,
})

async function learnSession($: EngineInterface): Promise<void> {
  try {
    const [id, cwd, model, host] = await Promise.all([
      $.session.id(),
      $.session.cwd(),
      $.session.model(),
      $.env.get('CLAUDE_CODE_HOST_SESSION_ID'),
    ])
    const configDir =
      (await $.env.get('CLAUDE_CONFIG_DIR')) ||
      `${(await $.env.get('USERPROFILE')) ?? (await $.env.get('HOME')) ?? ''}\\.claude`
    const hostId = host ? host : null
    session = { id, hostId, cwd, model, file: figuresPath(configDir, hostId ?? id) }
  } catch {
    session = null
  }
}

/** Writes the figures file for the overlay; a write that fails is left for the next change. */
async function publish($: EngineInterface): Promise<void> {
  if (session === null) return
  // After /clear, /resume or /branch the engine goes on under a new id: a changed one is learned again first.
  const id = await $.session.id().catch(() => null)
  if (id !== null && id !== session.id) await learnSession($)
  if (session === null) return
  const r = await read($, reading)
  if (r === null) return
  await $.fs.write(session.file, figuresJson(session, r, await read($, isHidden))).catch(() => undefined)
}

/** Keeps the figures and publishes them. */
async function setReading($: EngineInterface, context: SessionContextUsage): Promise<void> {
  const at = await $.clock.now()
  await update($, reading, old => toReading(context, old, at))
  await publish($)
}

/** Reads the status line's figures; `withThreshold` also asks where auto-compaction runs. */
async function refresh($: EngineInterface, withThreshold = false): Promise<void> {
  try {
    const usage = await $.session.usage(withThreshold ? { breakdown: 'summary' } : undefined)
    await setReading($, usage.context)
  } catch {
    // the bar keeps its last figures
  }
}

// ---------- drawing ----------

type BarParts = { fraction: number; tick: number | null; tip: string; pctText: string; counts: string }

const partsOf = (r: ContextReading): BarParts => {
  const fraction = r.tokens === null ? 0 : clamp01(r.tokens / r.window)
  return {
    fraction,
    tick: r.threshold !== null && r.threshold > 0 && r.threshold < r.window ? r.threshold / r.window : null,
    tip: colorAt(fraction),
    pctText: r.tokens === null ? '—' : `${Math.round(fraction * 100)}%`,
    counts: `${r.tokens === null ? '—' : fmtTokens(r.tokens)} / ${fmtTokens(r.window)}`,
  }
}

/**
 * The footer's inline run, one Text and nothing block-level, so the row keeps
 * it on its line: the filled part of FOOTER_CELLS ━ glyphs, each colored for
 * its place along the window, then the percent in the tip's color, the
 * counts, and the engine's own mode labels dim after them.
 */
const footerRun = (ui: Elements['desktop'], p: BarParts, modes: readonly string[]): JSX.Element => {
  const { Text } = ui
  const filled = Math.round(FOOTER_CELLS * p.fraction)
  const parts: JSX.Element[] = []
  for (let i = 0; i < filled; i += 1) parts.push(<Text color={colorAt((i + 0.5) / FOOTER_CELLS)}>━</Text>)
  if (filled > 0) parts.push(<Text> </Text>)
  parts.push(
    <Text bold color={p.tip}>
      {p.pctText}
    </Text>,
  )
  parts.push(<Text dimColor>{` ${p.counts}`}</Text>)
  if (modes.length > 0) parts.push(<Text dimColor>{` ${modes.join(' & ')}`}</Text>)

  return <Text>{parts}</Text>
}

/** The band's desktop row: the line as wide as the band, then the percent in the tip's color and the counts. */
const bandRow = (ui: Elements['desktop'], p: BarParts): JSX.Element => {
  const { Box, Svg, Text } = ui

  return (
    <Box flexDirection="row" alignItems="center" gap={1}>
      <Box flexGrow={1} flexShrink={1} minWidth={0}>
        <Svg source={barSvg(p.fraction, p.tick)} alt={`Context ${p.pctText} used, ${p.counts}`} height={SVG_HEIGHT} />
      </Box>
      <Text bold color={p.tip}>
        {p.pctText}
      </Text>
      <Text dimColor>{p.counts}</Text>
    </Box>
  )
}

/** The band's terminal row: one cell per column, colored for its place along the window. */
const terminalRow = (ui: Elements['terminal'], p: BarParts, bodyColumns: number): JSX.Element => {
  const { Box, Text } = ui
  const label = `${p.pctText} ${p.counts}`
  const cells = Math.max(8, bodyColumns - label.length - 3)
  const filled = Math.round(cells * p.fraction)
  const tickCell = p.tick === null ? -1 : Math.min(cells - 1, Math.round(cells * p.tick))
  const row: JSX.Element[] = []
  for (let i = 0; i < cells; i += 1) {
    if (i === tickCell) row.push(<Text dimColor>┊</Text>)
    else if (i < filled) row.push(<Text color={colorAt((i + 0.5) / cells)}>━</Text>)
    else row.push(<Text dimColor>╌</Text>)
  }

  return (
    <Box flexDirection="row" alignItems="center" gap={1}>
      <Box flexDirection="row" flexShrink={0}>
        {row}
      </Box>
      <Text bold color={p.tip}>
        {p.pctText}
      </Text>
      <Text dimColor>{p.counts}</Text>
    </Box>
  )
}

// ---------- hooks ----------

const PLACEMENTS: readonly Placement[] = ['overlay', 'footer', 'top']
/** The placement before any /ctxbar: nothing in the app, the overlay draws the line. */
const DEFAULT_PLACEMENT: Placement = 'overlay'

/** Reads the saved visibility and placement into the session's state (which /clear, /resume and /branch reset). */
async function loadPreferences($: EngineInterface): Promise<void> {
  const hidden = (await $.store.get('isHidden')) === true
  await update($, isHidden, () => hidden)
  const stored = await $.store.get('placement')
  const where = PLACEMENTS.find(p => p === stored) ?? DEFAULT_PLACEMENT
  await update($, placement, () => where)
}

/** The reading to draw at the site placement `at` owns, or null for nothing. */
async function drawable($: EngineInterface, at: Placement): Promise<BarParts | null> {
  if ((await read($, isHidden)) || (await read($, placement)) !== at) return null
  const r = await read($, reading)
  return r === null || r.window <= 0 ? null : partsOf(r)
}

export const register: Register = on => {
  on('session.start', async ($, e, next) => {
    await $.command.register({
      name: 'ctxbar',
      description: 'Context bar: /ctxbar [on | off | overlay | footer | top]',
      argumentHint: '[on | off | overlay | footer | top]',
    })
    // An earlier build pinned a status line under the prompt; a reload does not clear it.
    $.ui.status(undefined)
    await loadPreferences($)
    const result = await next(e)
    void learnSession($).then(() => refresh($, true))

    return result
  })

  // /clear, /resume and /branch: the engine goes on under a new session id,
  // $.state is reset and session.start does not fire again; this does (a
  // compaction keeps the id and the state, and is left out).
  on('classic.SessionStart', { source: ['clear', 'resume', 'fork'] }, async ($, e, next) => {
    try {
      await loadPreferences($)
      await learnSession($)
      void refresh($, true)
    } catch {
      // the next publish learns the session again; the preferences keep their defaults
    }

    return next(e)
  })

  // The engine's own push: after each main-thread turn.
  on('session.measure', async ($, e, next) => {
    if (e.changed.includes('context')) await setReading($, e.context)

    return next(e)
  })

  // Each later model request of a turn follows a response: the figures moved.
  // The event streams, so the hook is a generator that forwards the stream.
  on('turn.step', async function* ($, e, next) {
    if (e.agentId === undefined && e.index > 0) void refresh($)

    return yield* next(e)
  })

  on('turn.complete', ($, e, next) => {
    void refresh($)

    return next(e)
  })

  on('command.run', { command: 'ctxbar' }, async ($, e) => {
    const arg = e.args.trim().toLowerCase()
    if (arg === 'on' || arg === 'off') {
      const hidden = arg === 'off'
      await $.store.set('isHidden', hidden)
      await update($, isHidden, () => hidden)
      await publish($)

      return { text: hidden ? 'context-bar: hidden; /ctxbar on shows it again.' : 'context-bar: shown.' }
    }
    const where = PLACEMENTS.find(p => p === arg)
    if (where !== undefined) {
      await $.store.set('placement', where)
      await update($, placement, () => where)

      await publish($)

      return {
        text:
          where === 'top'
            ? 'context-bar: in the band above the prompt.'
            : where === 'footer'
              ? 'context-bar: in the prompt footer, the row under the prompt box.'
              : 'context-bar: nothing in the app; the Claude Code Plus footer line reads the published figures.',
      }
    }
    await refresh($, true)
    const r = await read($, reading)

    return { text: `${r === null ? 'context-bar: no reading yet.' : describeReading(r)}\n${describeSession(session)}` }
  })

  // The prompt footer's mode-label slot, under `/ctxbar footer`: on the desktop
  // an inline run of glyphs and the figures, the engine's mode labels kept
  // after them; on the terminal one more dim label among the engine's.
  on('ui.render', { component: 'SessionMode' }, async ($, e, next) => {
    const p = await drawable($, 'footer')
    if (p === null) return next(e)

    if (e.surface === 'desktop') return footerRun($.ui.resolve(e), p, e.props.modes)

    return next({ ...e, props: { ...e.props, modes: [...e.props.modes, `ctx ${p.pctText} ${p.counts}`] } })
  })

  // The band above the prompt, under `/ctxbar top`: the desktop's line as wide
  // as the band, the terminal's row of cells.
  on('ui.render', { component: 'AbovePrompt' }, async ($, e, next) => {
    if (e.props.hasSurvey) return next(e)
    const p = await drawable($, 'top')
    if (p === null) return next(e)

    if (e.surface === 'desktop') return bandRow($.ui.resolve(e), p)
    if (e.surface === 'terminal') return terminalRow($.ui.resolve(e), p, e.props.bodyColumns)

    return next(e)
  })
}
