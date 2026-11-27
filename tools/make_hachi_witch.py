# Делает портрет ведьмы из слоёв «Hachi» (HachiStudio, OpenGameArt, CC0):
# перекрашивает платье в фиолетовый и дорисовывает шляпу с розами и посох.
import sys, math, colorsys
from PIL import Image, ImageDraw, ImageFilter
src, out = sys.argv[1], sys.argv[2]
L = {n: Image.open(f'{src}/{n}.png').convert('RGBA') for n in ['back', 'front', 'eyes', 'mouth']}

def recolor(im):
    px = im.load()
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px[x, y]
            if a == 0: continue
            h, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
            if 0.03 <= h <= 0.16 and s > 0.42:
                if h > 0.105 and v > 0.6:      # жёлтый пояс -> белый с сиреневым
                    nh, ns, nv = 0.74, s * 0.25, min(1, v * 1.02)
                else:                           # оранжевое/коричневое -> фиолетовое
                    nh, ns, nv = 0.735 + (h - 0.07) * 0.4, min(1, s * 0.8), v * 0.92
                rr, gg, bb = colorsys.hsv_to_rgb(nh, ns, nv)
                px[x, y] = (int(rr * 255), int(gg * 255), int(bb * 255), a)
    return im

front = recolor(L['front'])

W, H, TOP = 800, 1000, 200
S = 3  # суперсэмплинг для рисованных частей

def layer():
    return Image.new('RGBA', (W * S, H * S), (0, 0, 0, 0))

def P(pts): return [(x * S, (y + TOP) * S) for x, y in pts]

# посох (за спиной персонажа)
staff = layer(); d = ImageDraw.Draw(staff)
d.line(P([(560, 150), (600, 790)]), fill=(74, 46, 36, 255), width=9 * S)
d.line(P([(557, 150), (597, 790)]), fill=(120, 80, 60, 255), width=3 * S)
for i, r in enumerate([46, 30, 18]):
    a = [90, 140, 255][i]
    c = [(190, 140, 255), (215, 180, 255), (250, 240, 255)][i]
    d.ellipse(P([(558 - r, 138 - r), (558 + r, 138 + r)]), fill=c + (a,))
d.polygon(P([(558, 108), (574, 138), (558, 168), (542, 138)]), fill=(170, 110, 255, 255), outline=(255, 255, 255, 255))
staff = staff.filter(ImageFilter.GaussianBlur(1)).resize((W, H), Image.LANCZOS)

# шляпа
hat = layer(); d = ImageDraw.Draw(hat)
cx, cy = 400, 34  # центр полей шляпы (в координатах персонажа)
def rot(pts, ang, c):
    ca, sa = math.cos(ang), math.sin(ang)
    return [(c[0] + (x - c[0]) * ca - (y - c[1]) * sa, c[1] + (x - c[0]) * sa + (y - c[1]) * ca) for x, y in pts]
def ellipse(c, rx, ry, n=90):
    return [(c[0] + rx * math.cos(t * 2 * math.pi / n), c[1] + ry * math.sin(t * 2 * math.pi / n)) for t in range(n)]
ang = -0.12
d.polygon(P(rot(ellipse((cx, cy + 5), 160, 30), ang, (cx, cy))), fill=(62, 38, 128, 255))
d.polygon(P(rot(ellipse((cx, cy), 156, 25), ang, (cx, cy))), fill=(244, 240, 252, 255))
d.polygon(P(rot(ellipse((cx + 10, cy - 3), 110, 13), ang, (cx, cy))), fill=(230, 222, 248, 255))
cone = [(cx - 80, cy - 2), (cx + 76, cy - 20), (cx + 50, cy - 80), (cx + 18, cy - 140), (cx - 20, cy - 186),
        (cx - 70, cy - 196), (cx - 98, cy - 170), (cx - 66, cy - 168), (cx - 48, cy - 140), (cx - 54, cy - 90)]
d.polygon(P(cone), fill=(246, 242, 253, 255))
shade = [(cx - 80, cy - 2), (cx - 54, cy - 90), (cx - 48, cy - 140), (cx - 66, cy - 168), (cx - 98, cy - 170),
         (cx - 70, cy - 150), (cx - 66, cy - 100), (cx - 60, cy - 4)]
d.polygon(P(shade), fill=(206, 194, 236, 255))
band = [(cx - 80, cy - 2), (cx + 76, cy - 20), (cx + 70, cy - 40), (cx - 76, cy - 22)]
d.polygon(P(band), fill=(110, 76, 196, 255))
def rose(x, y, r, petal, center):
    d.ellipse(P([(x - r, y - r), (x + r, y + r)]), fill=petal + (255,))
    for k in range(3):
        rr = r * (0.72 - k * 0.2)
        d.arc(P([(x - rr, y - rr), (x + rr, y + rr)]), 30 + k * 70, 300 + k * 70, fill=center + (255,), width=int(2.2 * S))
rose(cx + 74, cy - 28, 24, (255, 255, 255), (200, 188, 236))
rose(cx + 104, cy - 14, 17, (236, 222, 255), (178, 150, 230))
rose(cx + 48, cy - 16, 15, (250, 214, 234), (226, 140, 186))
for k, (dx, dy) in enumerate([(122, 4), (120, 30)]):
    d.line(P([(cx + 100, cy - 6), (cx + dx, cy + dy + 60), (cx + dx - 10, cy + dy + 110)]), fill=(196, 168, 255, 255), width=6 * S, joint='curve')
hat = hat.resize((W, H), Image.LANCZOS)

# сборка: посох -> контур (back) -> персонаж (front, слои eyes/mouth — варианты целиком) -> шляпа
canvas = Image.new('RGBA', (W, H), (0, 0, 0, 0))
canvas.alpha_composite(staff)
for im in [L['back'], front]:
    canvas.alpha_composite(im, (0, TOP))
canvas.alpha_composite(hat)
canvas = canvas.crop(canvas.getbbox())
canvas.save(out)
print(canvas.size)
