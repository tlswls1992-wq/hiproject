"""
게임 배경음악(BGM)과 효과음(SFX)을 코드로 직접 합성하는 스크립트입니다. (Claude가 사용)
외부 음원이나 샘플을 전혀 쓰지 않고 사인파/노이즈 같은 수학 공식으로만 만들기 때문에 저작권 문제가 없어요.

출력: Assets/Resources/Audio/*.ogg
  bgm_camp   야영지 · 타이틀 · 메뉴 (잔잔한 류트 느낌)
  bgm_battle 전투
  bgm_boss   보스 등장 후
  sfx_*      효과음

실행: python3 Art/make_audio.py   (numpy와 ffmpeg 필요)
"""
import subprocess
import tempfile
import wave
from pathlib import Path

import numpy as np

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "Assets" / "Resources" / "Audio"
SR = 32000
rng = np.random.default_rng(7)


# ---------------------------------------------------------------- 기본 도구

def t_of(dur):
    return np.arange(int(SR * dur)) / SR


def midi(n):
    return 440.0 * 2 ** ((n - 69) / 12)


NOTE = {"C": 0, "C#": 1, "Db": 1, "D": 2, "D#": 3, "Eb": 3, "E": 4, "F": 5, "F#": 6, "Gb": 6,
        "G": 7, "G#": 8, "Ab": 8, "A": 9, "A#": 10, "Bb": 10, "B": 11}


def n(name):
    """'A4' 같은 음 이름 -> MIDI 번호"""
    letter = name[:-1]
    octave = int(name[-1])
    return 12 * (octave + 1) + NOTE[letter]


def env(length, attack=0.005, release=0.05, total=None):
    total = total or length / SR
    t = np.arange(length) / SR
    a = np.clip(t / max(attack, 1e-4), 0, 1)
    r = np.clip((total - t) / max(release, 1e-4), 0, 1)
    return a * r


def lowpass(x, cutoff):
    """1차 저역 통과 필터 (소리를 부드럽게)"""
    a = 1 - np.exp(-2 * np.pi * cutoff / SR)
    y = np.empty_like(x)
    acc = 0.0
    for i in range(len(x)):
        acc += a * (x[i] - acc)
        y[i] = acc
    return y


def highpass(x, cutoff):
    return x - lowpass(x, cutoff)


def noise(dur):
    return rng.uniform(-1, 1, int(SR * dur))


def reverb(x, seconds=1.2, wet=0.2, seed=3):
    """지수적으로 줄어드는 노이즈를 잔향으로 써서 공간감을 줌 (FFT 합성곱)"""
    r = np.random.default_rng(seed)
    ir_t = np.arange(int(SR * seconds)) / SR
    ir = r.uniform(-1, 1, len(ir_t)) * np.exp(-ir_t * 5.5 / seconds)
    ir = lowpass(ir, 4500)
    ir /= np.sqrt((ir ** 2).sum())
    size = len(x) + len(ir)
    nfft = 1 << (size - 1).bit_length()
    y = np.fft.irfft(np.fft.rfft(x, nfft) * np.fft.rfft(ir, nfft), nfft)[:len(x)]
    return x * (1 - wet) + y * wet * 2.2


def normalize(x, peak=0.89):
    m = np.abs(x).max()
    return x if m == 0 else x * (peak / m)


def save(name, x, fade=True):
    OUT.mkdir(parents=True, exist_ok=True)
    x = np.clip(x, -1, 1)
    if fade:  # 효과음 끝이 '틱' 하고 끊기지 않게 마지막 15ms를 부드럽게 줄임
        k = min(len(x), int(SR * 0.015))
        x = x.copy()
        x[-k:] *= np.linspace(1, 0, k)
    with tempfile.TemporaryDirectory() as tmp:
        wav_path = Path(tmp) / "a.wav"
        with wave.open(str(wav_path), "wb") as w:
            w.setnchannels(1)
            w.setsampwidth(2)
            w.setframerate(SR)
            w.writeframes((x * 32767).astype("<i2").tobytes())
        out = OUT / f"{name}.ogg"
        subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", str(wav_path), "-c:a", "libvorbis", "-q:a", "5", str(out)], check=True)
    print("saved", name, f"{len(x) / SR:.1f}s")


# ---------------------------------------------------------------- 악기

def pluck(freq, dur, bright=1.0):
    """류트/하프 같은 뜯는 소리: 높은 배음일수록 빨리 사라짐"""
    t = t_of(dur)
    y = np.zeros_like(t)
    for k in range(1, 9):
        if freq * k > SR / 2.2:
            break
        y += (1 / k ** (1.3 / bright)) * np.sin(2 * np.pi * freq * k * t + k) * np.exp(-t * (2.2 + k * 1.6))
    return y * env(len(t), 0.003, 0.03, dur)


def pad(freq, dur, vib=0.0):
    """부드럽게 깔리는 화음 (현악/합창 느낌)"""
    t = t_of(dur)
    y = np.zeros_like(t)
    for detune in (-0.12, 0.0, 0.11):
        f = freq * 2 ** (detune / 12) * (1 + vib * 0.004 * np.sin(2 * np.pi * 5.2 * t))
        ph = 2 * np.pi * np.cumsum(f) / SR
        for k in range(1, 7):
            y += (0.55 / k ** 1.4) * np.sin(ph * k)
    y = lowpass(y, 1800)
    return y * env(len(t), min(0.35, dur * 0.3), min(0.4, dur * 0.3), dur)


def bass(freq, dur, grit=0.3):
    t = t_of(dur)
    y = np.sin(2 * np.pi * freq * t) + grit * np.sin(4 * np.pi * freq * t) + grit * 0.4 * np.sin(6 * np.pi * freq * t)
    return y * env(len(t), 0.004, 0.04, dur) * np.exp(-t * 1.2)


def brass(freq, dur):
    """금관 느낌의 멜로디 (배음이 많고 살짝 떨림)"""
    t = t_of(dur)
    f = freq * (1 + 0.005 * np.sin(2 * np.pi * 5.5 * t) * np.clip(t / 0.25, 0, 1))
    ph = 2 * np.pi * np.cumsum(f) / SR
    y = np.zeros_like(t)
    for k in range(1, 11):
        y += (1 / k) * np.sin(ph * k)
    bright = np.clip(t / 0.06, 0, 1)
    y = lowpass(y, 2600) * (0.7 + 0.3 * bright)
    return y * env(len(t), 0.03, 0.08, dur)


def flute(freq, dur):
    t = t_of(dur)
    f = freq * (1 + 0.004 * np.sin(2 * np.pi * 5 * t) * np.clip(t / 0.3, 0, 1))
    ph = 2 * np.pi * np.cumsum(f) / SR
    y = np.sin(ph) + 0.18 * np.sin(2 * ph) + 0.06 * np.sin(3 * ph)
    y += 0.03 * lowpass(noise(dur), 3000)
    return y * env(len(t), 0.06, 0.12, dur)


def strings_stab(freq, dur):
    t = t_of(dur)
    y = np.zeros_like(t)
    for k in range(1, 9):
        y += (1 / k) * np.sin(2 * np.pi * freq * k * t)
    return lowpass(y, 1500) * env(len(t), 0.01, 0.05, dur) * np.exp(-t * 3)


def kick(dur=0.35):
    t = t_of(dur)
    f = 45 + 95 * np.exp(-t * 28)
    ph = 2 * np.pi * np.cumsum(f) / SR
    return np.sin(ph) * np.exp(-t * 9) + 0.15 * noise(dur) * np.exp(-t * 80)


def snare(dur=0.25):
    t = t_of(dur)
    body = np.sin(2 * np.pi * 190 * t) * np.exp(-t * 22)
    sn = highpass(noise(dur), 1200) * np.exp(-t * 16)
    return 0.5 * body + 0.8 * sn


def hat(dur=0.06, open_=False):
    t = t_of(dur if not open_ else 0.25)
    return highpass(noise(len(t) / SR), 6000) * np.exp(-t * (14 if open_ else 70))


def tom(freq=110, dur=0.45):
    t = t_of(dur)
    f = freq * (1 + 0.5 * np.exp(-t * 18))
    ph = 2 * np.pi * np.cumsum(f) / SR
    return np.sin(ph) * np.exp(-t * 6) + 0.1 * lowpass(noise(dur), 1500) * np.exp(-t * 20)


# ---------------------------------------------------------------- 곡 만들기 도우미

class Song:
    """음표를 박자 위치에 놓아서 곡을 만들고, 반복해도 끊기지 않게 이어 붙임"""

    def __init__(self, bpm, bars, beats_per_bar=4):
        self.beat = 60 / bpm
        self.length = int(SR * self.beat * beats_per_bar * bars)
        self.buf = np.zeros(self.length * 3)

    def put(self, beat_pos, sound, gain=1.0):
        start = int(SR * beat_pos * self.beat)
        # 같은 곡을 두 번 이어 그려서, 두 번째 반복 구간을 잘라 쓰면 꼬리 소리까지 자연스럽게 이어짐
        for rep in (0, 1):
            s = start + rep * self.length
            e = min(s + len(sound), len(self.buf))
            self.buf[s:e] += sound[:e - s] * gain

    def render(self, wet=0.2, verb=1.4):
        y = reverb(self.buf, verb, wet)
        return normalize(y[self.length:2 * self.length], 0.8)


CHORDS = {
    # 이름: (뿌리음, 화음 구성)
    "D": ("D", [0, 4, 7]), "Bm": ("B", [0, 3, 7]), "G": ("G", [0, 4, 7]), "A": ("A", [0, 4, 7]),
    "F#m": ("F#", [0, 3, 7]), "Em": ("E", [0, 3, 7]), "Asus": ("A", [0, 5, 7]),
    "Am": ("A", [0, 3, 7]), "F": ("F", [0, 4, 7]), "C": ("C", [0, 4, 7]), "E": ("E", [0, 4, 7]),
    "Dm": ("D", [0, 3, 7]), "Bb": ("Bb", [0, 4, 7]), "Eb": ("Eb", [0, 4, 7]),
}


def chord_notes(name, octave):
    root, tones = CHORDS[name]
    base = n(f"{root}{octave}")
    return [base + i for i in tones]


def melody(song, notes, start_beat, voice, gain, transpose=0):
    pos = start_beat
    for name, length in notes:
        if name != "-":
            song.put(pos, voice(midi(n(name) + transpose), length * song.beat * 0.98), gain)
        pos += length


# ---------------------------------------------------------------- 배경음악

def bgm_camp():
    s = Song(bpm=80, bars=8)
    prog = ["D", "Bm", "G", "A", "D", "F#m", "G", "Asus"]
    arp = [0, 1, 2, 1, 0, 2, 1, 2]  # 8분음표 분산화음
    for bar, ch in enumerate(prog):
        b0 = bar * 4
        tones = chord_notes(ch, 4)
        tones = tones + [tones[0] + 12]
        for i, idx in enumerate(arp):
            note = tones[idx if i % 4 != 3 else 3]
            s.put(b0 + i * 0.5, pluck(midi(note), 1.6, 0.9), 0.32)
        for tone in chord_notes(ch, 3):
            s.put(b0, pad(midi(tone), 4 * s.beat * 1.05), 0.06)
        root = chord_notes(ch, 2)[0]
        s.put(b0, bass(midi(root), 1.8 * s.beat, 0.15), 0.35)
        s.put(b0 + 2, bass(midi(root + 7), 1.8 * s.beat, 0.15), 0.25)
    # 후반 4마디에 잔잔한 피리 멜로디
    tune = [("F#5", 1.5), ("E5", 0.5), ("D5", 2),
            ("D5", 1), ("F#5", 1), ("A5", 2),
            ("B5", 1.5), ("A5", 0.5), ("G5", 1), ("F#5", 1),
            ("E5", 3), ("-", 1)]
    melody(s, tune, 16, flute, 0.22)
    return s.render(wet=0.28, verb=1.8)


def bgm_battle():
    s = Song(bpm=138, bars=16)
    prog = ["Am", "F", "C", "G", "Am", "F", "G", "E"] * 2
    for bar, ch in enumerate(prog):
        b0 = bar * 4
        root = chord_notes(ch, 2)[0]
        # 질주하는 베이스 (8분음표, 옥타브 왕복)
        for i in range(8):
            s.put(b0 + i * 0.5, bass(midi(root + (12 if i % 2 else 0)), 0.45 * s.beat, 0.5), 0.32)
        # 화음 끊어 치기
        for k, beat in enumerate((0, 1.5, 3)):
            for tone in chord_notes(ch, 4):
                s.put(b0 + beat, strings_stab(midi(tone), 0.5), 0.07)
        # 드럼
        s.put(b0, kick(), 0.8)
        s.put(b0 + 2, kick(), 0.7)
        if bar % 2 == 1:
            s.put(b0 + 2.5, kick(), 0.5)
        s.put(b0 + 1, snare(), 0.45)
        s.put(b0 + 3, snare(), 0.45)
        for i in range(8):
            s.put(b0 + i * 0.5, hat(), 0.18 if i % 2 else 0.12)
    tune = [("A4", 1), ("C5", 0.5), ("E5", 0.5), ("D5", 1), ("C5", 1),
            ("A4", 1.5), ("F4", 0.5), ("A4", 1), ("C5", 1),
            ("G4", 1), ("E5", 1), ("D5", 0.5), ("C5", 0.5), ("D5", 1),
            ("B4", 2), ("G4", 1), ("B4", 1),
            ("A4", 0.5), ("B4", 0.5), ("C5", 1), ("E5", 1), ("A5", 1),
            ("G5", 1), ("F5", 0.5), ("E5", 0.5), ("F5", 1), ("A5", 1),
            ("G5", 1.5), ("F5", 0.5), ("E5", 1), ("D5", 1),
            ("E5", 2), ("G#4", 1), ("B4", 1)]
    melody(s, tune, 0, brass, 0.22)
    melody(s, tune, 32, brass, 0.2)
    melody(s, tune, 32, brass, 0.1, transpose=-12)  # 두 번째는 아래 옥타브를 겹쳐서 더 웅장하게
    return s.render(wet=0.16, verb=1.1)


def bgm_boss():
    s = Song(bpm=148, bars=16)
    prog = ["Dm", "Bb", "C", "Dm", "Dm", "Eb", "C", "A"] * 2
    ostinato = [0, 0, 12, 0, 7, 0, 10, 12]
    for bar, ch in enumerate(prog):
        b0 = bar * 4
        root = chord_notes(ch, 2)[0]
        for i, step in enumerate(ostinato):
            s.put(b0 + i * 0.5, strings_stab(midi(root + step), 0.32), 0.2)
            s.put(b0 + i * 0.5, bass(midi(root - 12 + (step if step == 12 else 0)), 0.42 * s.beat, 0.6), 0.22)
        for tone in chord_notes(ch, 3):
            s.put(b0, pad(midi(tone), 4 * s.beat * 1.05, vib=1.0), 0.07)
        s.put(b0, tom(90), 0.7)
        s.put(b0, kick(), 0.6)
        s.put(b0 + 2, snare(), 0.55)
        s.put(b0 + 2.5, tom(120), 0.45)
        s.put(b0 + 3, tom(100), 0.5)
        s.put(b0 + 3.5, tom(80), 0.55)
        hats = 16 if bar >= 8 else 8
        for i in range(hats):
            s.put(b0 + i * 4 / hats, hat(), 0.1)
    tune = [("D5", 3), ("F5", 1),
            ("D5", 2), ("F5", 1), ("D5", 1),
            ("E5", 2), ("G5", 1), ("E5", 1),
            ("D5", 4),
            ("A5", 2), ("G5", 1), ("F5", 1),
            ("G5", 2), ("Eb5", 2),
            ("E5", 2), ("C5", 1), ("E5", 1),
            ("C#5", 3), ("E5", 1)]
    melody(s, tune, 0, brass, 0.24)
    melody(s, tune, 32, brass, 0.22)
    melody(s, tune, 32, brass, 0.14, transpose=-12)
    return s.render(wet=0.2, verb=1.6)


# ---------------------------------------------------------------- 효과음

def sweep_noise(dur, f0, f1, decay):
    """주파수가 f0 -> f1로 움직이는 '휙' 소리"""
    x = noise(dur)
    t = t_of(dur)
    out = np.empty_like(x)
    acc = 0.0
    low = 0.0
    for i in range(len(x)):
        f = f0 + (f1 - f0) * (i / len(x))
        a = 1 - np.exp(-2 * np.pi * f / SR)
        acc += a * (x[i] - acc)       # 저역 통과
        low += (a * 0.25) * (acc - low)
        out[i] = acc - low            # 간단한 대역 통과
    return out * np.exp(-t * decay) * env(len(t), 0.01, 0.03, dur)


def bell(freq, dur, decay=4.0):
    t = t_of(dur)
    y = np.zeros_like(t)
    for ratio, amp in ((1, 1), (2.0, 0.5), (2.76, 0.35), (5.4, 0.2), (8.9, 0.1)):
        y += amp * np.sin(2 * np.pi * freq * ratio * t) * np.exp(-t * decay * (0.6 + ratio * 0.25))
    return y * env(len(t), 0.002, 0.05, dur)


def mix(length, *parts):
    y = np.zeros(int(SR * length))
    for start, sound, gain in parts:
        s = int(SR * start)
        e = min(len(y), s + len(sound))
        y[s:e] += sound[:e - s] * gain
    return y


def sfx():
    out = {}
    out["click"] = normalize(mix(0.08, (0, bell(1800, 0.08, 40), 0.6), (0, highpass(noise(0.01), 3000), 0.3)), 0.6)
    out["slash"] = normalize(mix(0.25, (0, sweep_noise(0.22, 5000, 1200, 9), 1.0), (0.05, bell(2400, 0.15, 30), 0.12)), 0.7)
    out["stab"] = normalize(mix(0.18, (0, sweep_noise(0.1, 3500, 2000, 25), 0.8), (0.04, kick(0.14), 0.5)), 0.7)
    out["heavy"] = normalize(mix(0.4, (0, sweep_noise(0.18, 1800, 500, 10), 0.8), (0.06, kick(0.32), 1.0),
                                 (0.06, lowpass(noise(0.25), 900) * np.exp(-t_of(0.25) * 14), 0.6)), 0.8)
    chomp = lowpass(noise(0.07), 1600) * np.exp(-t_of(0.07) * 40)
    out["bite"] = normalize(mix(0.2, (0, chomp, 1.0), (0.08, chomp, 0.9), (0, tom(140, 0.12), 0.4)), 0.7)
    out["arrow"] = normalize(mix(0.32, (0, pluck(196, 0.2, 1.5), 0.7), (0.02, sweep_noise(0.28, 6000, 2500, 8), 0.6)), 0.6)
    t = t_of(0.45)
    chirp = np.sin(2 * np.pi * np.cumsum(600 + 900 * (t / t[-1]) ** 0.7) / SR)
    shimmer = chirp * (0.6 + 0.4 * np.sin(2 * np.pi * 28 * t)) * np.exp(-t * 4) * env(len(t), 0.02, 0.08, 0.45)
    out["magic"] = normalize(mix(0.5, (0, shimmer, 0.8), (0.05, bell(1320, 0.4, 7), 0.3), (0.12, bell(1760, 0.3, 9), 0.2)), 0.6)
    boom = lowpass(noise(0.5), 700) * np.exp(-t_of(0.5) * 7)
    out["explode"] = normalize(mix(0.55, (0, boom, 1.0), (0, kick(0.4), 0.8), (0, highpass(noise(0.12), 2500) * np.exp(-t_of(0.12) * 30), 0.3)), 0.8)
    out["heal"] = normalize(mix(0.8, (0, bell(880, 0.7, 3), 0.5), (0.08, bell(1320, 0.6, 3.5), 0.4), (0.16, bell(1760, 0.5, 4), 0.35)), 0.55)
    t = t_of(0.3)
    drop = np.sin(2 * np.pi * np.cumsum(420 - 280 * (t / t[-1])) / SR) * np.exp(-t * 9)
    out["die"] = normalize(mix(0.32, (0, lowpass(noise(0.3), 1200) * np.exp(-t * 11), 0.8), (0, drop, 0.4)), 0.6)
    arp = [n("C6"), n("E6"), n("G6"), n("C7")]
    out["gacha"] = normalize(mix(0.9, *[(i * 0.06, bell(midi(m), 0.6, 5), 0.5) for i, m in enumerate(arp)]), 0.6)
    fan = [n("C5"), n("E5"), n("G5"), n("C6"), n("E6"), n("G6")]
    parts = [(i * 0.07, bell(midi(m), 1.0, 3), 0.45) for i, m in enumerate(fan)]
    parts += [(0.42, brass(midi(n("C5")), 0.8), 0.3), (0.42, brass(midi(n("G5")), 0.8), 0.25), (0.42, brass(midi(n("E5")), 0.8), 0.25)]
    out["rare"] = normalize(reverb(mix(1.5, *parts), 1.0, 0.25), 0.7)
    win = [("C5", 0, 0.18), ("E5", 0.15, 0.18), ("G5", 0.3, 0.18), ("C6", 0.45, 0.9)]
    parts = [(st, brass(midi(n(m)), d), 0.5) for m, st, d in win]
    parts += [(0.45, brass(midi(n("G5")), 0.9), 0.3), (0.45, brass(midi(n("E5")), 0.9), 0.3), (0.45, kick(), 0.4)]
    out["win"] = normalize(reverb(mix(1.6, *parts), 1.2, 0.25), 0.75)
    lose = [("E4", 0, 0.4), ("D#4", 0.4, 0.4), ("D4", 0.8, 0.4), ("C#4", 1.2, 0.9)]
    out["lose"] = normalize(reverb(mix(2.3, *[(st, flute(midi(n(m)), d) * 0.6 + pad(midi(n(m)) / 2, d) * 0.4, 0.6) for m, st, d in lose]), 1.4, 0.3), 0.6)
    t = t_of(0.9)
    air = lowpass(noise(0.9), 2500) * np.sin(np.pi * t / t[-1]) ** 2
    tone = (np.sin(2 * np.pi * 1046 * t) + np.sin(2 * np.pi * 1052 * t) + 0.5 * np.sin(2 * np.pi * 1568 * t)) * np.sin(np.pi * t / t[-1]) ** 2
    out["whisper"] = normalize(reverb(air * 0.5 + tone * 0.25, 1.0, 0.35), 0.4)
    return out


if __name__ == "__main__":
    save("bgm_camp", bgm_camp(), fade=False)    # 배경음악은 반복 재생되도록 끝을 그대로 둠
    save("bgm_battle", bgm_battle(), fade=False)
    save("bgm_boss", bgm_boss(), fade=False)
    for name, sound in sfx().items():
        save("sfx_" + name, sound)
