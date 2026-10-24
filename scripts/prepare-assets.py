"""Fetch the attributed source recordings and build offline PCM sound assets."""
from pathlib import Path
import hashlib
import json
import shutil
import subprocess
import urllib.request
import wave
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
ORIGINAL = ROOT / 'assets' / 'originals'
OUTPUT = ROOT / 'src' / 'KeyEcho.App' / 'Assets' / 'Sounds'
SOURCES = {
    'keyboard01.ogg': 'https://opengameart.org/sites/default/files/keyboard01_0.ogg',
    'keyboard02.ogg': 'https://opengameart.org/sites/default/files/keyboard02_0.ogg',
    'shots.7z': 'https://opengameart.org/sites/default/files/shots.7z',
}

def fetch():
    ORIGINAL.mkdir(parents=True, exist_ok=True)
    OUTPUT.mkdir(parents=True, exist_ok=True)
    for name, url in SOURCES.items():
        target = ORIGINAL / name
        if not target.exists():
            print('Downloading', name, flush=True)
            with urllib.request.urlopen(url, timeout=120) as response:
                target.write_bytes(response.read())
        print(name, hashlib.sha256(target.read_bytes()).hexdigest(), flush=True)
    subprocess.run(['7z', 'x', str(ORIGINAL / 'shots.7z'), '-o' + str(ORIGINAL / 'shots'), '-y'], check=True, capture_output=True)

def convert():
    cuts = [
        ('keyboard01.ogg', 'mechanical-1.wav', .006, .070),
        ('keyboard02.ogg', 'mechanical-2.wav', .032, .110),
        ('shots/shots/pistol.wav', 'pistol.wav', .001, .500),
        ('shots/shots/rifle.wav', 'rifle.wav', .001, .450),
        ('shots/shots/shotgun.wav', 'shotgun.wav', .001, .450),
    ]
    manifest = {'sources': {name: {'url': url, 'sha256': hashlib.sha256((ORIGINAL / name).read_bytes()).hexdigest()} for name, url in SOURCES.items()}, 'assets': []}
    for source, name, start, end in cuts:
        raw = subprocess.check_output(['ffmpeg', '-v', 'error', '-i', str(ORIGINAL / source), '-ar', '48000', '-ac', '1', '-f', 'f32le', 'pipe:1'])
        samples = np.frombuffer(raw, dtype='<f4')[int(start * 48000):int(end * 48000)].copy()
        samples *= .72 / max(float(np.max(np.abs(samples))), .00001)
        fade = min(384, len(samples) // 4)
        samples[-fade:] *= np.linspace(1, 0, fade)
        # Retain the transient; just ease the first four samples to avoid a hard edge.
        samples[:4] *= np.linspace(0, 1, 4)
        with wave.open(str(OUTPUT / name), 'wb') as output:
            output.setparams((1, 2, 48000, 0, 'NONE', 'not compressed'))
            output.writeframes((samples * 32767).astype('<i2').tobytes())
        manifest['assets'].append({'file': name, 'source': source, 'startSeconds': start, 'endSeconds': end, 'peak': .72, 'sha256': hashlib.sha256((OUTPUT / name).read_bytes()).hexdigest()})
        print('Built', name, len(samples) / 48000, 'seconds', flush=True)
    (ROOT / 'assets' / 'sound-manifest.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')

if __name__ == '__main__':
    fetch()
    convert()
