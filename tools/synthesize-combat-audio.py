"""Generate original combat prototype sounds with Python's standard library.

Run from any directory. Output is deterministic mono, PCM 16-bit, 44.1 kHz WAV.
No recordings, downloaded audio, synthesis packages, or external tools are used.
Use --verify-only to inspect existing files without replacing them.
"""

from __future__ import annotations

import argparse
import json
import math
import random
import struct
import wave
from pathlib import Path


SAMPLE_RATE = 44_100
OUTPUT_DIR = Path(__file__).resolve().parent.parent / "unity/Assets/Resources/Audio"
TAU = 2.0 * math.pi


def sine(frequency: float, time: float) -> float:
    return math.sin(TAU * frequency * time)


def chirp(start: float, end: float, time: float, duration: float) -> float:
    """Linear frequency sweep, with phase integrated to avoid discontinuities."""
    return math.sin(TAU * (start * time + (end - start) * time * time / (2 * duration)))


def smoothstep(value: float) -> float:
    value = min(1.0, max(0.0, value))
    return value * value * (3.0 - 2.0 * value)


def filtered_noise(count: int, seed: int, cutoff: float) -> tuple[list[float], list[float]]:
    """Return deterministic white noise and its one-pole low-pass signal."""
    rng = random.Random(seed)
    coefficient = 1.0 - math.exp(-TAU * cutoff / SAMPLE_RATE)
    white, low = [], []
    state = 0.0
    for _ in range(count):
        sample = rng.uniform(-1.0, 1.0)
        state += coefficient * (sample - state)
        white.append(sample)
        low.append(state)
    return white, low


def synthesize(name: str, duration: float, seed: int) -> list[float]:
    count = round(duration * SAMPLE_RATE)
    noise, low = filtered_noise(count, seed, 1_200.0)
    samples = []
    for index in range(count):
        time = index / SAMPLE_RATE
        if name == "melee_hit":
            # Low physical impact, a short noise transient, and inharmonic metal.
            body = 0.95 * chirp(165, 54, time, duration) * math.exp(-time * 24)
            thud = 0.45 * low[index] * math.exp(-time * 70)
            metal = (0.19 * sine(1_370, time) + 0.11 * sine(2_213, time)) * math.exp(-time * 42)
            sample = body + thud + metal + 0.16 * noise[index] * math.exp(-time * 155)
        elif name == "arrow_launch":
            # A light band-shaped rush and quickly decaying bow-string note.
            rush = (noise[index] - low[index]) * 0.45
            rush *= math.sin(math.pi * time / duration) ** 1.3
            string = 0.19 * chirp(710, 330, time, duration) * math.exp(-time * 35)
            sample = rush + string
        elif name == "arrow_hit":
            # Damped wooden resonance, low contact noise, and a small bright tick.
            wood = (0.65 * sine(410, time) + 0.29 * sine(697, time)) * math.exp(-time * 48)
            tick = 0.12 * sine(2_650, time) * math.exp(-time * 170)
            sample = wood + tick + 0.35 * low[index] * math.exp(-time * 85)
        elif name == "defeat":
            # A falling two-oscillator tone with soft noise; no dramatic stinger.
            body = chirp(260, 58, time, duration)
            overtone = 0.18 * chirp(520, 116, time, duration)
            sample = (body + overtone) * math.exp(-time * 8) + 0.06 * low[index] * math.exp(-time * 15)
        elif name == "ui_confirm":
            # Two soft notes in succession, each with its own smooth envelope.
            sample = 0.0
            for start, frequency in ((0.0, 660.0), (0.042, 880.0)):
                local = time - start
                note_length = 0.058
                if 0.0 <= local < note_length:
                    envelope = smoothstep(local / 0.004) * smoothstep((note_length - local) / 0.018)
                    sample += (sine(frequency, local) + 0.08 * sine(frequency * 2, local)) * envelope
        else:
            raise ValueError(f"Unknown sound: {name}")

        # Enforce silent endpoints and remove start/end clicks for every asset.
        fade_in = 0.006 if name in ("arrow_launch", "defeat") else 0.002
        fade_out = 0.018
        sample *= smoothstep(time / fade_in)
        sample *= smoothstep(((count - 1 - index) / SAMPLE_RATE) / fade_out)
        samples.append(sample)
    return samples


SOUNDS = {
    "melee_hit": (0.25, 2_026_101, 0.62),
    "arrow_launch": (0.15, 2_026_102, 0.45),
    "arrow_hit": (0.15, 2_026_103, 0.52),
    "defeat": (0.30, 2_026_104, 0.48),
    "ui_confirm": (0.10, 2_026_105, 0.35),
}


def write_sound(path: Path, samples: list[float], peak_target: float) -> None:
    peak = max(abs(sample) for sample in samples)
    gain = peak_target / peak if peak else 0.0
    integers = [round(sample * gain * 32_767) for sample in samples]
    with wave.open(str(path), "wb") as output:
        output.setnchannels(1)
        output.setsampwidth(2)
        output.setframerate(SAMPLE_RATE)
        output.writeframes(struct.pack(f"<{len(integers)}h", *integers))


def verify_sound(path: Path, expected_duration: float) -> dict[str, object]:
    with wave.open(str(path), "rb") as source:
        channels = source.getnchannels()
        sample_width = source.getsampwidth()
        rate = source.getframerate()
        count = source.getnframes()
        compression = source.getcomptype()
        payload = source.readframes(count)
    assert channels == 1, f"{path.name}: expected mono"
    assert sample_width == 2, f"{path.name}: expected 16-bit PCM"
    assert rate == SAMPLE_RATE, f"{path.name}: incorrect sample rate"
    assert compression == "NONE", f"{path.name}: expected uncompressed PCM"
    assert count == round(expected_duration * SAMPLE_RATE), f"{path.name}: incorrect duration"
    samples = struct.unpack(f"<{count}h", payload)
    peak = max(abs(sample) for sample in samples) / 32_768
    rms = math.sqrt(sum(sample * sample for sample in samples) / count) / 32_768
    assert 0.0 < peak <= 0.65, f"{path.name}: unsafe or silent peak: {peak}"
    assert samples[0] == 0 and samples[-1] == 0, f"{path.name}: endpoints must be silent"
    # The first and last 0.5 ms must remain quiet after the fades and quantization.
    edge_count = round(SAMPLE_RATE * 0.0005)
    edge_peak = max(abs(sample) for sample in samples[:edge_count] + samples[-edge_count:]) / 32_768
    assert edge_peak < 0.10, f"{path.name}: edge fade too abrupt: {edge_peak}"
    return {
        "file": path.name,
        "channels": channels,
        "sample_rate_hz": rate,
        "pcm_bits": sample_width * 8,
        "frames": count,
        "duration_seconds": round(count / rate, 6),
        "peak": round(peak, 6),
        "rms": round(rms, 6),
        "edge_peak": round(edge_peak, 6),
        "silent_endpoints": True,
    }


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--verify-only", action="store_true")
    args = parser.parse_args()
    if not args.verify_only:
        OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
        for name, (duration, seed, peak) in SOUNDS.items():
            write_sound(OUTPUT_DIR / f"{name}.wav", synthesize(name, duration, seed), peak)
    reports = [verify_sound(OUTPUT_DIR / f"{name}.wav", spec[0]) for name, spec in SOUNDS.items()]
    print(json.dumps({"verification": "passed", "listening_review": "not_performed", "sounds": reports}, indent=2))


if __name__ == "__main__":
    main()
