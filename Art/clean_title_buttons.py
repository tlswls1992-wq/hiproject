"""
타이틀 그림의 메뉴 버튼 안에 그려진 흐린 글자를 지우는 스크립트입니다. (Claude가 사용)
(글자는 게임이 선명한 글꼴로 다시 그려요: TitleScene.DrawButtonLabel)

입력: Art/originals/title/background_static.png (글자가 그려진 원본)
출력: Assets/Resources/Title/background.jpg   글자를 지운 배경
      Assets/Resources/Title/button_<번호>.png 마우스를 올렸을 때 덮는 밝은 버튼 (글자 없음)

방법: 버튼 가운데의 밝은 글자 픽셀을 찾아, 주변 판자색으로 메우고(번짐 채우기)
      옆 부분의 나무결 무늬를 살짝 얹어 자연스럽게 만들어요.
"""
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "Art" / "originals" / "title" / "background_static.png"
OUT = ROOT / "Assets" / "Resources" / "Title"
BUTTONS = [(500, 287, 285, 58), (500, 352, 285, 58), (500, 417, 285, 58), (500, 482, 285, 58), (500, 547, 285, 60)]


def blur(a, r):
    img = Image.fromarray(np.clip(a, 0, 255).astype(np.uint8))
    return np.asarray(img.filter(ImageFilter.GaussianBlur(r))).astype(float)


def clean(img):
    lum = img.mean(2)
    for x, y, w, h in BUTTONS:
        # 글자가 있는 가운데 칸 (테두리·덩굴은 건드리지 않음)
        x0, x1, y0, y1 = x + int(w * 0.27), x + int(w * 0.73), y + 12, y + h - 11
        box = lum[y0:y1, x0:x1]
        med = np.median(box)
        m = (box > med + 32).astype(np.uint8) * 255
        m = np.asarray(Image.fromarray(m).filter(ImageFilter.MaxFilter(5))) > 0   # 글자 가장자리 번짐까지
        mask = np.zeros(lum.shape, bool)
        mask[y0:y1, x0:x1] = m

        # 1) 번짐 채우기: 글자 자리를 주변 색의 평균으로 여러 번 메움
        ys, xs = slice(y0 - 4, y1 + 4), slice(x0 - 6, x1 + 6)
        region = img[ys, xs].copy()
        rm = mask[ys, xs]
        known = ~rm
        fill = region.copy()
        fill[rm] = region[known].mean(0)
        for _ in range(300):
            avg = (np.roll(fill, 1, 0) + np.roll(fill, -1, 0) + np.roll(fill, 1, 1) + np.roll(fill, -1, 1)) / 4
            fill[rm] = avg[rm]

        # 2) 나무결 무늬: 글자 칸 바로 왼쪽·오른쪽 판자의 잔무늬를 가져와 얹음
        texture = np.zeros_like(region)
        tw = x1 - x0
        left = img[ys, x0 - 6 - tw // 2: x0 - 6]
        right = img[ys, x1 + 6: x1 + 6 + tw // 2]
        strip = np.concatenate([left, right], 1)
        strip = strip - blur(strip, 3)
        reps = int(np.ceil(region.shape[1] / strip.shape[1]))
        texture = np.tile(strip, (1, reps, 1))[:, :region.shape[1]]
        fill[rm] += texture[rm] * 0.8

        soft = blur(rm.astype(float)[..., None].repeat(3, 2) * 255, 1.2) / 255
        img[ys, xs] = region * (1 - soft) + fill * soft
    return img


def main():
    img = np.asarray(Image.open(SRC).convert("RGB")).astype(float)
    img = clean(img)
    Image.fromarray(np.clip(img, 0, 255).astype(np.uint8)).save(OUT / "background.jpg", quality=92)
    # 밝은 버튼 (원래 밝은 버튼과 같은 색 보정)
    gain, offset = np.array([0.955, 1.16, 1.217]), np.array([50.7, 28.5, 22.7])
    for i, (x, y, w, h) in enumerate(BUTTONS):
        crop = img[y:y + h, x:x + w] * gain + offset
        rgba = np.dstack([np.clip(crop, 0, 255), np.full((h, w), 255.0)]).astype(np.uint8)
        Image.fromarray(rgba, "RGBA").save(OUT / f"button_{i}.png", optimize=True)
    print("saved background.jpg and", len(BUTTONS), "buttons")


if __name__ == "__main__":
    main()
