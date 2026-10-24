"""Prepare varied mouse clicks from CC0 recordings, preserving natural transients."""
from pathlib import Path
import hashlib
import json
import math
import subprocess
import urllib.request
import wave
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
ORIGINALS = ROOT / 'assets/originals/mouse'
OUTPUT = ROOT / 'src/KeyEcho.App/Assets/Sounds'
RATE = 48000
COMMIT = '33c8ce9eb2cf0b1c2f8bcf322eb349b6be34dbb6'
SOURCES = [
    {
        'file': '4-202298-A-31.wav',
        'url': f'https://raw.githubusercontent.com/karolpiczak/ESC-50/{COMMIT}/audio/4-202298-A-31.wav',
        'sourcePage': 'https://freesound.org/people/gokiburi/sounds/202298/',
        'title': 'mouse_clicks.wav', 'author': 'gokiburi', 'license': 'CC0 1.0',
        'licenseEvidence': f'https://github.com/karolpiczak/ESC-50/blob/{COMMIT}/LICENSE',
        'sha256': 'e120e039333f2d013153a1da70f53fd8049dca03abf73aa0482125a1413446ff',
    },
    {
        'file': 'middle-click-press.wav',
        'url': 'https://opengameart.org/sites/default/files/middle-click-press.wav',
        'sourcePage': 'https://opengameart.org/content/middle-mouse-click',
        'title': 'Middle Mouse Click', 'author': '1j01', 'license': 'CC0 1.0',
        'sha256': '8a0ec2e7341f70b33f0802aadeb6a0689aed893048a600bfd46b1a6716fbe8fe',
    },
]


def load_source(info):
    path = ORIGINALS / info['file']
    if not path.exists():
        with urllib.request.urlopen(info['url'], timeout=60) as response:
            data = response.read()
        if hashlib.sha256(data).hexdigest() != info['sha256']:
            raise ValueError(f"Source checksum mismatch: {info['file']}")
        path.write_bytes(data)
    if hashlib.sha256(path.read_bytes()).hexdigest() != info['sha256']:
        raise ValueError(f"Source checksum mismatch: {info['file']}")
    raw = subprocess.check_output(['ffmpeg', '-v', 'error', '-i', str(path),
                                   '-ar', str(RATE), '-ac', '1', '-f', 'f32le', 'pipe:1'])
    return np.frombuffer(raw, dtype='<f4').astype(np.float64)


def soften(values):
    # Retain the original attack instead of the old seven-sample box filter,
    # whose spectral notches gave the click a hollow, artificial character.
    alpha = 1 - math.exp(-2 * math.pi * 4500 / RATE)
    low = np.empty_like(values)
    previous = 0.0
    for index, sample in enumerate(values):
        previous += alpha * (sample - previous)
        low[index] = previous
    return (values * .55 + low * .45) * .78


def write_clip(name, values):
    values = values.copy()
    values[:12] *= np.linspace(0, 1, 12)
    values[-240:] *= np.linspace(1, 0, 240)
    path = OUTPUT / name
    with wave.open(str(path), 'wb') as wav:
        wav.setparams((1, 2, RATE, 0, 'NONE', 'not compressed'))
        wav.writeframes(np.rint(np.clip(values, -1, 1) * 32767).astype('<i2').tobytes())
    return {'file': name, 'seconds': len(values) / RATE,
            'sha256': hashlib.sha256(path.read_bytes()).hexdigest()}


def main():
    ORIGINALS.mkdir(parents=True, exist_ok=True)
    source = load_source(SOURCES[0])
    middle = load_source(SOURCES[1])
    active = np.flatnonzero(np.abs(middle) > np.max(np.abs(middle)) * .015)
    middle = middle[max(0, int(active[0]) - 96):int(active[-1]) + 241].copy()
    middle *= .48 / np.max(np.abs(middle))
    # Separate real switch transients, ending before the next click/release.
    # No pitch shifts, synthetic layers or reverb.
    starts = [.374, 1.153, 2.102, 4.155]
    targets = [.54, .57, .55, .53]
    clicks = []
    for start, peak in zip(starts, targets):
        values = source[round(start * RATE):round((start + .060) * RATE)].copy()
        values -= values.mean()
        values *= peak / np.max(np.abs(values))
        clicks.append(values)

    manifest = {'sources': SOURCES, 'sampleRate': RATE,
                'processing': {'clickStartSeconds': starts, 'clickDurationSeconds': .060,
                               'clickPeakTargets': targets, 'middlePeakTarget': .48,
                               'fadeInMilliseconds': .25, 'fadeOutMilliseconds': 5,
                               'soft': {'lowPassHz': 4500, 'dryMix': .55, 'gain': .78},
                               'pitchShift': False}, 'assets': []}
    for preset in ['crisp', 'soft']:
        for index, values in enumerate(clicks):
            name = f'mouse-{preset}{"" if index == 0 else "-" + str(index + 1)}.wav'
            asset = write_clip(name, soften(values) if preset == 'soft' else values)
            asset.update(source=SOURCES[0]['file'], startSeconds=starts[index], role='primary')
            manifest['assets'].append(asset)
        asset = write_clip(f'mouse-{preset}-middle.wav', soften(middle) if preset == 'soft' else middle)
        asset.update(source=SOURCES[1]['file'], role='middle')
        manifest['assets'].append(asset)
    (ROOT / 'assets/mouse-manifest.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
    print('Prepared 4 real click variations and a separate middle click for each mouse preset.')


if __name__ == '__main__':
    main()
