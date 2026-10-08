"""
레이어로 나뉜 전투 배경(배경 + 바람에 흔들리는 풀/가지 조각)을 게임용으로 옮기는 스크립트입니다. (Claude가 사용)

입력 폴더 (압축을 푼 폴더):
  background_static.png   움직이지 않는 배경
  foliage/*.png           흔들리는 조각들
  layers.json             조각 위치 (canvas, layers[sprite, size, anchor_in_sprite, anchor_in_scene, phase_radians, motion])

출력: Assets/Resources/StageScenes/<이름>/
  background.jpg, <조각>.png, layout.txt (게임이 읽는 위치 정보)

사용법: python3 Art/import_scene.py <압축 푼 폴더> <이름> --zoom 1.2 --lanes 470,690
  --zoom   배경을 아래쪽 기준으로 얼마나 확대할지 (땅이 화면에 더 넓게 보이도록)
  --lanes  원본 그림(1280x720) 기준으로 유닛이 다닐 땅의 위쪽/아래쪽 y 좌표
"""
import argparse
import json
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent.parent


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("src")
    ap.add_argument("name")
    ap.add_argument("--zoom", type=float, default=1.0)
    ap.add_argument("--lanes", default="470,690")
    args = ap.parse_args()

    src = Path(args.src)
    out = ROOT / "Assets" / "Resources" / "StageScenes" / args.name
    out.mkdir(parents=True, exist_ok=True)
    for old in out.glob("*"):
        old.unlink()

    layout = json.loads((src / "layers.json").read_text())
    Image.open(src / "background_static.png").convert("RGB").save(out / "background.jpg", quality=92)

    lines = [
        f"canvas={layout['canvas'][0]},{layout['canvas'][1]}",
        f"zoom={args.zoom}",
        f"lanes={args.lanes}",
    ]
    saved = {}

    def sprite(rel, biggest_w):
        """조각 그림을 복사 (화면에서 쓰이는 크기의 2배까지만 남겨 용량 절약). 게임 안 이름을 돌려줌"""
        name = Path(rel).stem
        if name in saved:
            return name
        img = Image.open(src / rel)
        limit = max(8, int(biggest_w * 2))
        if img.width > limit:
            img = img.resize((limit, round(img.height * limit / img.width)), Image.LANCZOS)
        img.save(out / f"{name}.png", optimize=True)
        saved[name] = True
        return name

    def num(v):
        return f"{v:g}" if isinstance(v, (int, float)) else str(v)

    def emit(kind, *values):
        lines.append(kind + "=" + ",".join(num(v) for v in values))

    duration = layout.get("duration_seconds", 8)
    extra = json.loads((src / "extra.json").read_text()) if (src / "extra.json").exists() else {}

    # 1) 바람에 흔들리는 풀/가지 (layers)
    for layer in layout.get("layers", []) + extra.get("sway", []):
        if not isinstance(layer, dict):
            continue
        w, h = layer["size"]
        name = sprite(layer["sprite"], w)
        ax, ay = layer["anchor_in_sprite"]
        sx, sy = layer["anchor_in_scene"]
        kind = "bend" if "bend" in layer.get("motion", "bend") else "rotate"
        emit("sway", name, w, h, ax, ay, sx, sy, layer.get("phase_radians", 0), kind)

    # 2) 흘러가는 구름 (cloud_instances): 시작 위치(가운데)에서 travel만큼 이동하며 생겼다 사라짐
    for c in layout.get("cloud_instances", []) + extra.get("clouds", []):
        w, h = c["size"]
        name = sprite(c["sprite"], w)
        dx, dy = c.get("travel", [60, 0])
        emit("drift", name, c["start"][0], c["start"][1], w, h, dx, dy, c.get("period", duration), c.get("phase", 0), c.get("opacity", 1))

    # 3) 달리는 말 무리 (horse_layers)
    if "horse_layers" in layout:
        frames = sorted((src / "horses").glob("gallop_*.png"))
        for f in frames:
            sprite(f"horses/{f.name}", 60)
        path = layout["horse_path"]
        emit("herd", "gallop_", len(frames), path["start_x"], path["travel"], duration, path.get("start_phase", 0), 12)
        for hl in layout["horse_layers"]:
            emit("horse", hl["offset"], hl["ground_y"], hl["size"][0], hl["size"][1], hl.get("phase", 0))

    # 4) 바다: 출렁이는 배, 밀려오는 물거품, 반짝이는 물빛
    if "boat" in layout:
        bt = layout["boat"]
        w, h = bt["size"]
        emit("bob", sprite("boat.png", w), bt["center"][0], bt["center"][1], w, h, bt.get("bob_pixels", 2), bt.get("roll_degrees", 0.5), 4)
        if (src / "boat_reflection.png").exists():
            ref = Image.open(src / "boat_reflection.png")
            emit("bob", sprite("boat_reflection.png", ref.width), bt["center"][0], bt["center"][1] + h / 2 + ref.height / 2 - 2,
                 ref.width, ref.height, -bt.get("bob_pixels", 2), 0, 4)
    for wv in layout.get("waves", []):
        w, h = wv["size"]
        emit("foam", sprite("wave_foam.png", 700), wv["center"][0], wv["center"][1], w, h, wv.get("angle", 0), duration, wv.get("phase", 0), wv.get("opacity", 0.4))
    if (src / "water_glints.png").exists():
        emit("glint", sprite("water_glints.png", layout["canvas"][0] / 2), 4.0, 0.35, 1.0)

    # 5) 깃발 (왼쪽 고정, 물결이 펄럭임), 화로 불꽃, 날리는 먼지
    for fl in layout.get("flag_layers", []):
        w, h = fl["size"]
        emit("flag", sprite("flag_cloth.png", w * 2), fl["hoist"][0], fl["hoist"][1], w, h, fl.get("phase", 0))
    if layout.get("braziers"):
        flames = sorted((src / "flames").glob("flame_*.png"))
        for f in flames:
            sprite(f"flames/{f.name}", 104)
        for br in layout["braziers"]:
            emit("flame", "flame_", len(flames), br["baseline"][0], br["baseline"][1], 54, 56, br.get("phase", 0))
    if layout.get("dust_count"):
        emit("dust", layout["dust_count"])

    (out / "layout.txt").write_text("\n".join(lines) + "\n", encoding="utf-8")
    print("saved", out, len(saved), "sprites,", len(lines) - 3, "moving layers")


if __name__ == "__main__":
    main()
