"""Собирает атлас иконок управления game/assets/input_icons.png из купленного пака
«Controller & Keyboard Icons» (Vryell, https://vryell.itch.io/controller-keyboard-icons).

Пак разрешает использование в играх, но запрещает распространять файлы, поэтому
ни пак, ни атлас в репозиторий не коммитятся (атлас в .gitignore).

Запуск:  python3 tools/make_input_icons.py "<папка Controllers and Keyboard>"
Порядок иконок совпадает с enum InputIcons.Id в game/src/InputIcons.cs.
"""
import os, sys
from PIL import Image

pack = sys.argv[1]
out = sys.argv[2] if len(sys.argv) > 2 else os.path.join(os.path.dirname(__file__), '..', 'game', 'assets', 'input_icons.png')
ICONS = [  # (файл, столбец, строка) в сетке 16x16
    ('kb_dark_all.png', 0, 22),   # W
    ('kb_dark_all.png', 0, 0),    # A
    ('kb_dark_all.png', 0, 18),   # S
    ('kb_dark_all.png', 0, 3),    # D
    ('kb_dark_all.png', 8, 8),    # Space
    ('kb_dark_all.png', 8, 0),    # Esc
    ('mouse.png', 1, 0),          # левая кнопка мыши
    ('kb_dark_all.png', 4, 12),   # 1
    ('kb_dark_all.png', 4, 13),   # 2
    ('kb_dark_all.png', 4, 14),   # 3
    ('controller_ps.png', 0, 4),  # Cross
    ('controller_ps.png', 0, 30), # R1
    ('controller_ps.png', 0, 8),  # крестовина
    ('sticks.png', 0, 3),         # левый стик
    ('sticks.png', 0, 7),         # правый стик
    ('controller_ps.png', 0, 21), # Options (кнопки у тачпада)
    ('kb_dark_all.png', 8, 25),   # Enter
]
atlas = Image.new('RGBA', (16 * len(ICONS), 16), (0, 0, 0, 0))
cache = {}
for i, (f, c, r) in enumerate(ICONS):
    if f not in cache:
        cache[f] = Image.open(os.path.join(pack, f)).convert('RGBA')
    atlas.paste(cache[f].crop((c * 16, r * 16, c * 16 + 16, r * 16 + 16)), (i * 16, 0))
atlas.save(out)
print('saved', out, atlas.size)
