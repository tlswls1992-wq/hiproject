"""
몬스터 그림을 게임용으로 바꾸는 스크립트입니다. (Claude가 사용)

입력: 몬스터 폴더 (full_body.png, dialogue_portrait.png, idle/ run/ attack/ 의 <동작>_01~08.png, manifest.json)
출력: Assets/Resources/Enemies/<적 id>/
  full.png, portrait.png, idle/ run/ attack/ (가로 512 x 세로 384 캔버스, 발은 아래 16픽셀 선), anim.txt

- 몬스터 그림은 왼쪽(용사 쪽)을 보고 있어요. (anim.txt에 facing=left)
- 동작마다 그려진 크기가 다를 수 있어서 scale로 맞춘 뒤, 몬스터마다 정한 키(용사 대비)로 맞춰요.

사용법: python3 Art/convert_monster.py <몬스터 폴더> <적 id> <용사 대비 키> [동작=배율 ...]
예)     python3 Art/convert_monster.py monster_set/wolf wolf 0.55 run=1.12 attack=0.78
"""
import json
import sys
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
UNIT = 256                          # 이 그림에서 '용사 캔버스 한 칸(512픽셀)'에 해당하는 픽셀 수 (원본이 작아서 절반 밀도로 저장)
CANVAS_W, CANVAS_H = 512, 384       # 용사보다 큰 캔버스(1.5칸 높이): 일어서서 공격하는 큰 몬스터도 잘리지 않게
FEET = CANVAS_H - 16                # 발 높이: 아래에서 16픽셀 (용사 캔버스의 발 선과 같은 비율)
HERO_HEIGHT = 440 * UNIT / 512      # 용사 대기 그림의 키 (이 그림의 픽셀 기준)


def frames(folder: Path, motion: str):
    files = sorted(folder.glob(f"{motion}_[0-9][0-9].png"))
    return [Image.open(f).convert("RGBA") for f in files]


def main():
    src, enemy_id, rel_height = Path(sys.argv[1]), sys.argv[2], float(sys.argv[3])
    fixes = {k: float(v) for k, v in (a.split("=") for a in sys.argv[4:])}
    out = ROOT / "Assets" / "Resources" / "Enemies" / enemy_id
    out.mkdir(parents=True, exist_ok=True)

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
            canvas = Image.new("RGBA", (CANVAS_W, CANVAS_H), (0, 0, 0, 0))
            canvas.paste(big, (round(CANVAS_W / 2 - cx * k), round(FEET - bottom * k)), big)
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
    (out / "anim.txt").write_text("\n".join(anim) + "\n", encoding="utf-8")
    print("anim:", anim[-3:])


if __name__ == "__main__":
    main()
