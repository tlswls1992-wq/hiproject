"""
캐릭터 이미지를 Unity에서 쓸 수 있게 변환하는 스크립트입니다. (Claude가 사용)

입력 (Art/originals/<캐릭터 id>/):
  portrait.*  초상화 (도감, 대화)
  full.*      전신 (뽑기 카드)
  preview.*   대기 모습 (야영지, 전투 대기) - 움직이는 이미지면 여러 장면
  run.*       달리는 모습 (전투 중 이동) - 한 장이면 코드로 위아래 흔들림을 줌
  attack.*    공격 모습 (전투) - 움직이는 이미지
출력 (Assets/Resources/Characters/<캐릭터 id>/):
  portrait.png, full.png, idle/idle_00.png..., run/run_00.png..., attack/attack_00.png..., anim.txt (장면별 시간)

- Unity는 webp를 읽지 못하고 gif는 첫 장면만 읽기 때문에 png로 바꿉니다.
- 단색 배경은 가장자리에서부터 지워서 투명하게 만듭니다.
"""
import sys
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parent.parent


def find(folder: Path, name: str):
    for p in folder.glob(name + ".*"):
        return p
    return None


def remove_background(img: Image.Image, tolerance=26, soft=55) -> Image.Image:
    """가장자리와 이어진, 배경색과 비슷한 픽셀을 투명하게 만듭니다."""
    rgba = np.array(img.convert("RGBA")).astype(np.int32)
    h, w = rgba.shape[:2]
    if rgba[..., 3].min() < 255 and (rgba[0, 0, 3] == 0):
        return img.convert("RGBA")  # 이미 배경이 투명함
    corners = [rgba[0, 0, :3], rgba[0, w - 1, :3], rgba[h - 1, 0, :3], rgba[h - 1, w - 1, :3]]
    bg = np.median(np.array(corners), axis=0)
    dist = np.sqrt(((rgba[..., :3] - bg) ** 2).sum(axis=2))

    mask = np.zeros((h, w), dtype=bool)
    q = deque()
    for x in range(w):
        for y in (0, h - 1):
            if dist[y, x] <= tolerance and not mask[y, x]:
                mask[y, x] = True
                q.append((y, x))
    for y in range(h):
        for x in (0, w - 1):
            if dist[y, x] <= tolerance and not mask[y, x]:
                mask[y, x] = True
                q.append((y, x))
    while q:
        y, x = q.popleft()
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            ny, nx = y + dy, x + dx
            if 0 <= ny < h and 0 <= nx < w and not mask[ny, nx] and dist[ny, nx] <= tolerance:
                mask[ny, nx] = True
                q.append((ny, nx))

    alpha = np.where(mask, 0, 255).astype(np.float64)
    # 배경 바로 옆의 비슷한 색 픽셀은 반투명하게 (테두리 번짐 줄이기)
    edge = np.zeros_like(mask)
    edge[1:, :] |= mask[:-1, :]
    edge[:-1, :] |= mask[1:, :]
    edge[:, 1:] |= mask[:, :-1]
    edge[:, :-1] |= mask[:, 1:]
    soft_zone = edge & ~mask & (dist < soft)
    alpha[soft_zone] = np.clip((dist[soft_zone] - tolerance) / (soft - tolerance), 0, 1) * 255
    rgba[..., 3] = alpha.astype(np.int32)
    return Image.fromarray(rgba.astype(np.uint8), "RGBA")


def frames_of(path: Path):
    im = Image.open(path)
    frames, durations = [], []
    for i in range(getattr(im, "n_frames", 1)):
        im.seek(i)
        frames.append(im.convert("RGBA").copy())
        durations.append(int(im.info.get("duration", 100) or 100))
    return frames, durations


def fit(img: Image.Image, max_size: int) -> Image.Image:
    scale = min(1.0, max_size / max(img.size))
    if scale < 1.0:
        img = img.resize((round(img.width * scale), round(img.height * scale)), Image.LANCZOS)
    return img


FEET_LINE = 0.94  # 모든 동작(대기/달리기/공격)의 발 높이를 그림 아래 6% 지점으로 맞춤 (게임의 CharacterArt.FeetPivot과 같게)


def align_feet(frames):
    """동작마다 발 위치가 다르면 바꿀 때 캐릭터가 들썩이므로, 장면들의 발 높이를 같은 선에 맞춥니다."""
    bottom = max(f.getbbox()[3] for f in frames if f.getbbox())
    shift = round(frames[0].height * FEET_LINE) - bottom
    out = []
    for f in frames:
        moved = Image.new("RGBA", f.size, (0, 0, 0, 0))
        moved.paste(f, (0, shift), f)
        out.append(moved)
    return out


def convert(char_id: str):
    src = ROOT / "Art" / "originals" / char_id
    out = ROOT / "Assets" / "Resources" / "Characters" / char_id
    out.mkdir(parents=True, exist_ok=True)
    anim_lines = []

    for name, size in (("portrait", 768), ("full", 1024)):
        p = find(src, name)
        if p:
            fit(remove_background(Image.open(p)), size).save(out / f"{name}.png", optimize=True)
            print("saved", name)

    for name, folder in (("preview", "idle"), ("run", "run"), ("attack", "attack")):
        p = find(src, name)
        if not p:
            continue
        (out / folder).mkdir(exist_ok=True)
        for old in (out / folder).glob("*.png"):
            old.unlink()
        frames, durations = frames_of(p)
        frames = align_feet([fit(remove_background(f), 512) for f in frames])
        for i, f in enumerate(frames):
            f.save(out / folder / f"{folder}_{i:02d}.png", optimize=True)
        anim_lines.append(f"{folder}=" + ",".join(str(d) for d in durations))
        print("saved", folder, len(frames), "frames")

    (out / "anim.txt").write_text("\n".join(anim_lines) + "\n", encoding="utf-8")


if __name__ == "__main__":
    convert(sys.argv[1] if len(sys.argv) > 1 else "hero")
