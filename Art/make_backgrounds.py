"""
게임 배경 그림을 만드는 스크립트입니다. (Claude가 사용)
진짜 일러스트 배경이 생기기 전까지 쓰는, 코드로 그린 배경이에요.
출력: Assets/Resources/Backgrounds/*.jpg, Assets/Resources/UI/*.png

  title.jpg      타이틀 (노을 지는 왕국)
  camp.jpg       야영지 (밤 숲속 공터)
  menu.jpg       메뉴 화면들 공통 (어두운 양피지)
  battle_1~10    스테이지별 전투 배경
  UI/leather.png 판·버튼 질감 (이어 붙일 수 있는 무늬)
"""
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter

ROOT = Path(__file__).resolve().parent.parent
BG = ROOT / "Assets" / "Resources" / "Backgrounds"
UIDIR = ROOT / "Assets" / "Resources" / "UI"
W, H = 1280, 720
rng = np.random.default_rng(7)


# ---------------- 기본 도구 ----------------

def value_noise(w, h, cell, seed, wrap=False):
    r = np.random.default_rng(seed)
    gw, gh = w // cell + 2, h // cell + 2
    grid = r.random((gh, gw))
    if wrap:
        grid[:, -2:] = grid[:, :2]
        grid[-2:, :] = grid[:2, :]
    ys, xs = np.mgrid[0:h, 0:w]
    gx, gy = xs / cell, ys / cell
    x0, y0 = gx.astype(int), gy.astype(int)
    fx, fy = gx - x0, gy - y0
    fx = fx * fx * (3 - 2 * fx)
    fy = fy * fy * (3 - 2 * fy)
    a = grid[y0, x0]; b = grid[y0, x0 + 1]; c = grid[y0 + 1, x0]; d = grid[y0 + 1, x0 + 1]
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy


def fbm(w, h, base_cell, octaves, seed, wrap=False):
    total, amp, norm = np.zeros((h, w)), 1.0, 0.0
    cell = base_cell
    for o in range(octaves):
        total += value_noise(w, h, max(2, cell), seed + o * 31, wrap) * amp
        norm += amp
        amp *= 0.5
        cell = max(2, cell // 2)
    return total / norm


def ridge_line(w, base, amp, cell, seed, octaves=5):
    """산 능선 높이 (0~1, 위가 0)"""
    n = fbm(w, 1, cell, octaves, seed)[0]
    return base - (n - 0.5) * 2 * amp


def vertical_gradient(colors, stops, h=H, w=W):
    t = np.linspace(0, 1, h)[:, None]
    out = np.zeros((h, w, 3))
    for i in range(3):
        out[..., i] = np.interp(t, stops, [c[i] / 255.0 for c in colors])
    return out


def paint_layer(img, top_y, color, haze_color=None, haze=0.0, texture=0.08, seed=0, fade=0.0):
    """top_y(배열, 0~1) 아래를 color로 칠함. 위쪽 가장자리는 살짝 흐리게."""
    h, w = img.shape[:2]
    ys = np.arange(h)[:, None] / h
    edge = 1.5 / h
    mask = np.clip((ys - top_y[None, :]) / edge, 0, 1)
    col = np.array(color, dtype=float)
    tex = (fbm(w, h, 64, 4, seed) - 0.5) * texture
    layer = col[None, None, :] / 255.0 + tex[..., None]
    if haze_color is not None and haze > 0:
        # 아래로 갈수록 진해지는 색 (안개가 걷히는 느낌)
        depth = np.clip((ys - top_y[None, :]) * 3, 0, 1)[..., None]
        hc = np.array(haze_color) / 255.0
        layer = layer * (1 - haze * (1 - depth)) + hc * haze * (1 - depth)
    if fade > 0:
        layer = layer * (1 - fade) + img * fade
    img[:] = img * (1 - mask[..., None]) + layer * mask[..., None]


def glow(img, cx, cy, radius, color, strength):
    h, w = img.shape[:2]
    ys, xs = np.mgrid[0:h, 0:w]
    d = np.sqrt(((xs - cx * w) / radius) ** 2 + ((ys - cy * h) / radius) ** 2)
    g = np.clip(1 - d, 0, 1) ** 2 * strength
    img[:] = img + (np.array(color) / 255.0)[None, None, :] * g[..., None]


def fog_band(img, y, thickness, color, alpha, seed):
    h, w = img.shape[:2]
    ys = np.arange(h)[:, None] / h
    n = fbm(w, h, 160, 4, seed)
    band = np.exp(-((ys - y) / thickness) ** 2) * (0.6 + 0.8 * n)
    a = np.clip(band * alpha, 0, 1)[..., None]
    img[:] = img * (1 - a) + (np.array(color) / 255.0) * a


def finish(img, vignette=0.45, grain=0.035, warm=(1.0, 0.97, 0.9), blur=0):
    h, w = img.shape[:2]
    ys, xs = np.mgrid[0:h, 0:w]
    d = np.sqrt(((xs / w) - 0.5) ** 2 * 1.2 + ((ys / h) - 0.5) ** 2 * 1.6)
    img *= (1 - vignette * np.clip(d * 1.4 - 0.25, 0, 1) ** 1.5)[..., None]
    paper = fbm(w, h, 6, 3, 99) - 0.5
    img += paper[..., None] * grain
    img *= np.array(warm)[None, None, :]
    out = Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8))
    if blur:
        out = out.filter(ImageFilter.GaussianBlur(blur))
    return out


def trees(img, base_y, height, count, color, seed, jitter=0.02, pine=True):
    """숲 실루엣: base_y(0~1) 위로 뾰족한 나무들"""
    h, w = img.shape[:2]
    r = np.random.default_rng(seed)
    top = np.full(w, 2.0)
    xs = np.arange(w)
    for _ in range(count):
        cx = r.random() * w
        th = height * (0.6 + 0.6 * r.random())
        tw = th * h * (0.28 if pine else 0.6)
        if pine:
            prof = base_y - th + np.abs(xs - cx) / max(tw, 1) * th
        else:
            prof = base_y - th + ((xs - cx) / max(tw, 1)) ** 2 * th
        top = np.minimum(top, prof)
    top = np.minimum(top, base_y + (r.random(w) - 0.5) * jitter * 0.1)
    paint_layer(img, np.clip(top, 0, 1), color, texture=0.05, seed=seed)


def castle(img, cx, base_y, scale, color):
    h, w = img.shape[:2]
    top = np.full(w, 2.0)
    def rect(x0, x1, y):
        a, b = int((cx + x0 * scale) * w), int((cx + x1 * scale) * w)
        top[max(a, 0):max(b, 0)] = np.minimum(top[max(a, 0):max(b, 0)], y)
    rect(-0.10, 0.10, base_y - 0.10 * scale * 2)
    rect(-0.13, -0.08, base_y - 0.17 * scale * 2)
    rect(0.08, 0.13, base_y - 0.17 * scale * 2)
    rect(-0.03, 0.03, base_y - 0.26 * scale * 2)
    for i in range(-6, 7):  # 성벽 톱니
        if i % 2 == 0:
            rect(i * 0.016, i * 0.016 + 0.01, base_y - 0.115 * scale * 2)
    paint_layer(img, np.clip(top, 0, 1), color, texture=0.03, seed=5)


def save_jpg(img: Image.Image, name):
    BG.mkdir(parents=True, exist_ok=True)
    img.save(BG / name, quality=88)
    print("saved", name)


# ---------------- 타이틀: 노을 지는 왕국 ----------------

def make_title():
    img = vertical_gradient(
        [(28, 30, 58), (92, 46, 62), (196, 104, 70), (240, 178, 110), (250, 214, 150)],
        [0.0, 0.32, 0.55, 0.68, 0.74])
    glow(img, 0.72, 0.62, 520, (255, 190, 110), 0.55)
    glow(img, 0.72, 0.64, 140, (255, 235, 180), 0.8)
    clouds = fbm(W, H, 220, 5, 3)
    ys = np.arange(H)[:, None] / H
    cmask = np.clip((clouds - 0.52) * 4, 0, 1) * np.exp(-((ys - 0.3) / 0.16) ** 2)
    img[:] = img * (1 - cmask[..., None] * 0.6) + np.array([1.0, 0.72, 0.55]) * cmask[..., None] * 0.6
    paint_layer(img, ridge_line(W, 0.62, 0.10, 380, 11), (120, 86, 104), (230, 160, 120), 0.6, seed=1)
    fog_band(img, 0.66, 0.05, (240, 190, 150), 0.5, 12)
    castle(img, 0.55, 0.70, 0.35, (70, 50, 66))
    paint_layer(img, ridge_line(W, 0.72, 0.06, 260, 21), (70, 52, 62), (200, 130, 110), 0.35, seed=2)
    fog_band(img, 0.76, 0.04, (220, 150, 120), 0.35, 13)
    trees(img, 0.84, 0.12, 70, (36, 30, 34), 31)
    paint_layer(img, ridge_line(W, 0.86, 0.025, 200, 41), (30, 24, 26), seed=3)
    save_jpg(finish(img, vignette=0.5), "title.jpg")


# ---------------- 야영지: 밤 숲속 공터 ----------------

def make_camp():
    img = vertical_gradient([(10, 14, 34), (24, 30, 58), (54, 50, 78)], [0.0, 0.35, 0.5])
    r = np.random.default_rng(5)
    for _ in range(260):  # 별
        x, y = r.random() * W, r.random() * H * 0.45
        s = r.random()
        glow(img, x / W, y / H, 2 + s * 3, (255, 245, 220), 0.6 + s)
    glow(img, 0.82, 0.14, 60, (255, 245, 210), 1.2)   # 달
    glow(img, 0.82, 0.14, 260, (150, 160, 210), 0.25)
    paint_layer(img, ridge_line(W, 0.46, 0.06, 300, 61), (40, 44, 70), (60, 60, 90), 0.4, seed=6)
    trees(img, 0.55, 0.16, 90, (22, 30, 38), 62)
    fog_band(img, 0.56, 0.04, (70, 80, 110), 0.35, 63)
    trees(img, 0.62, 0.22, 60, (14, 20, 22), 64)
    # 공터 (땅) - 가운데로 갈수록 모닥불 빛으로 따뜻하게
    ground = np.full(W, 0.6) + (fbm(W, 1, 300, 3, 65)[0] - 0.5) * 0.02
    paint_layer(img, ground, (44, 40, 30), texture=0.12, seed=66)
    glow(img, 0.5, 0.75, 620, (255, 140, 60), 0.35)
    save_jpg(finish(img, vignette=0.55, warm=(1.0, 0.95, 0.9)), "camp.jpg")


# ---------------- 메뉴 공통: 어두운 양피지 ----------------

def make_menu():
    base = np.array([62, 44, 32]) / 255.0
    n = fbm(W, H, 180, 6, 71)
    stains = fbm(W, H, 90, 4, 72)
    img = base[None, None, :] * (0.75 + 0.5 * n[..., None]) + (stains[..., None] - 0.5) * 0.08
    glow(img, 0.5, 0.45, 760, (120, 80, 50), 0.25)
    save_jpg(finish(img, vignette=0.65, grain=0.05), "menu.jpg")


# ---------------- 전투 배경 (스테이지 10개) ----------------
# 화면 위쪽 27% 근처가 지평선, 아래는 유닛이 서는 땅

HORIZON = 0.27

STAGES = [
    # 하늘 위/아래, 먼 산, 가까운 언덕, 땅, 특징
    dict(sky=((120, 170, 170), (210, 225, 200)), far=(110, 140, 130), near=(52, 92, 60), ground=(70, 108, 58), feature="forest"),
    dict(sky=((120, 170, 225), (230, 230, 205)), far=(150, 170, 180), near=(120, 150, 80), ground=(140, 160, 80), feature="plains"),
    dict(sky=((130, 145, 175), (215, 215, 220)), far=(120, 125, 145), near=(100, 98, 100), ground=(118, 110, 100), feature="mountain"),
    dict(sky=((110, 175, 220), (235, 225, 200)), far=(70, 140, 180), near=(200, 180, 130), ground=(222, 200, 150), feature="coast"),
    dict(sky=((150, 160, 185), (225, 220, 210)), far=(130, 135, 150), near=(110, 105, 100), ground=(140, 130, 112), feature="checkpoint"),
    dict(sky=((230, 200, 140), (255, 240, 200)), far=(200, 180, 120), near=(170, 170, 80), ground=(185, 175, 95), feature="holy"),
    dict(sky=((40, 28, 18), (110, 70, 30)), far=(90, 60, 30), near=(70, 50, 28), ground=(96, 72, 40), feature="cave"),
    dict(sky=((150, 70, 50), (230, 150, 90)), far=(110, 60, 50), near=(70, 70, 30), ground=(90, 82, 40), feature="jungle"),
    dict(sky=((110, 100, 100), (190, 170, 150)), far=(110, 95, 90), near=(90, 75, 65), ground=(105, 88, 72), feature="battlefield"),
    dict(sky=((30, 8, 16), (120, 30, 30)), far=(60, 20, 30), near=(40, 14, 20), ground=(58, 30, 34), feature="demon"),
]


def make_battle(i, st):
    img = vertical_gradient([st["sky"][0], st["sky"][1], st["sky"][1]], [0.0, HORIZON, 1.0])
    f = st["feature"]
    seed = 100 + i * 10

    if f in ("plains", "coast", "holy", "jungle", "demon"):
        glow(img, 0.7, 0.12, 300, (255, 230, 180) if f != "demon" else (255, 80, 40), 0.35)

    # 먼 산 / 바다 / 동굴 천장
    if f == "coast":
        sea = np.full(W, HORIZON - 0.04)
        paint_layer(img, sea, st["far"], texture=0.06, seed=seed)
        for k in range(6):  # 물결 반짝임
            fog_band(img, HORIZON - 0.03 + k * 0.006, 0.002, (230, 245, 255), 0.25, seed + k)
    elif f == "cave":
        ceiling = 0.12 + (fbm(W, 1, 60, 4, seed)[0] - 0.5) * 0.18
        img[:] = img
        top = np.clip(ceiling, 0, 1)
        ys = np.arange(H)[:, None] / H
        mask = (ys < top[None, :]).astype(float)
        img[:] = img * (1 - mask[..., None]) + (np.array((30, 20, 12)) / 255.0) * mask[..., None]
        paint_layer(img, ridge_line(W, HORIZON - 0.02, 0.05, 120, seed + 1), st["far"], texture=0.1, seed=seed + 1)
        glow(img, 0.5, 0.3, 500, (255, 190, 60), 0.3)  # 용의 보물 빛
    else:
        amp = 0.14 if f == "mountain" else 0.07
        paint_layer(img, ridge_line(W, HORIZON - 0.03, amp, 300, seed), st["far"], st["sky"][1], 0.55, seed=seed)
        fog_band(img, HORIZON - 0.02, 0.025, st["sky"][1], 0.45, seed + 2)

    if f == "demon":
        castle(img, 0.62, HORIZON - 0.01, 0.55, (20, 6, 10))
        glow(img, 0.62, HORIZON, 260, (255, 60, 20), 0.35)
    if f == "checkpoint":
        castle(img, 0.35, HORIZON + 0.005, 0.25, (95, 92, 100))
    if f == "holy":
        castle(img, 0.25, HORIZON, 0.2, (230, 220, 190))

    # 가까운 언덕 / 숲
    if f in ("forest", "jungle"):
        trees(img, HORIZON + 0.02, 0.16, 90, st["near"], seed + 3, pine=(f == "forest"))
    else:
        paint_layer(img, ridge_line(W, HORIZON + 0.01, 0.025, 200, seed + 3), st["near"], st["sky"][1], 0.3, seed=seed + 3)

    # 땅: 아래로 갈수록 진하고 가까운 느낌, 풀/모래 질감
    ground_top = np.full(W, HORIZON + 0.03)
    g = np.array(st["ground"], dtype=float)
    paint_layer(img, ground_top, g, texture=0.09, seed=seed + 4)
    ys = np.arange(H)[:, None] / H
    shade = np.clip((ys - HORIZON) / (1 - HORIZON), 0, 1)
    img[:] = img * (1 - 0.25 * shade[..., None])
    # 땅 위의 얼룩 (풀, 돌, 그림자)
    blots = fbm(W, H, 40, 4, seed + 5)
    bm = np.clip((blots - 0.62) * 4, 0, 1) * (ys > HORIZON + 0.04)
    img[:] = img * (1 - 0.08 * bm[..., None])
    if f == "battlefield":
        r = np.random.default_rng(seed)
        for _ in range(40):  # 꽂힌 검과 창
            x = int(r.random() * W); y0 = int((HORIZON + 0.06 + r.random() * 0.6) * H)
            hh = int(18 + r.random() * 30)
            img[y0 - hh:y0, x:x + 3] = np.array((60, 55, 55)) / 255.0
            img[y0 - hh + 6:y0 - hh + 9, x - 5:x + 8] = np.array((60, 55, 55)) / 255.0
    if f == "demon":
        cracks = np.clip((fbm(W, H, 30, 3, seed + 7) - 0.66) * 6, 0, 1) * (ys > HORIZON + 0.05)
        img[:] = img + np.array((0.9, 0.25, 0.05))[None, None, :] * cracks[..., None] * 0.3
    fog_band(img, HORIZON + 0.035, 0.02, st["sky"][1], 0.35, seed + 6)
    save_jpg(finish(img, vignette=0.35, grain=0.03), f"battle_{i + 1}.jpg")


# ---------------- UI 질감: 이어 붙일 수 있는 가죽 무늬 ----------------

def make_leather():
    UIDIR.mkdir(parents=True, exist_ok=True)
    s = 256
    n = fbm(s, s, 32, 5, 81, wrap=True)
    pores = fbm(s, s, 4, 2, 82, wrap=True)
    v = 0.5 + (n - 0.5) * 0.9 + (pores - 0.5) * 0.35
    v = np.clip(v, 0, 1)
    img = Image.fromarray((v * 255).astype(np.uint8), "L").convert("RGBA")
    img.putalpha(255)
    img.save(UIDIR / "leather.png")
    print("saved UI/leather.png")


if __name__ == "__main__":
    make_title()
    make_camp()
    make_menu()
    for i, st in enumerate(STAGES):
        make_battle(i, st)
    make_leather()
