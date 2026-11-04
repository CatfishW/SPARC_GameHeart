#!/usr/bin/env python3
"""Serve a WebGL build locally (no-cache, wasm MIME). Usage: serve.py [--dir Builds/WebGLDev] [--port 18790]"""
import argparse
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path


class H(SimpleHTTPRequestHandler):
    extensions_map = {**SimpleHTTPRequestHandler.extensions_map, '.wasm': 'application/wasm', '.js': 'application/javascript',
                      '.mp3': 'audio/mpeg', '.data': 'application/octet-stream'}

    def end_headers(self):
        self.send_header('Cache-Control', 'no-cache')
        super().end_headers()

    def log_message(self, *a):
        pass


if __name__ == '__main__':
    ap = argparse.ArgumentParser()
    ap.add_argument('--dir', default=str(Path(__file__).resolve().parents[2] / 'Builds/WebGLDev'))
    ap.add_argument('--port', type=int, default=18790)
    o = ap.parse_args()
    s = ThreadingHTTPServer(('127.0.0.1', o.port), partial(H, directory=o.dir))
    print(f'serving {o.dir} on http://127.0.0.1:{o.port}/', flush=True)
    s.serve_forever()
