"""Extract individual keys from the MIT-licensed original Mechvibes recordings."""
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
import hashlib
import json
import subprocess
import urllib.request
import wave
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
REVISION = '326252a13e7bef4f1c35d08ef0189b5af6f8ba02'
BASE = f'https://raw.githubusercontent.com/hainguyents13/mechvibes/{REVISION}/'
PACKS = [f'cherrymx-{switch}-{caps}' for switch in ['red', 'brown', 'blue', 'black'] for caps in ['pbt', 'abs']] + ['topre-purple-hybrid-pbt']
ORIGINAL = ROOT / 'assets/originals/mechvibes'
OUTPUT = ROOT / 'src/KeyEcho.App/Assets/Sounds'

def download(pack):
    directory = ORIGINAL / pack
    directory.mkdir(parents=True, exist_ok=True)
    for name in ['config.json', 'sound.ogg']:
        target = directory / name
        if not target.exists():
            with urllib.request.urlopen(BASE + f'src/audio/{pack}/{name}', timeout=120) as response:
                target.write_bytes(response.read())
    print('Downloaded', pack, flush=True)

def convert(pack):
    directory = ORIGINAL / pack
    config = json.loads((directory / 'config.json').read_text(encoding='utf-8'))
    raw = subprocess.check_output(['ffmpeg', '-v', 'error', '-i', str(directory / 'sound.ogg'), '-ar', '48000', '-ac', '1', '-f', 'f32le', 'pipe:1'])
    recording = np.frombuffer(raw, dtype='<f4')
    mapping = {}
    for code, (start_ms, length_ms) in config['defines'].items():
        if not code.isdigit():
            continue  # release samples are unused: KeyEcho sounds on key-down only
        scan = int(code)
        # Upstream Topre F8 has a mistyped offset pointing into silence. Use the
        # adjacent function key recording rather than shipping an inaudible key.
        if pack == 'topre-purple-hybrid-pbt' and code == '66':
            start_ms, length_ms = config['defines']['65']
        if 0xe00 <= scan <= 0xe7f:
            scan = 0xe000 | (scan & 0x7f)
        if not (0 < scan < 0x80 or 0xe000 <= scan <= 0xe07f):
            continue
        start = round(start_ms * 48)
        length = round(min(length_ms, 300) * 48)
        samples = recording[start:start + length].copy()
        peak = float(np.max(np.abs(samples))) if len(samples) else 0
        if peak < .00001:
            continue
        # Preserve the original key's body and natural release; only trim leading quiet.
        active = np.flatnonzero(np.abs(samples) > peak * .015)
        samples = samples[max(0, int(active[0]) - 96):]
        # A consistent pack-wide gain preserves differences between large and small keys.
        samples *= min(2.0, .65 / max(float(np.max(np.abs(recording))), .00001))
        fade = min(240, len(samples) // 8)
        samples[-fade:] *= np.linspace(1, 0, fade)
        samples[:4] *= np.linspace(0, 1, 4)
        name = f'{pack}-{scan:04x}.wav'
        with wave.open(str(OUTPUT / name), 'wb') as output:
            output.setparams((1, 2, 48000, 0, 'NONE', 'not compressed'))
            output.writeframes((np.clip(samples, -1, 1) * 32767).astype('<i2').tobytes())
        mapping[str(scan)] = name
    assert '30' in mapping and '57' in mapping and '28' in mapping, pack
    (OUTPUT / f'{pack}.json').write_text(json.dumps(mapping, indent=2), encoding='utf-8')
    return {'id': pack, 'source': BASE + f'src/audio/{pack}/sound.ogg', 'sourceSha256': hashlib.sha256((directory / 'sound.ogg').read_bytes()).hexdigest(), 'keyCount': len(mapping)}

if __name__ == '__main__':
    OUTPUT.mkdir(parents=True, exist_ok=True)
    with ThreadPoolExecutor(max_workers=6) as pool:
        list(pool.map(download, PACKS))
    manifests = [convert(pack) for pack in PACKS]
    license_dir = ROOT / 'src/KeyEcho.App/Assets/Licenses'
    license_dir.mkdir(parents=True, exist_ok=True)
    (license_dir / 'MECHVIBES-LICENSE.txt').write_bytes(urllib.request.urlopen(BASE + 'LICENSE').read())
    (ROOT / 'assets/keyboard-manifest.json').write_text(json.dumps({'revision': REVISION, 'packs': manifests}, indent=2), encoding='utf-8')
    print('Prepared', len(manifests), 'packs and', sum(p['keyCount'] for p in manifests), 'individual keys', flush=True)
