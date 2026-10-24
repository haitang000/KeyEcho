"""Rebuild the amplified AWM one-shot from the locally retained video crop.

Normal builds use the final WAV directly and do not require the original video.
"""
from pathlib import Path
import hashlib
import json
import subprocess
import wave

import numpy as np

ROOT = Path(__file__).resolve().parents[1]
RATE = 48000
source = ROOT / 'assets/originals/sniper/awm-video-shot.wav'
provenance = json.loads((ROOT / 'assets/awm-video-source.json').read_text(encoding='utf-8'))
if not source.is_file():
    raise FileNotFoundError(f'Original extracted gunshot is required: {source}')
if hashlib.sha256(source.read_bytes()).hexdigest() != provenance['sourceShotSha256']:
    raise ValueError('Extracted source checksum differs from the verified video shot')

raw = subprocess.check_output([
    'ffmpeg', '-v', 'error', '-i', str(source), '-ar', str(RATE),
    '-ac', '1', '-f', 'f32le', 'pipe:1'
])
samples = np.frombuffer(raw, dtype='<f4').copy()
if len(samples) != provenance['samples']:
    raise ValueError('Source duration differs from the verified crop')

# Raise the body/tail. Lookahead limiting catches the original AAC overshoots
# and leaves about 1 dB of headroom without changing playback speed. Oversample
# the limiter 4x to catch inter-sample peaks, too.
filters = 'aresample=192000,volume=20dB,alimiter=limit=0.885:attack=1:release=3:level=false:latency=true,aresample=48000'
processed = subprocess.run([
    'ffmpeg', '-v', 'error', '-f', 'f32le', '-ar', str(RATE), '-ac', '1',
    '-i', 'pipe:0', '-af', filters, '-f', 'f32le', 'pipe:1'
], input=samples.astype('<f4').tobytes(), capture_output=True, check=True)
result = np.frombuffer(processed.stdout, dtype='<f4').copy()
if len(result) != len(samples):
    raise ValueError('Processing changed the gunshot duration')

reference = samples.copy()
for clip in (result, reference):
    clip[:24] *= np.linspace(0, 1, 24)  # 0.5 ms, before the attack
    clip[-2880:] *= np.linspace(1, 0, 2880)  # 60 ms, before the bolt

output = ROOT / 'src/KeyEcho.App/Assets/Sounds/awm.wav'
pcm = np.rint(result * 32767).astype('<i2')
with wave.open(str(output), 'wb') as wav:
    wav.setparams((1, 2, RATE, 0, 'NONE', 'not compressed'))
    wav.writeframes(pcm.tobytes())

final = pcm.astype(np.float32) / 32768
peak_db = float(20 * np.log10(np.max(np.abs(final))))
rms_db = float(20 * np.log10(np.sqrt(np.mean(final ** 2))))
gain_db = float(20 * np.log10(np.sqrt(np.mean(final ** 2)) / np.sqrt(np.mean(reference ** 2))))
manifest = {
    'name': 'AWM gunshot extracted and amplified from user-provided video',
    'source': provenance['videoFile'], 'sourceSha256': provenance['videoSha256'],
    'license': 'User-provided video; no separate license supplied',
    'sourceShot': source.relative_to(ROOT).as_posix(),
    'sourceShotSha256': provenance['sourceShotSha256'],
    'cropStartVideoSeconds': provenance['cropStartVideoSeconds'],
    'cropEndVideoSeconds': provenance['cropEndVideoSeconds'],
    'filter': filters, 'fadeInSeconds': .0005, 'fadeOutSeconds': .060,
    'sampleRate': RATE, 'channels': 1, 'bitsPerSample': 16,
    'outputSeconds': len(final) / RATE,
    'peakDbfs': peak_db, 'rmsDbfs': rms_db, 'rmsGainDb': gain_db,
    'clippedSamples': int(np.count_nonzero(np.abs(final) >= 1)),
    'outputSha256': hashlib.sha256(output.read_bytes()).hexdigest()
}
(ROOT / 'assets/sniper-manifest.json').write_text(
    json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(f'Built AWM: {len(final) / RATE:.3f} s; RMS gain {gain_db:+.2f} dB; peak {peak_db:.2f} dBFS')
