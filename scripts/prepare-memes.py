"""Prepare the meme presets from pinned, locally retained user files."""
from pathlib import Path
import hashlib
import json
import subprocess
import wave

import numpy as np

ROOT = Path(__file__).resolve().parents[1]
RATE = 48000
SOURCE_SHA256 = 'a1b0949b66b104a308b1ba2f330494049a3dfad84d9bf457049635ebc8240bb9'
CHANNELS = 2
CROP_START = .850
CROP_END = 3.005
source = ROOT / 'assets/originals/memes/fah-user-reference.mp4'
if hashlib.sha256(source.read_bytes()).hexdigest() != SOURCE_SHA256:
    raise ValueError('FAHHHHHH source checksum differs from the user-confirmed video')

raw = subprocess.check_output([
    'ffmpeg', '-v', 'error', '-i', str(source), '-map', '0:a:0',
    '-ar', str(RATE), '-ac', str(CHANNELS), '-f', 'f32le', 'pipe:1'
])
samples = np.frombuffer(raw, dtype='<f4').reshape(-1, CHANNELS)
# These offsets are relative to the decoded audio stream. Keep the original
# stereo image, pitch, speed and dynamics; only remove the surrounding silence.
start = round(CROP_START * RATE)
end = round(CROP_END * RATE)
if len(samples) < end:
    raise ValueError('FAHHHHHH source is shorter than the verified crop')
clip = samples[start:end].copy()
peak = float(np.max(np.abs(clip)))
if peak < .00001:
    raise ValueError('FAHHHHHH source has no audible samples')
gain = .80 / peak
clip *= gain
clip[:24] *= np.linspace(0, 1, 24)[:, None]
clip[-480:] *= np.linspace(1, 0, 480)[:, None]
pcm = np.rint(clip * 32767).astype('<i2')
output = ROOT / 'src/KeyEcho.App/Assets/Sounds/fah.wav'
with wave.open(str(output), 'wb') as wav:
    wav.setparams((CHANNELS, 2, RATE, 0, 'NONE', 'not compressed'))
    wav.writeframes(pcm.tobytes())

final = pcm.astype(np.float32) / 32768
manifest = {
    'name': 'FAHHHHHH',
    'sourceTitle': 'Fahhhh - Sound effect (HD)',
    'sourceVideoFile': 'Fahhhh - Sound effect (HD).mp4',
    'sourceUploader': 'HighQualitySFX',
    'sourcePage': 'https://www.youtube.com/watch?v=-vH2b55TSHI',
    'sourceKind': 'Exact reference video confirmed and supplied by the user',
    'license': 'User-supplied video; no separate redistribution license supplied; rights remain with their respective holders',
    'sourceFile': source.relative_to(ROOT).as_posix(),
    'sourceSha256': SOURCE_SHA256,
    'cropStartSeconds': start / RATE, 'cropEndSeconds': end / RATE,
    'cropTimeBase': 'Decoded audio stream seconds',
    'peakTarget': .80, 'gainDb': float(20 * np.log10(gain)),
    'fadeInSeconds': .0005, 'fadeOutSeconds': .010,
    'sampleRate': RATE, 'channels': CHANNELS, 'bitsPerSample': 16,
    'playbackRate': 1.0, 'pitchShiftSemitones': 0,
    'outputFile': output.relative_to(ROOT).as_posix(),
    'outputSeconds': len(final) / RATE,
    'peakDbfs': float(20 * np.log10(np.max(np.abs(final)))),
    'rmsDbfs': float(20 * np.log10(np.sqrt(np.mean(final ** 2)))),
    'clippedSamples': int(np.count_nonzero(np.abs(final) >= 1)),
    'outputSha256': hashlib.sha256(output.read_bytes()).hexdigest()
}
(ROOT / 'assets/meme-manifest.json').write_text(
    json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(f'Built FAHHHHHH: {len(final) / RATE:.3f} s; peak {manifest["peakDbfs"]:.2f} dBFS')


def prepare_user_mp3(preset_id, name, original_filename, expected_sha256, activity_search_start_seconds=0):
    source_path = ROOT / f'assets/originals/memes/{preset_id}-user-reference.mp3'
    if hashlib.sha256(source_path.read_bytes()).hexdigest() != expected_sha256:
        raise ValueError(f'{name} source checksum differs from the user-supplied MP3')
    decoded = subprocess.check_output([
        'ffmpeg', '-v', 'error', '-i', str(source_path), '-map', '0:a:0',
        '-ar', str(RATE), '-ac', str(CHANNELS), '-f', 'f32le', 'pipe:1'
    ])
    original = np.frombuffer(decoded, dtype='<f4').reshape(-1, CHANNELS)
    source_peak = float(np.max(np.abs(original)))
    active = np.flatnonzero(np.max(np.abs(original), axis=1) > max(source_peak, .00001) * .006)
    active = active[active >= round(activity_search_start_seconds * RATE)]
    if len(active) == 0:
        raise ValueError(f'{name} source has no audible samples')
    # Trim only the quiet padding, keeping 2 ms before and 15 ms after the sound.
    crop_start = max(0, int(active[0]) - 96)
    crop_end = min(len(original), int(active[-1]) + 1 + 720)
    values = original[crop_start:crop_end].copy()
    clip_gain = .80 / float(np.max(np.abs(values)))
    values *= clip_gain
    values[:24] *= np.linspace(0, 1, 24)[:, None]
    values[-480:] *= np.linspace(1, 0, 480)[:, None]
    encoded = np.rint(values * 32767).astype('<i2')
    destination = ROOT / f'src/KeyEcho.App/Assets/Sounds/{preset_id}.wav'
    with wave.open(str(destination), 'wb') as wav:
        wav.setparams((CHANNELS, 2, RATE, 0, 'NONE', 'not compressed'))
        wav.writeframes(encoded.tobytes())
    normalized = encoded.astype(np.float32) / 32768
    record = {
        'name': name, 'presetId': preset_id,
        'sourceOriginalFilename': original_filename,
        'sourceKind': 'MP3 supplied by the user',
        'license': 'User-supplied audio; no separate license supplied; rights remain with their respective holders',
        'sourceFile': source_path.relative_to(ROOT).as_posix(),
        'sourceSha256': expected_sha256,
        'cropStartSeconds': crop_start / RATE, 'cropEndSeconds': crop_end / RATE,
        'cropTimeBase': 'Decoded audio stream seconds',
        'silenceThresholdRelativeToPeak': .006,
        'activitySearchStartSeconds': activity_search_start_seconds,
        'peakTarget': .80, 'gainDb': float(20 * np.log10(clip_gain)),
        'fadeInSeconds': .0005, 'fadeOutSeconds': .010,
        'sampleRate': RATE, 'channels': CHANNELS, 'bitsPerSample': 16,
        'playbackRate': 1.0, 'pitchShiftSemitones': 0,
        'outputFile': destination.relative_to(ROOT).as_posix(),
        'outputSeconds': len(normalized) / RATE,
        'peakDbfs': float(20 * np.log10(np.max(np.abs(normalized)))),
        'rmsDbfs': float(20 * np.log10(np.sqrt(np.mean(normalized ** 2)))),
        'clippedSamples': int(np.count_nonzero(np.abs(normalized) >= 1)),
        'outputSha256': hashlib.sha256(destination.read_bytes()).hexdigest()
    }
    (ROOT / f'assets/{preset_id}-manifest.json').write_text(
        json.dumps(record, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(f'Built {name}: {len(normalized) / RATE:.3f} s; peak {record["peakDbfs"]:.2f} dBFS')


prepare_user_mp3('bing', '冰冰冰',
                 'bing-bing-bing-ding-ding-ding-zhe-ni-bing-wo-wo-liao-bing.mp3',
                 'c65e5b132a1d11567ce2237da36a0a58fa320cc592d7605914e315aa020db772')
prepare_user_mp3('bruh', 'bruh', 'movie_1.mp3',
                 '6a04c34422d17b5ad0a62baaf5810dab7e19aa06baf5cbe2002fc750486ff170')
prepare_user_mp3('augh', 'AUGHHHHH', 'aughhhhh-aughhhhh.mp3',
                 '3ae073250cd74192aea07c5005d5a647454470e8252c400d1ffa1daec508d788')
prepare_user_mp3('metal-pipe', '钢管落地', 'metal-pipe-clang.mp3',
                 '69f185a65580059bd279faf3efca8bc66dca0a984473cd5a97be3a3b5b3dd797')
prepare_user_mp3('shocked', 'Shocked', 'shocked-sound-effect.mp3',
                 '65dee4c57274f57c967529f274bdded069a6ac066cafed1005e066eea6f241de')
# A sub-millisecond click at 0.205 s precedes the laugh, which starts at 0.400 s.
prepare_user_mp3('private-laugh', '私人笑声', 'baby-laughing-meme.mp3',
                 '1a8862951f78fa5745a126254770c133e8a5b8b33745f1b5f898d78508e095ba',
                 activity_search_start_seconds=.390)
prepare_user_mp3('wow', 'wow', 'anime-wow-sound-effect.mp3',
                 '5118c619f0ab62e181f32d9eacd490441b49aaa7bb8e1be1b6b51e7a3c25debc')
