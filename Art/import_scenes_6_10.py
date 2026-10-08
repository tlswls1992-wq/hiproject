"""
스테이지 6~10 전투 배경(은혜의 땅, 노란 용의 동굴, 붉은 정글, 옛 대전쟁터, 마왕성)을 게임용으로 옮기는 스크립트입니다. (Claude가 사용)
다섯 배경은 움직이는 방식이 저마다 달라서 Art/import_scene.py 대신 이 스크립트를 써요.

사용법: python3 Art/import_scenes_6_10.py <은혜의땅 폴더> <동굴 폴더> <정글 폴더> <전쟁터 폴더> <마왕성 폴더>
출력:   Assets/Resources/StageScenes/stage6 ~ stage10 (background.jpg, 조각 그림, layout.txt)
layout.txt 줄의 뜻은 Assets/Scripts/BattleScene.cs 맨 위 설명을 보세요.
"""
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parent.parent


class Scene:
    def __init__(self, src, number, zoom, lanes):
        self.src = Path(src)
        self.out = ROOT / "Assets" / "Resources" / "StageScenes" / f"stage{number}"
        self.out.mkdir(parents=True, exist_ok=True)
        for old in self.out.glob("*"):
            old.unlink()
        self.layout = json.loads((self.src / "layers.json").read_text())
        self.lines = ["canvas=1280,720", f"zoom={zoom}", f"lanes={lanes[0]},{lanes[1]}"]
        Image.open(self.src / "background_static.png").convert("RGB").save(self.out / "background.jpg", quality=92)

    def save(self, img, name):
        img.save(self.out / f"{name}.png", optimize=True)
        return name

    def emit(self, kind, *values):
        self.lines.append(kind + "=" + ",".join(f"{v:g}" if isinstance(v, (int, float)) else str(v) for v in values))

    def finish(self):
        (self.out / "layout.txt").write_text("\n".join(self.lines) + "\n", encoding="utf-8")
        print("saved", self.out.name, len(self.lines) - 3, "moving layers")


def clean_alpha(img, low, span):
    """그림 둘레의 옅은 번짐을 지우고 단단한 부분만 남김 (원본 재생 코드와 같은 처리)"""
    a = np.array(img.convert("RGBA"))
    a[:, :, 3] = np.clip((a[:, :, 3].astype(float) - low) * 255 / span, 0, 255).astype("uint8")
    img = Image.fromarray(a)
    return img.crop(img.getbbox())


def land_of_grace(src):
    s = Scene(src, 6, 1.2, (478, 690))
    # 빛줄기 3개: 쏟아지는 곳(source)을 중심으로 살짝 기울며 밝아졌다 어두워짐
    for i, beam in enumerate(s.layout["beams"], 1):
        img = Image.open(s.src / f"light_shaft_{i}.png").convert("RGBA")
        box = img.getbbox()
        name = s.save(img.crop(box), f"shaft_{i}")
        sx, sy = beam["source"]
        gx, gy = beam["ground"]
        s.emit("beam", name, box[0], box[1], box[2] - box[0], box[3] - box[1], sx, sy, gx, gy, beam["phase"])
    s.emit("motes", 54)  # 빛줄기 안에서 떠오르는 먼지 (beam 줄들을 따라)
    s.finish()


def dragon_cave(src):
    s = Scene(src, 7, 1.2, (478, 690))
    s.save(Image.open(s.src / "coin_glint_sprite.png").convert("RGBA"), "coin_glint")
    # 금화 반짝임: 그림 크기는 반지름/16 배 (원본 65픽셀 = 반지름 16)
    for g in s.layout["glints"]:
        size = 65 * g["radius"] / 16
        s.emit("sparkle", "coin_glint", g["x"], g["y"], round(size, 1), g["period"], round(g["phase"], 3), round(g["duration"], 3))
    s.finish()


def crimson_jungle(src):
    s = Scene(src, 8, 1.2, (470, 680))
    leaf = Image.open(s.src / "red_leaf_cluster.png").convert("RGBA")
    leaf = leaf.crop(leaf.getbbox())
    for i, l in enumerate(s.layout["leaves"]):
        h = l["height"]
        w = round(h * leaf.width / leaf.height)
        sp = leaf.resize((w * 2, h * 2), Image.LANCZOS)  # 화면에서 쓰는 크기의 2배로 저장 (또렷하게)
        if l["flip"]:
            sp = sp.transpose(Image.FLIP_LEFT_RIGHT)
        a = np.array(sp).astype(float)
        a[:, :, :3] *= l["shade"]
        sp = Image.fromarray(a.clip(0, 255).astype("uint8"))
        # 뿌리(고정점): 맨 아래 줄에서 잎이 있는 곳의 가운데
        xs = np.where(np.array(sp.getchannel("A"))[-10:] > 64)[1]
        ax = float(np.median(xs)) / 2 if len(xs) else w / 2
        name = s.save(sp, f"leaf_{i}")
        ax_, ay_ = l["anchor_scene"]
        # 길(lanes)보다 위쪽에 뿌리를 둔 잎은 유닛 뒤에, 아래쪽 잎은 유닛 앞에
        layer = "back" if ay_ < 470 else "front"
        s.emit("sway", name, w, h, round(ax, 1), h, ax_, ay_, l["phase"], "bend", layer, 2.5)
    # 어둠 속의 눈: 뜬 눈 그림을 잘라 두고, 깜빡일 때 위아래로 감음
    eyes = Image.open(s.src / "eyes_open_overlay.png").convert("RGBA")
    box = eyes.getbbox()
    box = (box[0] - 2, box[1] - 2, box[2] + 2, box[3] + 2)
    name = s.save(eyes.crop(box), "eyes")
    e = s.layout["eyes"]
    s.emit("blink", name, box[0], box[1], box[2] - box[0], box[3] - box[1], s.layout["duration_seconds"], e["blink_duration"],
           *e["blink_start_seconds"])
    s.finish()


def old_battlefield(src):
    s = Scene(src, 9, 1.2, (440, 650))
    flag = clean_alpha(Image.open(s.src / "tattered_banner.png"), 160, 90)
    # 낡은 깃발 2개 (원본 재생 코드의 위치 · 크기). flip이면 깃대가 오른쪽
    for px, py, w, h, flip, phase in [(53, 130, 154, 96, False, .2), (1230, 135, 119, 78, True, 1.6)]:
        sp = flag.resize((w * 2, h * 2), Image.LANCZOS)
        if flip:
            sp = sp.transpose(Image.FLIP_LEFT_RIGHT)
        name = s.save(sp, f"banner_{'r' if flip else 'l'}")
        left = px - w if flip else px
        s.emit("flag", name, left, py, w, h, phase / (2 * 3.14159), 1 if flip else 0)
    # 해골 손: 3초에 땅(y=401)에서 솟아올라 머물다 8.7초에 사라짐
    hand = clean_alpha(Image.open(s.src / "skeletal_hand.png"), 160, 90)
    hh = s.layout["hand"]
    hand = hand.resize((round(hh["height_px"] * 2 * hand.width / hand.height), hh["height_px"] * 2), Image.LANCZOS)
    name = s.save(hand, "skeletal_hand")
    s.emit("hand", name, hh["anchor"][0], hh["anchor"][1], hand.width / 2, hh["height_px"], s.layout["duration_seconds"],
           hh["start_seconds"], hh["full_height_seconds"], hh["retreat_start_seconds"], hh["hidden_seconds"])
    s.emit("dust", 45)
    s.finish()


def demon_castle(src):
    s = Scene(src, 10, 1.0, (500, 700))
    banner = clean_alpha(Image.open(s.src / "hanging_banner.png"), 150, 100)
    w, h = s.layout["banner_size"]
    name = s.save(banner.resize((w * 3, h * 3), Image.LANCZOS), "hanging_banner")
    for (px, py), phase in zip(s.layout["banner_anchors"], (0.0, 1.25)):
        s.emit("banner", name, px, py, w, h, phase)
    # 푸른 화로 불꽃 (불꽃 4장 섞기) + 푸른 불티와 불빛
    for i in range(1, 5):
        s.save(Image.open(s.src / "flames" / f"flame_{i}.png").convert("RGBA"), f"flame_{i}")
    for x, y, phase in s.layout["braziers"]:
        s.emit("flame", "flame_", 4, x, y, 42, 55, phase, 0.51, 0.83, 1.0)
    # 바닥 안개: 가로로 이어지는 그림이라 옆으로 흘려 보내며 반복
    mist = Image.open(s.src / "mist_overlay.png").convert("RGBA")
    s.save(mist, "mist")
    s.emit("mist", "mist", 0, 400, 1280, 224, 1.0)
    s.finish()


def main():
    land_of_grace(sys.argv[1])
    dragon_cave(sys.argv[2])
    crimson_jungle(sys.argv[3])
    old_battlefield(sys.argv[4])
    demon_castle(sys.argv[5])


if __name__ == "__main__":
    main()
