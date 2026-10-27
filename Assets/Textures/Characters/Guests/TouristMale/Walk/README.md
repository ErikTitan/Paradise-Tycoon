# Chôdza turistu

Postava kráča na mieste v ôsmich smeroch. Pohyb po mape riadi hra.

- `Atlas/walk_atlas_8x8.png`: spoločný atlas 2048 × 2048 px.
- `Sheets/walk_<smer>.png`: osem pásov, každý 2048 × 256 px.
- `Frames/<smer>/walk_<smer>_00.png` až `_07.png`: jednotlivé snímky, 256 × 256 px.
- `animations.json`: cesty k snímkam, oblasti atlasu, smery, časovanie a pivot.

Atlas rozrež na mriežku 256 × 256 px bez medzier. Riadky zhora: **N, NE, E, SE, S, SW, W, NW**. V každom riadku prehrávaj osem snímok zľava doprava v slučke pri **10 fps**; jeden cyklus trvá **0,8 s**.

N = hore, NE = vpravo hore, E = vpravo, SE = vpravo dole, S = dole, SW = vľavo dole, W = vľavo, NW = vľavo hore.

Bod zarovnania je **(128, 234)** od ľavého horného rohu snímky. Normalizovaný pivot od ľavého dolného rohu je **(0.5, 0.0859375)**. Použi priehľadnosť PNG a bilineárne filtrovanie.

Ide o pracovnú generovanú animáciu pre prototyp. Niektoré prechody kroku a zmeny kresby potrebujú ručné doladenie; animácia zatiaľ nebola overená v hernom engine. Rýchlosť posunu postavy zlaď s prehrávaním krokov.
