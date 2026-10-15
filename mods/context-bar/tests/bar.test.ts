import type { On, RenderElement } from 'claude-code'
import { expect, mock, test } from 'claude-code/testing'

import { BAND_UNITS, FOOTER_CELLS, barSvg, colorAt, describeReading, figuresJson, figuresPath, fmtTokens } from '../hooks/register'

test('the gradient runs cyan through green, lime, yellow and orange to red', async () => {
  expect(colorAt(0)).toBe('#22d3ee')
  expect(colorAt(0.25)).toBe('#22c55e')
  expect(colorAt(0.5)).toBe('#a3e635')
  expect(colorAt(0.65)).toBe('#facc15')
  expect(colorAt(0.8)).toBe('#fb923c')
  expect(colorAt(1)).toBe('#ef4444')
  expect(colorAt(-1)).toBe('#22d3ee')
  expect(colorAt(2)).toBe('#ef4444')
  // halfway between cyan (34,211,238) and green (34,197,94)
  expect(colorAt(0.125)).toBe('#22cca6')
})

test('token counts read as plain, K and M', async () => {
  expect(fmtTokens(840)).toBe('840')
  expect(fmtTokens(8400)).toBe('8.4K')
  expect(fmtTokens(8000)).toBe('8K')
  expect(fmtTokens(84200)).toBe('84K')
  expect(fmtTokens(200000)).toBe('200K')
  expect(fmtTokens(1000000)).toBe('1M')
  expect(fmtTokens(1250000)).toBe('1.25M')
})

test('the svg is a bare line to the fraction, stretched to its width, with the compaction tick', async () => {
  const band = barSvg(0.42, 0.92)
  expect(band).toContain('width="42.00%"')
  expect(band).toContain('x="92.00%"')
  expect(band).toContain('linearGradient')
  expect(band).toContain('preserveAspectRatio="none"')
  expect(band).toContain(`width="${BAND_UNITS}" height="8"`)
  // nothing behind the line: no full-width rect
  expect(band).not.toContain('width="100%"')
  expect(barSvg(0, null)).not.toContain('url(#cbg)')
  expect(barSvg(1.5, null)).toContain('width="100.00%"')
})

test('the figures line names the counts and the threshold', async () => {
  expect(describeReading({ tokens: 84200, window: 200000, percent: 42, threshold: 184000, at: 0 })).toBe(
    'context-bar: 84K of 200K (42%), auto-compact at 184K',
  )
  expect(describeReading({ tokens: null, window: 200000, percent: null, threshold: null, at: 0 })).toBe(
    'context-bar: no reading yet (window 200K)',
  )
})

test('the figures file sits under the config dir and carries the session and the figures', async () => {
  expect(figuresPath('C:\\Users\\tester\\.claude\\', 'abc')).toBe('C:\\Users\\tester\\.claude\\context-bar\\abc.json')
  const text = figuresJson(
    { id: 'abc', cwd: 'C:\\Work', model: 'claude-x' },
    { tokens: 84200, window: 200000, percent: 42, threshold: 184000, at: 1000 },
    false,
  )
  expect(JSON.parse(text)).toEqual({
    sessionId: 'abc',
    cwd: 'C:\\Work',
    model: 'claude-x',
    tokens: 84200,
    window: 200000,
    percent: 42,
    threshold: 184000,
    at: 1000,
    isHidden: false,
  })
})

const BAND = {
  component: 'AbovePrompt',
  props: {
    hasSurvey: false,
    isWorking: false,
    maxRows: 10,
    bodyColumns: 120,
    scroll: { offset: 0, bodyRows: 10 },
    view: {},
  },
} as const

const FOOTER = {
  component: 'SessionMode',
  props: { modes: ['focus'] },
} as const

/**
 * The engine beneath the plugin: a session, a reading (84200 of 200000 by
 * default), a file system that remembers what was written, and its own
 * drawing at every site: the mode labels joined for the footer, the words
 * "the engine" elsewhere.
 */
const engine = (on: On, tokens = 84200) => {
  const clock = mock.clock(on, { now: 1000 })
  mock.env(on, { USERPROFILE: 'C:\\Users\\tester' })
  const written: { path: string; text: string }[] = []
  on('command.register', async (_$, e) => ({ value: { command: e.name } }))
  on('session.id', async () => ({ value: 'sess-1' }))
  on('session.cwd', async () => ({ value: 'C:\\Work' }))
  on('session.model', async () => ({ value: 'claude-x' }))
  on('session.usage', async () => ({
    value: { startedAt: 0, context: { tokens, window: 200000, percent: Math.round(tokens / 2000) }, rateLimits: [] },
  }))
  on('fs.write', async (_$, e) => {
    written.push({ path: e.path, text: e.text })
    return { value: undefined }
  })
  on('session.start', async (_$, e) => ({ cwd: e.cwd }))
  on('ui.render', async (_$, e) => {
    const { Text } = _$.ui.resolve(e)
    const modes = (e.props as { modes?: readonly string[] }).modes
    return h(Text, {}, modes ? modes.join(' & ') : 'the engine') as RenderElement
  })

  return { clock, written }
}

test('by default nothing is drawn in the app and the figures are published to the file', async ($, on) => {
  const { clock, written } = engine(on)
  mock.store(on)

  await $.session.start({ cwd: 'C:\\Work', surface: 'desktop', isInteractive: true })
  await clock.settle()

  expect(written.length).toBeGreaterThan(0)
  const last = written[written.length - 1]!
  expect(last.path).toBe('C:\\Users\\tester\\.claude\\context-bar\\sess-1.json')
  expect(JSON.parse(last.text)).toMatchObject({ sessionId: 'sess-1', cwd: 'C:\\Work', tokens: 84200, window: 200000, isHidden: false })

  for (const site of [FOOTER, BAND] as const) {
    const ui = await $.ui.mount({ plugin: 'context-bar', surface: 'desktop', ...site })
    expect(await ui.find({ type: 'Svg' })).toBeUndefined()
    expect(await ui.find({ type: 'Text', text: '42%' })).toBeUndefined()
    await ui.unmount()
  }
})

test('/ctxbar footer (kept in the store): an inline run on the desktop, a label on the terminal', async ($, on) => {
  const { clock } = engine(on)
  mock.store(on, { placement: 'footer' })

  await $.session.start({ cwd: 'C:\\Work', surface: 'desktop', isInteractive: true })
  await clock.settle()

  const footer = await $.ui.mount({ plugin: 'context-bar', surface: 'desktop', ...FOOTER })
  // nothing block-level: no Box, no Svg, one run of glyphs for the filled part
  expect(await footer.find({ type: 'Svg' })).toBeUndefined()
  expect(await footer.find({ type: 'Box' })).toBeUndefined()
  // one Text per filled glyph (the enclosing run's own text holds them all, so match a lone glyph)
  expect((await footer.findAll({ type: 'Text', text: /^━$/ })).length).toBe(Math.round(FOOTER_CELLS * 0.421))
  expect(await footer.find({ type: 'Text', text: '42%' })).toBeDefined()
  expect(await footer.find({ type: 'Text', text: ' 84K / 200K' })).toBeDefined()
  // the engine's own mode labels stay after the figures
  expect(await footer.find({ type: 'Text', text: ' focus' })).toBeDefined()
  await footer.unmount()

  const band = await $.ui.mount({ plugin: 'context-bar', surface: 'desktop', ...BAND })
  expect(await band.find({ type: 'Svg' })).toBeUndefined()
  expect(await band.find({ type: 'Text', text: 'the engine' })).toBeDefined()
  await band.unmount()

  // the terminal: the figures are one more label in the engine's own list
  const terminal = await $.ui.mount({ plugin: 'context-bar', surface: 'terminal', ...FOOTER })
  expect(await terminal.find({ type: 'Svg' })).toBeUndefined()
  expect(await terminal.find({ type: 'Text', text: 'focus & ctx 42% 84K / 200K' })).toBeDefined()
  await terminal.unmount()
})

test('/ctxbar top (kept in the store) draws the line in the band above the prompt', async ($, on) => {
  const { clock } = engine(on)
  mock.store(on, { placement: 'top' })

  await $.session.start({ cwd: 'C:\\Work', surface: 'desktop', isInteractive: true })
  await clock.settle()

  const band = await $.ui.mount({ plugin: 'context-bar', surface: 'desktop', ...BAND })
  const svg = await band.find({ type: 'Svg' })
  expect(svg).toBeDefined()
  // as wide as the band: no fixed width
  expect(svg?.props.width).toBeUndefined()
  expect(await band.find({ type: 'Text', text: '42%' })).toBeDefined()
  await band.unmount()

  const footer = await $.ui.mount({ plugin: 'context-bar', surface: 'desktop', ...FOOTER })
  expect(await footer.find({ type: 'Text', text: '━' })).toBeUndefined()
  expect(await footer.find({ type: 'Text', text: 'focus' })).toBeDefined()
  await footer.unmount()

  const terminal = await $.ui.mount({ plugin: 'context-bar', surface: 'terminal', ...BAND })
  expect(await terminal.find({ type: 'Text', text: '42%' })).toBeDefined()
  expect((await terminal.findAll({ type: 'Text', text: '━' })).length).toBeGreaterThan(30)
  await terminal.unmount()
})

test('a survey in the band leaves the band to the engine', async ($, on) => {
  const { clock } = engine(on, 10000)
  mock.store(on, { placement: 'top' })

  await $.session.start({ cwd: 'C:\\Work', surface: 'desktop', isInteractive: true })
  await clock.settle()

  const survey = await $.ui.mount({
    plugin: 'context-bar',
    surface: 'desktop',
    ...BAND,
    props: { ...BAND.props, hasSurvey: true },
  })
  expect(await survey.find({ type: 'Svg' })).toBeUndefined()
  expect(await survey.find({ type: 'Text', text: 'the engine' })).toBeDefined()
  await survey.unmount()
})

test('hidden by /ctxbar off (kept in the store): nothing in the app, and the file says so', async ($, on) => {
  const { clock, written } = engine(on, 10000)
  mock.store(on, { isHidden: true, placement: 'footer' })

  await $.session.start({ cwd: 'C:\\Work', surface: 'desktop', isInteractive: true })
  await clock.settle()

  expect(JSON.parse(written[written.length - 1]!.text)).toMatchObject({ isHidden: true, tokens: 10000 })

  const footer = await $.ui.mount({ plugin: 'context-bar', surface: 'desktop', ...FOOTER })
  expect(await footer.find({ type: 'Text', text: '━' })).toBeUndefined()
  expect(await footer.find({ type: 'Text', text: '5%' })).toBeUndefined()
  expect(await footer.find({ type: 'Text', text: 'focus' })).toBeDefined()
  await footer.unmount()
})
