// Headless Chrome QA driver for Game Heart.
// node drive.mjs "<query>" <prefix> [--size=1280x720] [--phone] [--until=event] [--timeout=sec] [--every=ms] [--steps=json] [--base=url]
import { createRequire } from 'node:module';
import fs from 'node:fs';
const require = createRequire(import.meta.url); // run `npm install` in Tools/qa first
const puppeteer = require('puppeteer-core');
const [query = '', prefix = 'run', ...flags] = process.argv.slice(2);
const opt = (k, d) => { const f = flags.find(x => x.startsWith('--' + k + '=')); return f ? f.slice(k.length + 3) : d; };
const phone = flags.includes('--phone');
const until = opt('until', null), timeout = +opt('timeout', 240), every = +opt('every', 0);
const steps = JSON.parse(opt('steps', '[]'));
const outDir = new URL('../../QA/', import.meta.url).pathname + prefix.split('/')[0];
fs.mkdirSync(outDir, { recursive: true });
const browser = await puppeteer.launch({ protocolTimeout: 60000, executablePath: process.env.CHROME_PATH || '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome', headless: 'new',
  args: ['--use-angle=metal', '--enable-gpu', '--ignore-gpu-blocklist', '--autoplay-policy=no-user-gesture-required', '--mute-audio', '--no-first-run'] });
const page = await browser.newPage();
if (phone) await page.emulate({ viewport: { width: 844, height: 390, isMobile: true, hasTouch: true, deviceScaleFactor: 2 }, userAgent: 'Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1' });
else { const [w, h] = opt('size', '1280x720').split('x').map(Number); await page.setViewport({ width: w, height: h, deviceScaleFactor: +opt('dpr', 1) }); }
const events = []; const errors = []; let hit = false; const t0 = Date.now();
page.on('console', m => {
  const t = m.text();
  if (t.includes('GH_EVENT')) { const s = t.slice(t.indexOf('GH_EVENT') + 9); const line = ((Date.now() - t0) / 1000).toFixed(1) + 's ' + s.slice(0, 160); events.push(line); if (flags.includes('--live')) console.log(line); if (until && s.startsWith(until)) hit = true; }
  else if (flags.includes('--all')) console.log('LOG', t.slice(0, 200));
  else if (/error|exception/i.test(t) && !/favicon|AudioContext was not allowed/.test(t)) errors.push(t.slice(0, 300));
});
page.on('pageerror', e => errors.push('PAGEERROR ' + e.message.slice(0, 300)));
const base = opt('base', 'http://127.0.0.1:18790/');
await page.goto(base + (query.startsWith('?') || !query ? query : '?' + query), { waitUntil: 'load' });
const sleep = ms => new Promise(r => setTimeout(r, ms));
let n = 0;
const shot = name => Promise.race([page.screenshot({ path: `${outDir}/${prefix.split('/').slice(1).join('-') || prefix}-${name}.jpg`, type: 'jpeg', quality: 78 }), sleep(15000).then(() => { throw new Error('shot timeout'); })]).catch(e => errors.push('SHOT ' + name + ' ' + e.message));
// wait for ready
while (!events.some(e => e.includes(' ready')) && Date.now() - t0 < 120000) await sleep(250);
const box = await page.$eval('#unity-canvas', c => { const r = c.getBoundingClientRect(); return { x: r.x, y: r.y, w: r.width, h: r.height }; });
const at = (x, y) => [box.x + box.w * x, box.y + box.h * y];
for (const s of steps) {
  if (s.wait) await sleep(s.wait);
  if (s.waitFor) { const ws = Date.now(); while (!events.some(e => e.includes(s.waitFor)) && Date.now() - ws < (s.max || 60000)) await sleep(150); }
  if (s.tap) { const [x, y] = at(...s.tap); if (phone) await page.touchscreen.tap(x, y); else await page.mouse.click(x, y); }
  if (s.key) await page.keyboard.press(s.key);
  if (s.shot) await shot(s.shot);
}
let last = Date.now();
while (!(until ? hit : steps.length > 0 || every === 0) && Date.now() - t0 < timeout * 1000) {
  if (every && Date.now() - last >= every) { last = Date.now(); await shot(String(n++).padStart(3, '0')); }
  await sleep(100);
}
if (until && hit) await sleep(+opt('after', 800));
await shot('final');
const fps = await Promise.race([sleep(8000).then(() => -1), page.evaluate(() => new Promise(r => { let k = 0, s = performance.now(); const f = () => { k++; if (performance.now() - s > 2000) r(k / 2); else requestAnimationFrame(f); }; f(); }))]);
console.log(`fps ${fps} canvas ${box.w}x${box.h} hit=${hit} elapsed=${((Date.now() - t0) / 1000).toFixed(1)}s`);
console.log(events.join('\n'));
if (errors.length) console.log('ERRORS:\n' + [...new Set(errors)].slice(0, 30).join('\n'));
await browser.close();
