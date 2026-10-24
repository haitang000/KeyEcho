"""Prepare crisp and soft mouse press one-shots from 1j01's CC0 recording."""
from pathlib import Path
import hashlib
import json
import subprocess
import urllib.request
import wave
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
directory = ROOT / 'assets/originals/mouse'
directory.mkdir(parents=True, exist_ok=True)
url = 'https://opengameart.org/sites/default/files/middle-click-press.wav'
source = directory / 'middle-click-press.wav'
if not source.exists():
    with urllib.request.urlopen(url, timeout=60) as response:
        source.write_bytes(response.read())
raw = subprocess.check_output(['ffmpeg', '-v', 'error', '-i', str(source), '-ar', '48000', '-ac', '1', '-f', 'f32le', 'pipe:1'])
samples = np.frombuffer(raw, dtype='<f4')
active = np.flatnonzero(np.abs(samples) > max(float(np.max(np.abs(samples))), .00001) * .015)
samples = samples[max(0, int(active[0]) - 96):int(active[-1]) + 241].copy()
samples *= .65 / max(float(np.max(np.abs(samples))), .00001)
soft = np.convolve(samples, np.ones(7) / 7, mode='same') * .8
manifest = {'source': url, 'sourcePage': 'https://opengameart.org/content/middle-mouse-click',
            'author': '1j01', 'license': 'CC0 1.0', 'sourceSha256': hashlib.sha256(source.read_bytes()).hexdigest(), 'assets': []}
for name, values in [('mouse-crisp.wav', samples), ('mouse-soft.wav', soft)]:
    values = values.copy()
    fade = min(144, len(values) // 4)
    values[:4] *= np.linspace(0, 1, 4)
    values[-fade:] *= np.linspace(1, 0, fade)
    output = ROOT / 'src/KeyEcho.App/Assets/Sounds' / name
    with wave.open(str(output), 'wb') as wav:
        wav.setparams((1, 2, 48000, 0, 'NONE', 'not compressed'))
        wav.writeframes((values * 32767).astype('<i2').tobytes())
    manifest['assets'].append({'file': name, 'seconds': len(values)/48000, 'sha256': hashlib.sha256(output.read_bytes()).hexdigest()})
(ROOT / 'assets/mouse-manifest.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
print('Prepared crisp and soft CC0 mouse clicks:',len(samples)/48000,'seconds')
