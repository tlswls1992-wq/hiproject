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
    saved = set()
    for layer in layout["layers"]:
        sprite = Path(layer["sprite"]).stem
        if sprite not in saved:
            img = Image.open(src / layer["sprite"])
            # 화면에서는 작게 쓰이므로 너무 큰 그림은 줄여서 용량 절약 (가장 크게 쓰이는 크기의 2배까지)
            biggest = max(l["size"][0] for l in layout["layers"] if Path(l["sprite"]).stem == sprite)
            limit = biggest * 2
            if img.width > limit:
                img = img.resize((limit, round(img.height * limit / img.width)), Image.LANCZOS)
            img.save(out / f"{sprite}.png", optimize=True)
            saved.add(sprite)
        kind = "bend" if "bend" in layer.get("motion", "") else "rotate"
        w, h = layer["size"]
        ax, ay = layer["anchor_in_sprite"]
        sx, sy = layer["anchor_in_scene"]
        lines.append(f"sway={sprite},{w},{h},{ax},{ay},{sx},{sy},{layer.get('phase_radians', 0)},{kind}")
    (out / "layout.txt").write_text("\n".join(lines) + "\n", encoding="utf-8")
    print("saved", out, len(saved), "sprites,", len(layout["layers"]), "layers")


if __name__ == "__main__":
    main()
