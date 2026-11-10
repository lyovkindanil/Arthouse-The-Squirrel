"""Пересобрать весь пробный арт Мшистой колыбели: python3 Tools/ArtGen/build_all.py (нужны cairosvg, pillow, numpy)."""
import squirrel, enemies, tiles, objects, background

for m in (squirrel, enemies, tiles, objects, background):
    m.build()
    print("ok", m.__name__)
