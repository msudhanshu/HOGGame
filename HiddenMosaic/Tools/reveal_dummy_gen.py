#!/usr/bin/env python3
"""
Generates placeholder day/night reveal sets for HiddenMosaic until real themed art arrives.

Each set is written to Assets/Game/Resources/HiddenMosaic/Reveal/<set>/:
  day.png      - the "real" image; hidden objects are painted into it
  night.png    - the cover image (darker / disguised), WITHOUT the hidden objects
  objects.json - pixel rects (top-left origin) of every painted object, auto-recorded

Usage:  python3 Tools/reveal_dummy_gen.py            (from HiddenMosaic/)
Real art: drop your own day.png / night.png in a new folder and author objects.json with
Nixin Studio > Hidden Mosaic > Reveal Set Editor.
"""
import json
import math
import os
import random

from PIL import Image, ImageDraw, ImageFilter

W, H = 2048, 1536
ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "Game", "Resources", "HiddenMosaic", "Reveal")


# ---------------------------------------------------------------- helpers

def gradient(top, bottom, w=W, h=H):
    img = Image.new("RGB", (w, h))
    px = img.load()
    for y in range(h):
        t = y / (h - 1)
        c = tuple(int(top[i] + (bottom[i] - top[i]) * t) for i in range(3))
        for x in range(w):
            px[x, y] = c
    return img


def vignette(img, strength=0.6):
    w, h = img.size
    mask = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(mask)
    d.ellipse([-w * 0.15, -h * 0.15, w * 1.15, h * 1.15], fill=255)
    mask = mask.filter(ImageFilter.GaussianBlur(220))
    dark = Image.new("RGB", (w, h), (0, 0, 0))
    return Image.composite(img, Image.blend(img, dark, strength), mask)


def tint(img, rgb, amount):
    return Image.blend(img, Image.new("RGB", img.size, rgb), amount)


def darken(img, factor):
    return img.point(lambda v: int(v * factor))


class Painter:
    """Draws objects and records their bounding rects."""

    def __init__(self, img):
        self.img = img
        self.d = ImageDraw.Draw(img)
        self.objects = []

    def record(self, name, box, pad=6):
        x0, y0, x1, y1 = box
        x0, y0 = max(0, int(x0) - pad), max(0, int(y0) - pad)
        x1, y1 = min(W, int(x1) + pad), min(H, int(y1) + pad)
        self.objects.append({"name": name, "x": x0, "y": y0, "w": x1 - x0, "h": y1 - y0})


# ---------------------------------------------------------------- scenery (shared by day + night)

def park_scenery(img, rng):
    d = ImageDraw.Draw(img)
    for i, (cx, r, col) in enumerate([(300, 520, (96, 160, 84)), (1100, 640, (84, 150, 76)), (1800, 560, (100, 168, 90))]):
        d.ellipse([cx - r * 1.4, 980 - r * 0.5, cx + r * 1.4, 980 + r], fill=col)
    d.rectangle([0, 1080, W, H], fill=(90, 150, 70))
    d.rectangle([0, 1250, W, 1330], fill=(150, 140, 120))  # path
    for _ in range(140):  # grass tufts / clutter so objects don't pop out trivially
        x, y = rng.randint(0, W), rng.randint(1100, H)
        d.line([x, y, x + rng.randint(-8, 8), y - rng.randint(10, 30)], fill=(60, 120, 50), width=3)
    for x in range(80, W, 260):  # distant bushes
        d.ellipse([x, 1010, x + 170, 1110], fill=(70, 128, 62))


def city_scenery(img, rng, palette):
    d = ImageDraw.Draw(img)
    x = 0
    while x < W:
        bw, bh = rng.randint(120, 260), rng.randint(380, 900)
        d.rectangle([x, H - 260 - bh, x + bw, H - 260], fill=palette["building"])
        for wy in range(H - 240 - bh, H - 290, 60):
            for wx in range(x + 18, x + bw - 30, 46):
                if rng.random() < 0.55:
                    d.rectangle([wx, wy, wx + 22, wy + 30], fill=palette["window"])
        x += bw + rng.randint(8, 30)
    d.rectangle([0, H - 260, W, H], fill=palette["street"])
    for lx in range(60, W, 230):
        d.rectangle([lx, H - 230, lx + 120, H - 222], fill=palette["lane"])


# ---------------------------------------------------------------- set: day/night park

def paint_park_objects(p):
    d = p.d
    d.ellipse([1700, 90, 1880, 270], fill=(255, 214, 60)); p.record("sun", (1700, 90, 1880, 270))
    # house
    d.rectangle([180, 860, 420, 1060], fill=(200, 90, 70)); d.polygon([(160, 860), (300, 740), (440, 860)], fill=(120, 60, 50))
    d.rectangle([270, 960, 330, 1060], fill=(90, 60, 40)); p.record("house", (160, 740, 440, 1060))
    # tree
    d.rectangle([640, 880, 680, 1070], fill=(110, 75, 40)); d.ellipse([560, 720, 760, 920], fill=(40, 120, 50)); p.record("tree", (560, 720, 760, 1070))
    # car
    d.rounded_rectangle([960, 1180, 1200, 1250], 20, fill=(40, 90, 200)); d.rounded_rectangle([1010, 1130, 1150, 1190], 16, fill=(70, 130, 230))
    d.ellipse([985, 1230, 1035, 1280], fill=(30, 30, 30)); d.ellipse([1125, 1230, 1175, 1280], fill=(30, 30, 30)); p.record("car", (960, 1130, 1200, 1280))
    # kite
    d.polygon([(1300, 220), (1360, 300), (1300, 400), (1240, 300)], fill=(230, 60, 140)); d.line([1300, 400, 1340, 520], fill=(60, 60, 60), width=3); p.record("kite", (1240, 220, 1360, 520))
    # lantern
    d.rectangle([1500, 900, 1514, 1240], fill=(60, 60, 60)); d.rectangle([1478, 860, 1536, 930], fill=(250, 220, 120)); p.record("lantern", (1478, 860, 1536, 1240))
    # cat
    d.ellipse([860, 1360, 960, 1430], fill=(240, 140, 40)); d.ellipse([930, 1320, 990, 1380], fill=(240, 140, 40))
    d.polygon([(938, 1330), (946, 1300), (956, 1328)], fill=(240, 140, 40)); d.polygon([(966, 1328), (976, 1300), (984, 1330)], fill=(240, 140, 40)); p.record("cat", (860, 1300, 990, 1430))
    # bench
    d.rectangle([1600, 1180, 1820, 1200], fill=(140, 90, 50)); d.rectangle([1600, 1140, 1820, 1158], fill=(140, 90, 50))
    d.rectangle([1615, 1200, 1630, 1250], fill=(70, 70, 70)); d.rectangle([1790, 1200, 1805, 1250], fill=(70, 70, 70)); p.record("bench", (1600, 1140, 1820, 1250))
    # balloon
    d.ellipse([480, 260, 580, 380], fill=(230, 40, 40)); d.line([530, 380, 520, 520], fill=(80, 80, 80), width=2); p.record("balloon", (480, 260, 580, 520))
    # mailbox
    d.rectangle([1370, 1110, 1384, 1240], fill=(80, 60, 40)); d.rounded_rectangle([1335, 1060, 1420, 1120], 18, fill=(30, 80, 160)); p.record("mailbox", (1335, 1060, 1420, 1240))
    # bicycle
    d.ellipse([200, 1340, 300, 1440], outline=(30, 30, 30), width=8); d.ellipse([340, 1340, 440, 1440], outline=(30, 30, 30), width=8)
    d.line([250, 1390, 320, 1330, 390, 1390, 250, 1390], fill=(200, 40, 40), width=8); p.record("bicycle", (200, 1320, 440, 1440))
    # flower pot
    d.polygon([(1960, 1400), (2010, 1400), (2000, 1470), (1970, 1470)], fill=(170, 90, 50))
    for i, col in enumerate([(240, 60, 90), (250, 200, 40), (160, 80, 220)]):
        d.ellipse([1955 + i * 22, 1350, 1985 + i * 22, 1380], fill=col)
    p.record("flower_pot", (1950, 1345, 2015, 1470))


def make_park(rng):
    base = gradient((120, 190, 245), (210, 235, 250))
    park_scenery(base, rng)
    day = base.copy()
    p = Painter(day)
    paint_park_objects(p)

    night = gradient((8, 12, 40), (30, 40, 80))
    park_scenery(night, random.Random(7))  # same scenery, same seed path -> aligned with day
    night = tint(darken(night, 0.42), (20, 30, 90), 0.35)
    dn = ImageDraw.Draw(night)
    for _ in range(260):
        x, y = rng.randint(0, W), rng.randint(0, 700)
        r = rng.choice([1, 1, 2, 3])
        dn.ellipse([x - r, y - r, x + r, y + r], fill=(230, 235, 255))
    dn.ellipse([300, 120, 420, 240], fill=(235, 235, 210))
    dn.ellipse([330, 110, 450, 230], fill=(12, 18, 50))  # crescent
    night = vignette(night, 0.55)
    return day, night, p.objects, "#0b1020"


# ---------------------------------------------------------------- set: alien city (MIB)

def paint_alien_objects(p, rng):
    d = p.d
    d.ellipse([820, 120, 1220, 220], fill=(160, 255, 200)); d.ellipse([940, 70, 1100, 160], fill=(120, 220, 255)); p.record("ufo", (820, 70, 1220, 220))
    # alien
    d.ellipse([380, 1080, 470, 1190], fill=(80, 230, 90)); d.ellipse([395, 1110, 425, 1140], fill=(10, 10, 10)); d.ellipse([425, 1110, 455, 1140], fill=(10, 10, 10))
    d.rectangle([405, 1190, 445, 1270], fill=(80, 230, 90)); p.record("alien", (380, 1080, 470, 1270))
    # eye monster
    d.ellipse([1500, 820, 1640, 960], fill=(250, 250, 250)); d.ellipse([1545, 865, 1595, 915], fill=(200, 30, 200)); p.record("alien_eye", (1500, 820, 1640, 960))
    # tentacle plant
    for i in range(5):
        x = 1720 + i * 30
        d.line([x, 1290, x + rng.randint(-40, 40), 1150], fill=(200, 60, 255), width=12)
    p.record("tentacle_plant", (1680, 1140, 1880, 1290))
    # ray gun
    d.rectangle([700, 1360, 820, 1395], fill=(220, 220, 240)); d.rectangle([700, 1395, 730, 1450], fill=(160, 160, 190)); d.ellipse([815, 1355, 860, 1400], fill=(255, 80, 80)); p.record("ray_gun", (700, 1355, 860, 1450))
    # crystal
    d.polygon([(1250, 1290), (1290, 1150), (1330, 1290)], fill=(120, 255, 255)); d.polygon([(1290, 1290), (1330, 1190), (1370, 1290)], fill=(80, 200, 255)); p.record("crystal", (1250, 1150, 1370, 1290))
    # ringed planet
    d.ellipse([1780, 140, 1920, 280], fill=(255, 150, 60)); d.ellipse([1730, 195, 1970, 225], outline=(255, 230, 160), width=6); p.record("ringed_planet", (1730, 140, 1970, 280))
    # robot
    d.rectangle([140, 640, 240, 760], fill=(170, 170, 190)); d.rectangle([160, 600, 220, 640], fill=(170, 170, 190)); d.ellipse([172, 612, 188, 628], fill=(255, 60, 60)); d.ellipse([192, 612, 208, 628], fill=(255, 60, 60))
    p.record("robot", (140, 600, 240, 760))
    # antenna tower
    d.line([1010, 900, 1010, 640], fill=(220, 220, 255), width=10); d.ellipse([985, 610, 1035, 660], fill=(255, 60, 220)); p.record("antenna", (985, 610, 1035, 900))
    # slime blob
    d.ellipse([560, 1210, 700, 1290], fill=(120, 255, 60)); d.ellipse([590, 1180, 650, 1230], fill=(120, 255, 60)); p.record("slime", (560, 1180, 700, 1290))
    # portal
    d.ellipse([1380, 400, 1560, 580], outline=(80, 255, 220), width=18); d.ellipse([1420, 440, 1520, 540], fill=(40, 10, 80)); p.record("portal", (1380, 400, 1560, 580))
    # tiny saucer
    d.ellipse([300, 240, 420, 290], fill=(200, 200, 230)); d.ellipse([335, 215, 385, 255], fill=(120, 255, 180)); p.record("mini_saucer", (300, 215, 420, 290))


def make_alien(rng):
    seed = 11
    day = gradient((60, 10, 90), (10, 120, 110))
    city_scenery(day, random.Random(seed), {"building": (40, 20, 70), "window": (120, 255, 160), "street": (30, 10, 50), "lane": (255, 80, 220)})
    p = Painter(day)
    paint_alien_objects(p, rng)

    night = gradient((40, 50, 70), (90, 90, 100))
    city_scenery(night, random.Random(seed), {"building": (55, 60, 72), "window": (230, 200, 120), "street": (45, 45, 50), "lane": (220, 220, 210)})
    night = darken(night, 0.55)
    night = vignette(night, 0.5)
    return day, night, p.objects, "#0c0c14"


# ---------------------------------------------------------------- main

def write_set(name, day, night, objects, background):
    folder = os.path.normpath(os.path.join(ROOT, name))
    os.makedirs(folder, exist_ok=True)
    day.save(os.path.join(folder, "day.png"))
    night.save(os.path.join(folder, "night.png"))
    with open(os.path.join(folder, "objects.json"), "w") as f:
        json.dump({"width": W, "height": H, "background": background, "objects": objects}, f, indent=2)
    print(f"{name}: {len(objects)} objects -> {folder}")


def main():
    rng = random.Random(7)
    write_set("daynight_park", *make_park(rng))
    write_set("alien_city", *make_alien(random.Random(3)))


if __name__ == "__main__":
    main()
