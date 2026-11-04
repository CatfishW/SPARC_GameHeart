// Plays a chart with real keyboard events (no autoplay) to verify input, judging and clock sync.
// node play_keys.mjs <chapter> <song> <diff> [--base=url] [--size=WxH]
import { createRequire } from 'node:module';
import fs from 'node:fs';
const require = createRequire(import.meta.url); // run `npm install` in Tools/qa first
const puppeteer = require('puppeteer-core');
const [chapter, song, diff, ...flags] = process.argv.slice(2);
const opt = (k, d) => { const f = flags.find(x => x.startsWith('--' + k + '=')); return f ? f.slice(k.length + 3) : d; };
const chart = JSON.parse(fs.readFileSync(new URL(`../../Assets/Resources/Charts/${song}.json`, import.meta.url)));
const notes = chart.diffs.find(d => d.name === diff).notes;
const keys = ['d', 'f', 'j', 'k'];
const touch = flags.includes('--touch');
const laneX = [0.2, 0.45, 0.55, 0.8];
const browser = await puppeteer.launch({ executablePath: process.env.CHROME_PATH || '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome', headless: 'new',
  args: ['--use-angle=metal', '--enable-gpu', '--autoplay-policy=no-user-gesture-required', '--mute-audio'] });
const page = await browser.newPage();
if (touch) await page.emulate({ viewport: { width: 844, height: 390, isMobile: true, hasTouch: true, deviceScaleFactor: 2 }, userAgent: 'Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1' });
else await page.setViewport({ width: 1280, height: 720 });
let begin = null, end = null;
page.on('console', m => { const t = m.text(); if (t.includes('GH_EVENT song_begin')) begin = performance.now(); if (t.includes('GH_EVENT song_end')) end = t.slice(t.indexOf('song_end')); });
await page.goto(opt('base', 'http://127.0.0.1:18791/') + `?qa=1&fast=1&nosave=1&jump=${chapter}:song&diff=${diff}`);
// the song card auto-starts only with auto=1, so press Enter on START
const t0 = Date.now();
while (!begin && Date.now() - t0 < 60000) { await new Promise(r => setTimeout(r, 300)); await page.keyboard.press('Enter').catch(() => {}); }
const zero = begin + 2200; // song time 0 (preroll)
const events = [];
for (const n of notes) {
  const k = n.type === 2 ? ' ' : keys[n.lane];
  events.push({ at: zero + n.t * 1000, k, down: true }, { at: zero + (n.t + (n.type === 1 ? n.d : 0.05)) * 1000, k, down: false });
}
events.sort((a, b) => a.at - b.at);
for (const e of events) {
  const wait = e.at - performance.now();
  if (wait > 2) await new Promise(r => setTimeout(r, wait - 1));
  while (performance.now() < e.at) {}
  if (touch) {
    const lane = e.k === ' ' ? 1 : keys.indexOf(e.k);
    const x = 844 * laneX[lane], y = 390 * 0.85;
    if (e.down) page.touchscreen.touchStart(x, y).catch(() => {}); else page.touchscreen.touchEnd().catch(() => {});
  } else if (e.down) page.keyboard.down(e.k === ' ' ? 'Space' : e.k).catch(() => {}); else page.keyboard.up(e.k === ' ' ? 'Space' : e.k).catch(() => {});
}
const t1 = Date.now();
while (!end && Date.now() - t1 < 15000) await new Promise(r => setTimeout(r, 200));
console.log(end || 'no song_end');
await browser.close();
