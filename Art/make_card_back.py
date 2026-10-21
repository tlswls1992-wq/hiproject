"""
보상 카드 뒷면 그림을 만드는 스크립트입니다. (Claude가 사용)
타이틀 화면의 카드처럼 짙은 남색 바탕 + 금색 테두리 + 가운데 나침반 별.
출력: Assets/Resources/UI/card_back.png
"""
import math
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parent.parent
W, H, SS = 360, 520, 3          # 최종 크기, 매끄럽게 그리려고 3배로 그린 뒤 줄임
w, h = W * SS, H * SS


def gold(t):
    """0~1 → 어두운 금 ~ 밝은 금"""
    a, b = np.array([120, 82, 34]), np.array([248, 214, 140])
    return tuple(int(v) for v in a + (b - a) * t)


def main():
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    # 바탕: 위는 조금 밝은 남색, 아래는 짙은 남색 + 가운데 은은한 빛
    t = yy / h
    base = np.stack([27 - 14 * t, 37 - 18 * t, 82 - 34 * t], -1)
    d = np.sqrt(((xx - w / 2) / w) ** 2 + ((yy - h * 0.47) / h) ** 2)
    base += (np.clip(0.42 - d, 0, 1) * 70)[..., None] * np.array([0.35, 0.45, 1.0])
    # 잔잔한 마름모 무늬
    lattice = (np.abs(np.sin((xx + yy) * math.pi / (26 * SS))) < 0.06) | (np.abs(np.sin((xx - yy) * math.pi / (26 * SS))) < 0.06)
    base[lattice] += np.array([10, 13, 26])
    img = Image.fromarray(np.clip(base, 0, 255).astype(np.uint8)).convert("RGBA")
    dr = ImageDraw.Draw(img)

    def rr(box, radius, outline, width):
        dr.rounded_rectangle(box, radius * SS, outline=outline, width=width * SS)

    # 바깥 금테 (3겹으로 입체감)
    rr((0, 0, w - 1, h - 1), 26, gold(0.35), 10)
    rr((3 * SS, 3 * SS, w - 3 * SS, h - 3 * SS), 24, gold(0.9), 3)
    rr((9 * SS, 9 * SS, w - 9 * SS, h - 9 * SS), 20, gold(0.55), 2)
    # 안쪽 가는 금선
    rr((22 * SS, 22 * SS, w - 22 * SS, h - 22 * SS), 14, gold(0.75), 2)

    # 네 귀퉁이 장식: 사분원 + 작은 마름모
    for cx, cy, sx, sy in ((22, 22, 1, 1), (W - 22, 22, -1, 1), (22, H - 22, 1, -1), (W - 22, H - 22, -1, -1)):
        for r, col in ((34, gold(0.8)), (24, gold(0.5))):
            box = [(cx - r) * SS, (cy - r) * SS, (cx + r) * SS, (cy + r) * SS]
            start = {(1, 1): 0, (-1, 1): 90, (-1, -1): 180, (1, -1): 270}[(sx, sy)]
            dr.arc(box, start, start + 90, fill=col, width=2 * SS)
        px, py = cx + sx * 16, cy + sy * 16
        s = 7
        dr.polygon([(px * SS, (py - s) * SS), ((px + s) * SS, py * SS), (px * SS, (py + s) * SS), ((px - s) * SS, py * SS)], fill=gold(0.95))

    # 위 · 아래 가운데 작은 장식
    for py in (40, H - 40):
        dr.line([((W / 2 - 60) * SS, py * SS), ((W / 2 + 60) * SS, py * SS)], fill=gold(0.6), width=2 * SS)
        s = 8
        dr.polygon([(W / 2 * SS, (py - s) * SS), ((W / 2 + s) * SS, py * SS), (W / 2 * SS, (py + s) * SS), ((W / 2 - s) * SS, py * SS)], fill=gold(0.95))

    # 가운데: 이중 원 + 둘레 점 + 8갈래 나침반 별
    cx, cy = W / 2, H * 0.47
    for r, col, wd in ((118, gold(0.85), 3), (106, gold(0.5), 2), (70, gold(0.65), 2)):
        dr.ellipse([(cx - r) * SS, (cy - r) * SS, (cx + r) * SS, (cy + r) * SS], outline=col, width=wd * SS)
    for i in range(24):
        a = i / 24 * 2 * math.pi
        r = 112
        x, y = cx + math.cos(a) * r, cy + math.sin(a) * r
        rad = 3.2 if i % 3 == 0 else 1.8
        dr.ellipse([(x - rad) * SS, (y - rad) * SS, (x + rad) * SS, (y + rad) * SS], fill=gold(0.95))
    # 나침반 별: 긴 4갈래 + 짧은 4갈래, 각 갈래를 밝은 면 · 어두운 면으로 나눠 입체감
    for i in range(8):
        a = i / 8 * 2 * math.pi - math.pi / 2
        length = 100 if i % 2 == 0 else 58
        half = 15 if i % 2 == 0 else 11
        tip = (cx + math.cos(a) * length, cy + math.sin(a) * length)
        left = (cx + math.cos(a - math.pi / 2) * half, cy + math.sin(a - math.pi / 2) * half)
        right = (cx + math.cos(a + math.pi / 2) * half, cy + math.sin(a + math.pi / 2) * half)
        S = lambda p: (p[0] * SS, p[1] * SS)
        dr.polygon([S((cx, cy)), S(left), S(tip)], fill=gold(1.0 if i % 2 == 0 else 0.8))
        dr.polygon([S((cx, cy)), S(right), S(tip)], fill=gold(0.55 if i % 2 == 0 else 0.45))
    dr.ellipse([(cx - 9) * SS, (cy - 9) * SS, (cx + 9) * SS, (cy + 9) * SS], fill=gold(1.0))

    # 둥근 모서리 밖은 투명하게
    mask = Image.new("L", (w, h), 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, w - 1, h - 1), 26 * SS, fill=255)
    img.putalpha(mask)
    img = img.resize((W, H), Image.LANCZOS)
    out = ROOT / "Assets" / "Resources" / "UI" / "card_back.png"
    img.save(out, optimize=True)
    print("saved", out)


if __name__ == "__main__":
    main()
