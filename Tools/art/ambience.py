#!/usr/bin/env python3
"""The terminal's crowd murmur: a CC0 field recording made inside Tocumen airport, Panama (Wikimedia Commons,
"Sound inside Tocumen airport in Panama.flac"), turned into a steady loop for the hall view. Four stretches of it are
filtered to the murmur band, levelled and laid over each other so no single voice stands out, then the end is
cross-faded into the start so it loops without a seam. Writes Assets/Resources/Audio/hall.ogg.

    python3 Tools/art/ambience.py          # needs ffmpeg and numpy; downloads the recording (~52 MB) to a temp folder
"""
import os
import subprocess
import sys
import tempfile
import urllib.request

import numpy as np

SOURCE = "https://upload.wikimedia.org/wikipedia/commons/0/0e/Sound_inside_Tocumen_airport_in_Panama.flac"
PAGE = "https://commons.wikimedia.org/wiki/File:Sound_inside_Tocumen_airport_in_Panama.flac"
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "Resources", "Audio", "hall.ogg")
RATE = 44100
LENGTH = 60.0           # seconds of loop
FADE = 4.0              # the cross-fade at the seam
LAYERS = (20, 85, 140, 225)


def main():
    work = tempfile.mkdtemp(prefix="crosswind_hall_")
    src = sys.argv[1] if len(sys.argv) > 1 else os.path.join(work, "tocumen.flac")
    if not os.path.exists(src):
        print("fetching", PAGE)
        req = urllib.request.Request(SOURCE, headers={"User-Agent": "crosswind-assets/1.0"})
        with urllib.request.urlopen(req) as r, open(src, "wb") as f:
            f.write(r.read())
    span, pad = LENGTH + FADE, 12          # the leveller needs a few seconds either side to settle: cut them off after
    layers = []
    for start in LAYERS:
        raw = subprocess.run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-ss", str(start - pad), "-t", str(span + 2 * pad), "-i", src,
                              "-ac", "1", "-ar", str(RATE),
                              "-af", "highpass=f=140,lowpass=f=3000,acompressor=threshold=-30dB:ratio=6:attack=30:release=600,dynaudnorm=f=500:g=31",
                              "-f", "f32le", "-"], check=True, capture_output=True).stdout
        a = np.frombuffer(raw, dtype=np.float32).astype(np.float64)[pad * RATE:]
        n = int(span * RATE)
        layers.append(np.pad(a, (0, max(0, n - len(a))))[:n])
    mix = sum(layers) / len(layers)
    # The seam: the last FADE seconds fade out over the first FADE seconds fading in (equal power).
    n, f = int(LENGTH * RATE), int(FADE * RATE)
    t = np.linspace(0, 1, f)
    loop = mix[:n].copy()
    loop[:f] = mix[:f] * np.sin(t * np.pi / 2) + mix[n:n + f] * np.cos(t * np.pi / 2)
    loop *= 0.1 / max(1e-6, np.sqrt(np.mean(loop ** 2)))           # -20 dBFS RMS, room for the peaks; the game sets the level
    loop = np.clip(loop, -0.98, 0.98).astype(np.float32)
    wav = os.path.join(work, "hall.f32")
    loop.tofile(wav)
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    subprocess.run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-f", "f32le", "-ar", str(RATE), "-ac", "1", "-i", wav,
                    "-c:a", "libvorbis", "-q:a", "3", OUT], check=True)
    print("wrote", os.path.normpath(OUT), os.path.getsize(OUT) // 1024, "KB")


if __name__ == "__main__":
    main()
