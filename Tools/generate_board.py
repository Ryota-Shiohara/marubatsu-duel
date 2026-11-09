"""Generate the matching Unity Image Target and A4 vector artwork with Pillow."""
from io import BytesIO
from math import cos, pi, sin
from pathlib import Path
import random

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
WIDTH, HEIGHT = 2700, 1800
TARGET_SIZE = (1800, 1200)
NAVY = "#16223b"
INK = "#29344a"
GOLD = "#aa7d36"
PALE_GOLD = "#e4c987"
IVORY = "#fcf8ee"
BLUE = "#174d70"
RED = "#792b37"


class Artwork:
    def __init__(self):
        self.image = Image.new("RGB", (WIDTH * 2, HEIGHT * 2), "white")
        self.draw = ImageDraw.Draw(self.image)
        self.elements = []
        self.box(0, 0, WIDTH, HEIGHT, "white")

    def box(self, x, y, width, height, color):
        self.draw.rectangle((2*x, 2*y, 2*(x+width)-1, 2*(y+height)-1), fill=color)
        self.elements.append(f'<rect x="{x}" y="{y}" width="{width}" height="{height}" fill="{color}"/>')

    def line(self, points, color, width=3):
        self.draw.line([(2*x, 2*y) for x, y in points], fill=color, width=2*width, joint="curve")
        coords = " ".join(f"{x},{y}" for x, y in points)
        self.elements.append(
            f'<polyline points="{coords}" fill="none" stroke="{color}" stroke-width="{width}" '
            'stroke-linejoin="round" stroke-linecap="round"/>'
        )

    def polygon(self, points, color):
        self.draw.polygon([(2*x, 2*y) for x, y in points], fill=color)
        coords = " ".join(f"{x},{y}" for x, y in points)
        self.elements.append(f'<polygon points="{coords}" fill="{color}"/>')

    def circle(self, x, y, radius, fill, stroke=None, width=1):
        self.draw.ellipse((2*(x-radius), 2*(y-radius), 2*(x+radius), 2*(y+radius)),
                          fill=fill, outline=stroke, width=2*width)
        attrs = f' stroke="{stroke}" stroke-width="{width}"' if stroke else ""
        self.elements.append(f'<circle cx="{x}" cy="{y}" r="{radius}" fill="{fill or "none"}"{attrs}/>')


def polar(x, y, radius, angle):
    return round(x + radius*cos(angle)), round(y + radius*sin(angle))


def diamond(art, x, y, width, height, color):
    art.polygon([(x-width, y), (x, y-height), (x+width, y), (x, y+height)], color)


def rosette(art, x, y, radius, points, rotation):
    angles = [rotation + 2*pi*i/points for i in range(points)]
    outside = [polar(x, y, radius, angle) for angle in angles]
    inner = [polar(x, y, radius*.40, angle + pi/points) for angle in angles]
    art.line(outside + outside[:1], GOLD, 3)
    for i in range(points):
        next_point = (i+1) % points
        if i % 3 == 0:
            art.polygon([outside[i], inner[i], (x, y), inner[i-1]], "#f2e3be")
        art.line([outside[i], inner[i], outside[next_point]], NAVY, 4)
        art.line([outside[i], (x, y)], NAVY, 3)
        art.line([inner[i], inner[next_point]], GOLD, 3)
    diamond(art, x, y, 11, 11, NAVY)


def header(art):
    art.box(46, 48, WIDTH-92, 244, IVORY)
    art.box(46, 48, WIDTH-92, 7, NAVY)
    art.box(46, 285, WIDTH-92, 7, NAVY)
    art.box(46, 272, WIDTH-92, 3, GOLD)
    motifs = [(146, 167, 78, 6, pi/6), (367, 157, 92, 8, pi/8),
              (580, 171, 77, 6, 0), (797, 161, 96, 8, 0),
              (1020, 159, 82, 7, pi/2), (1238, 168, 91, 6, pi/6),
              (1461, 159, 96, 8, pi/8), (1673, 173, 76, 6, 0),
              (1897, 160, 90, 7, pi/2), (2116, 171, 76, 8, 0),
              (2334, 157, 92, 6, pi/6), (2552, 165, 78, 8, pi/8)]
    for motif in motifs:
        rosette(art, *motif)
    for left, right in zip(motifs, motifs[1:]):
        x = (left[0] + right[0]) // 2
        art.line([(x, 79), (x+22, 169), (x, 248), (x-22, 169), (x, 79)], GOLD, 3)
        diamond(art, x, 169, 11, 19, NAVY)


def footer(art):
    rng = random.Random(2065)
    art.box(46, 1518, WIDTH-92, 234, NAVY)
    x_positions = [46, 166, 300, 433, 583, 725, 869, 1002, 1149, 1282,
                   1443, 1601, 1754, 1884, 2035, 2155, 2320, 2490, 2654]
    rows = [
        [(x, 1530) for x in x_positions],
        [(x, 1612 if x in (46, WIDTH-46) else rng.randint(1579, 1653)) for x in x_positions],
        [(x, 1740) for x in x_positions],
    ]
    palette = [NAVY, INK, "#40516d", GOLD, "#bf964f", PALE_GOLD, IVORY]
    for row in range(2):
        for i in range(len(x_positions)-1):
            corners = [rows[row][i], rows[row][i+1], rows[row+1][i+1], rows[row+1][i]]
            pivot = ((corners[0][0]+corners[1][0])//2 + rng.randint(-21, 21),
                     (corners[0][1]+corners[2][1])//2)
            for index in range(4):
                triangle = [corners[index], corners[(index+1) % 4], pivot]
                art.polygon(triangle, rng.choice(palette))
                art.line(triangle + triangle[:1], NAVY, 3)
    art.box(46, 1518, WIDTH-92, 5, GOLD)
    art.box(46, 1744, WIDTH-92, 4, PALE_GOLD)


def stage(art, x, color, circle_mark):
    y = 900
    art.circle(x, y, 174, IVORY, GOLD, 5)
    art.circle(x, y, 162, color, NAVY, 4)
    art.circle(x, y, 148, None, PALE_GOLD, 3)
    for i in range(8):
        angle = pi * i / 4
        rim = polar(x, y, 146, angle)
        left = polar(x, y, 98, angle-pi/8)
        right = polar(x, y, 98, angle+pi/8)
        art.line([left, rim, right], PALE_GOLD, 3)
    if circle_mark:
        art.circle(x, y, 47, None, IVORY, 9)
    else:
        art.line([(x-38, y-38), (x+38, y+38)], IVORY, 9)
        art.line([(x-38, y+38), (x+38, y-38)], IVORY, 9)


def frame(art):
    art.line([(32, 32), (WIDTH-32, 32), (WIDTH-32, 1768), (32, 1768), (32, 32)], NAVY, 5)
    art.line([(46, 309), (46, 1498)], GOLD, 3)
    art.line([(WIDTH-46, 309), (WIDTH-46, 1498)], GOLD, 3)
    accents = [
        (145, 425, 63, 6, pi/6), (465, 453, 79, 8, 0), (647, 625, 62, 7, 0),
        (151, 1130, 56, 8, pi/8), (330, 1329, 72, 6, 0), (651, 1214, 62, 7, pi/2),
        (2075, 438, 69, 8, 0), (2460, 466, 84, 7, pi/2), (2530, 661, 57, 6, pi/6),
        (2101, 1214, 71, 6, 0), (2340, 1351, 80, 7, pi/2), (2549, 1198, 61, 8, pi/8),
    ]
    for motif in accents:
        rosette(art, *motif)
    for x, y in [(272, 546), (112, 657), (686, 447), (506, 1260), (180, 1415),
                 (2180, 597), (2510, 548), (2045, 1400), (2560, 1378)]:
        diamond(art, x, y, 20, 33, GOLD)
        diamond(art, x, y, 10, 17, NAVY)
    stage(art, 410, BLUE, True)
    stage(art, 2290, RED, False)


def generate():
    art = Artwork()
    frame(art)
    header(art)
    footer(art)
    # Centered 108 mm square. Keep these lines aligned with BoardGeometry.
    for index in range(4):
        x, y = 810 + index*360, 360 + index*360
        art.line([(x, 360), (x, 1440)], NAVY, 5)
        art.line([(810, y), (1890, y)], NAVY, 5)

    target = ROOT / "Assets/Marubatsu/Art/BoardTarget.png"
    with BytesIO() as png:
        art.image.resize(TARGET_SIZE, Image.Resampling.LANCZOS).save(
            png, format="PNG", dpi=(169.333, 169.333), optimize=True
        )
        target.write_bytes(png.getvalue())
    paper = ROOT / "Print/Board_A4.svg"
    paper.write_text(
        '<svg xmlns="http://www.w3.org/2000/svg" width="297mm" height="210mm" viewBox="0 0 2970 2100">\n'
        '<title>Marubatsu Duel — Kumiko board</title>\n'
        '<rect width="2970" height="2100" fill="white"/>\n'
        f'<text x="1485" y="108" text-anchor="middle" font-family="Georgia, serif" font-size="64" '
        f'letter-spacing="9" fill="{NAVY}">MARUBATSU DUEL</text>\n'
        f'<text x="2835" y="102" text-anchor="end" font-family="sans-serif" font-size="28" '
        f'letter-spacing="5" fill="{GOLD}">KUMIKO / TOP</text>\n'
        '<svg x="135" y="150" width="2700" height="1800" viewBox="0 0 2700 1800">\n'
        + "\n".join(art.elements)
        + '\n</svg>\n'
        f'<text x="135" y="2026" font-family="sans-serif" font-size="34" fill="{NAVY}">'
        'A4 landscape / Actual size (100%) / Image target 270 × 180 mm</text>\n'
        f'<path d="M1835 2014H2835 M1835 1998V2030 M2835 1998V2030" stroke="{NAVY}" stroke-width="4"/>\n'
        f'<text x="2335" y="2060" text-anchor="middle" font-family="sans-serif" font-size="28" fill="{INK}">'
        '100 mm</text>\n</svg>\n',
        encoding="utf-8",
    )


if __name__ == "__main__":
    generate()
