# Графика и звук

Графика сделана для этой игры, кроме основы портрета:

- мир, ведьма, враги, деревья и эффекты рисуются кодом векторно (`game/src/Painter.cs`, `game/src/Main.cs`);
- фон титульного экрана — векторный SVG (`game/assets/forest_bg.svg`);
- портрет ведьмы (`game/assets/witch_portrait.png`) — переработка персонажа
  [«Anime Girl: Hachi»](https://opengameart.org/content/anime-girl%EF%BC%9Ahachi) от HachiStudio (OpenGameArt, CC0):
  платье перекрашено, дорисованы шляпа с розами и посох (скрипт сборки портрета — `tools/make_hachi_witch.py`);
- звуки синтезируются кодом (`game/src/Sfx.cs`);
- шрифт — встроенный шрифт Godot.
