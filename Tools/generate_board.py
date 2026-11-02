"""Rebuild the exact RGB target and A4 print SVG. Requires Pillow; no AI assets."""
from pathlib import Path
import random
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]


def generate():
    rng = random.Random(20261102)
    size = 1800
    image = Image.new('RGB', (size, size), 'white')
    draw = ImageDraw.Draw(image)
    elements = ['<rect width="1800" height="1800" fill="white"/>']
    # Unequal angular patches throughout all four bands; leave a white moat around writing.
    for _ in range(1100):
        x, y = rng.randrange(15, 1785), rng.randrange(15, 1785)
        radius = rng.randrange(10, 54)
        if not (x + radius < 310 or x - radius > 1490 or
                y + radius < 310 or y - radius > 1490):
            continue
        points = [(x-radius, y+rng.randrange(-radius, radius)),
                  (x+rng.randrange(-radius, radius), y-radius),
                  (x+radius, y+rng.randrange(-radius, radius)),
                  (x+rng.randrange(-radius, radius), y+radius)]
        shade = rng.choice([12, 30, 60, 100, 155, 200, 235])
        color = (shade, shade, shade)
        draw.polygon(points, fill=color)
        coords = ' '.join(f'{a},{b}' for a, b in points)
        elements.append(f'<polygon points="{coords}" fill="rgb{color}"/>')
    # Play area = 60% of target. Cell width 36 mm; row 0 is the printed top.
    draw.rectangle((360, 360, 1440, 1440), outline='black', width=5)
    elements.append('<rect x="360" y="360" width="1080" height="1080" fill="none" stroke="black" stroke-width="5"/>')
    for pos in (720, 1080):
        draw.line((pos, 360, pos, 1440), fill='black', width=5)
        draw.line((360, pos, 1440, pos), fill='black', width=5)
        elements.append(f'<path d="M{pos} 360V1440 M360 {pos}H1440" fill="none" stroke="black" stroke-width="5"/>')
    art = ROOT / 'Assets/Marubatsu/Art'
    art.mkdir(parents=True, exist_ok=True)
    image.save(art / 'BoardTarget.png', dpi=(254, 254))
    printable = ROOT / 'Print'
    printable.mkdir(exist_ok=True)
    svg = ('<svg xmlns="http://www.w3.org/2000/svg" width="210mm" height="297mm" viewBox="0 0 2100 2970">'
           '<rect width="2100" height="2970" fill="white"/>'
           '<text x="150" y="300" font-family="sans-serif" font-size="62">MARUBATSU DUEL / TOP</text>'
           '<svg x="150" y="450" width="1800" height="1800" viewBox="0 0 1800 1800">'
           + ''.join(elements) + '</svg>'
           '<text x="150" y="2390" font-family="sans-serif" font-size="38">Print A4 at 100% / Target: 180 x 180 mm</text>'
           '<path d="M150 2500H1150 M150 2470V2530 M1150 2470V2530" stroke="black" stroke-width="4"/>'
           '<text x="150" y="2600" font-family="sans-serif" font-size="38">Check this line is exactly 100 mm.</text></svg>')
    (printable / 'Board_A4.svg').write_text(svg, encoding='utf-8')


if __name__ == '__main__':
    generate()
