"""
몬스터 그림을 게임용으로 바꾸는 스크립트입니다. (Claude가 사용)

입력: 몬스터 폴더 (full_body.png, dialogue_portrait.png, idle/ run/ attack/ 의 <동작>_01~08.png, manifest.json)
출력: Assets/Resources/Enemies/<적 id>/
  full.png, portrait.png, idle/ run/ attack/ (가로 512 x 세로 384 캔버스, 발은 아래 16픽셀 선), anim.txt

- 몬스터 그림은 왼쪽(용사 쪽)을 보고 있어요. (anim.txt에 facing=left)
- 동작마다 그려진 크기가 다를 수 있어서 scale로 맞춘 뒤, 몬스터마다 정한 키(용사 대비)로 맞춰요.

사용법: python3 Art/convert_monster.py <몬스터 폴더> <적 id> <용사 대비 키> [동작=배율 ...] [width=768] [--clean]
  --clean  옆 칸 캐릭터 조각이 섞여 든 장면을 정리 (몸에서 떨어져 가장자리에 붙은 조각 제거)
예)     python3 Art/convert_monster.py monster_set/wolf wolf 0.55 run=1.12 attack=0.78
"""
import json
import sys

import numpy as np
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
UNIT = 256                          # 이 그림에서 '용사 캔버스 한 칸(512픽셀)'에 해당하는 픽셀 수 (원본이 작아서 절반 밀도로 저장)
CANVAS_W, CANVAS_H = 512, 384       # 용사보다 큰 캔버스(1.5칸 높이): 일어서서 공격하는 큰 몬스터도 잘리지 않게
FEET = CANVAS_H - 16                # 발 높이: 아래에서 16픽셀 (용사 캔버스의 발 선과 같은 비율)
HERO_HEIGHT = 440 * UNIT / 512      # 용사 대기 그림의 키 (이 그림의 픽셀 기준)


def clean_edges(img: Image.Image) -> Image.Image:
    """장면표에서 잘라낼 때 옆 칸 캐릭터의 조각(칸 가장자리에 붙은 발 · 선 따위)이 섞여 들어온 것을 지웁니다.
    몸통(가장 큰 덩어리)과 떨어져 있으면서 그림 가장자리에 닿은 덩어리만 지움 (무기 · 방패처럼 몸에 붙은 건 그대로)"""
    a = np.array(img)
    solid = a[:, :, 3] > 40
    h, w = solid.shape
    bb = img.getbbox()
    if not bb:
        return img
    label = np.zeros((h, w), np.int32)
    parts = []
    for y0, x0 in zip(*np.nonzero(solid)):
        if label[y0, x0]:
            continue
        n = len(parts) + 1
        label[y0, x0] = n
        stack, pts = [(y0, x0)], []
        while stack:
            y, x = stack.pop()
            pts.append((y, x))
            for dy in (-1, 0, 1):
                for dx in (-1, 0, 1):
                    ny, nx = y + dy, x + dx
                    if 0 <= ny < h and 0 <= nx < w and solid[ny, nx] and not label[ny, nx]:
                        label[ny, nx] = n
                        stack.append((ny, nx))
        ys, xs = zip(*pts)
        parts.append((len(pts), min(xs), min(ys), max(xs), max(ys), n))
    parts.sort(reverse=True)
    edge = 3
    for size, x1, y1, x2, y2, n in parts[1:]:
        if x1 <= bb[0] + edge or x2 >= bb[2] - 1 - edge or y1 <= bb[1] + edge or y2 >= bb[3] - 1 - edge:
            near = label == n
            # 투명하게 + 반투명 가장자리 1픽셀도 함께
            grow = near.copy()
            grow[1:] |= near[:-1]; grow[:-1] |= near[1:]; grow[:, 1:] |= near[:, :-1]; grow[:, :-1] |= near[:, 1:]
            a[grow & (label == 0) | near, 3] = 0
    return Image.fromarray(a)


CLEAN = False  # --clean 이면 모든 장면에 clean_edges 적용


def frames(folder: Path, motion: str):
    files = sorted(folder.glob(f"{motion}_[0-9][0-9].png"))
    imgs = [Image.open(f).convert("RGBA") for f in files]
    return [clean_edges(i) for i in imgs] if CLEAN else imgs


def main():
    global CLEAN
    args = [a for a in sys.argv[1:] if a != "--clean"]
    CLEAN = "--clean" in sys.argv
    src, enemy_id, rel_height = Path(args[0]), args[1], float(args[2])
    fixes = {k: float(v) for k, v in (a.split("=") for a in args[3:])}
    # width=768: 앞으로 크게 덤비는 큰 몬스터는 캔버스를 넓혀서 잘리지 않게 (게임은 캔버스 가운데를 기준으로 그림)
    canvas_w = int(fixes.pop("width", CANVAS_W))
    # scale=0.9: 전투에서 그림 전체 크기 배율 (anim.txt의 scale, 다시 변환해도 남도록)
    battle_scale = fixes.pop("scale", None)
    out = ROOT / "Assets" / "Resources" / "Enemies" / enemy_id
    out.mkdir(parents=True, exist_ok=True)

    # manifest.json: 몬스터 폴더 안에 있으면 그 파일, 여러 몬스터를 묶은 폴더에 있으면 그 안의 몬스터 이름 항목
    if (src / "manifest.json").exists():
        info = json.loads((src / "manifest.json").read_text())
    else:
        manifest = json.loads((src.parent / "manifest.json").read_text()) if (src.parent / "manifest.json").exists() else {}
        info = manifest.get(src.name, {})

    # 기준: 대기 첫 장면의 키를 '용사 키 x rel_height'로
    idle0 = frames(src / "idle", "idle")[0]
    bb = idle0.getbbox()
    base = (HERO_HEIGHT * rel_height) / (bb[3] - bb[1])

    anim, top_frac, body_half = [], 0.0, 0.0
    for motion in ("idle", "run", "attack"):
        fr = frames(src / motion, motion)
        if not fr:
            continue
        k = base * fixes.get(motion, 1.0)
        boxes = [f.getbbox() for f in fr]
        # 첫 장면의 가운데를 캔버스 가운데에 (공격 때 앞으로 덤비는 움직임은 그대로 살림), 바닥은 발 선에
        cx = (boxes[0][0] + boxes[0][2]) / 2
        bottom = max(b[3] for b in boxes)
        d = out / motion
        d.mkdir(exist_ok=True)
        for old in d.glob("*.png"):
            old.unlink()
        for i, f in enumerate(fr):
            big = f.resize((max(1, round(f.width * k)), max(1, round(f.height * k))), Image.LANCZOS)
            canvas = Image.new("RGBA", (canvas_w, CANVAS_H), (0, 0, 0, 0))
            canvas.paste(big, (round(canvas_w / 2 - cx * k), round(FEET - bottom * k)), big)
            canvas.save(d / f"{motion}_{i:02d}.png", optimize=True)
            if motion == "idle" and i == 0:
                b2 = canvas.getbbox()
                top_frac = (FEET - b2[1]) / UNIT            # 발에서 머리끝까지 (512픽셀 = 캔버스 1칸 기준)
                body_half = (b2[2] - b2[0]) / UNIT / 2       # 몸의 반폭 (같은 기준)
        ms = info.get(motion, {}).get("duration_ms", 100)
        anim.append(f"{motion}=" + ",".join([str(ms)] * len(fr)))
        print("saved", motion, len(fr), "frames, scale %.2f" % k)

    for name, file, size in (("full", "full_body.png", 1024), ("portrait", "dialogue_portrait.png", 768)):
        p = src / file
        if p.exists():
            img = Image.open(p).convert("RGBA")
            img.thumbnail((size, size), Image.LANCZOS)
            img.save(out / f"{name}.png", optimize=True)
    anim += ["facing=left", f"unit={UNIT}", f"feet={CANVAS_H - FEET}", f"top={top_frac:.3f}", f"body={body_half:.3f}"]
    if battle_scale:
        anim.append(f"scale={battle_scale:g}")
    (out / "anim.txt").write_text("\n".join(anim) + "\n", encoding="utf-8")
    print("anim:", anim[-3:])


if __name__ == "__main__":
    main()
