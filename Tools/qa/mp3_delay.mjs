// Decode an MP3 with Chrome's WebAudio and dump the first seconds as raw float32 for offset measurement.
import { createRequire } from 'node:module';
import fs from 'node:fs';
const require = createRequire(import.meta.url); // run `npm install` in Tools/qa first
const puppeteer = require('puppeteer-core');
const [url, out] = process.argv.slice(2);
const browser = await puppeteer.launch({ executablePath: process.env.CHROME_PATH || '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome', headless: 'new' });
const page = await browser.newPage();
await page.goto(url.replace(/[^/]*$/, ''), { waitUntil: 'load' }).catch(() => {});
const b64 = await page.evaluate(async (u) => {
  const buf = await (await fetch(u)).arrayBuffer();
  const ctx = new OfflineAudioContext(2, 44100 * 10, 44100);
  const ab = await ctx.decodeAudioData(buf);
  const ch = ab.getChannelData(0).slice(0, 44100 * 10);
  const bytes = new Uint8Array(ch.buffer);
  let s = ''; for (let i = 0; i < bytes.length; i += 0x8000) s += String.fromCharCode.apply(null, bytes.subarray(i, i + 0x8000));
  return [btoa(s), ab.sampleRate, ab.length];
}, url);
fs.writeFileSync(out, Buffer.from(b64[0], 'base64'));
console.log('sampleRate', b64[1], 'length', b64[2]);
await browser.close();
