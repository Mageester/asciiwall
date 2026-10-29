# Writing a scene

A scene is a plain JavaScript object pushed onto the global `SCENES` array. Put the file in
`%LOCALAPPDATA%\AsciiWall\scenes\` and choose **Restart wallpaper** from the tray menu; it then
appears in the tray, the gallery and auto-change. [examples/pulse-rings.js](examples/pulse-rings.js)
is a complete working scene.

```js
SCENES.push({
  name: 'My Scene',          // shown in menus
  slug: 'my-scene',          // lowercase letters, digits and dashes; unique
  accent: '#7cf0c9',         // swatch colour in the tray and gallery
  ramp: ' .:-=+*#%@',        // characters from faint to bright
  colors: ['#ffffff', '#7cf0c9'],   // palette used by field()
  bg:    function (t) { /* paint the backdrop */ },
  field: function (t) { /* paint glyphs */ }
});
```

`t` is time in seconds (already scaled by the speed setting).

## The grid

Glyphs live on a grid of cells. All coordinates in a scene are in **grid units**, not pixels:
a cell is `CW` x `CH` = 8 x 14 units, the whole canvas is `W` x `H` units (`COLS` x `ROWS` cells),
and the grid keeps the screen's aspect ratio, so circles stay round. `S` is a size factor that grows
with the screen (about 1 at 1080p): multiply distances by `S` so a scene looks the same on every display.
`SOFT` is a good edge softness for anti-aliasing shapes.

## `field(t)`: the glyph layer

Runs every frame. Write brightness values (0 to 1) into the grid:

| Helper | Does |
|---|---|
| `fill(x0, y0, x1, y1, fn)` | calls `fn(x, y)` for every cell centre in the box; return a brightness 0..1. Set `_c` (palette index) and optionally `_g` (glyph index) inside `fn`. |
| `put(i, j, v, c, g)` | brightest-wins write to cell `(i, j)` with palette colour `c` |
| `over(i, j, v, c, g)` | overwrite a cell |
| `splat(x, y, v, c, g)` | a soft dot at unit coordinates, spread over four cells |
| `cov(d)` | edge coverage from a signed distance, for crisp anti-aliased shapes |

Brightness picks the character from `ramp`; values under about 0.03 draw nothing. To force a specific
character instead (stars, leaves), pass a glyph index from `gi('*')`.

Math helpers: `clamp`, `sstep(a, b, x)` (smoothstep), `fract`, `hash(n)` (stable pseudo-random 0..1),
`mod`, `segD` (distance to a line segment), `polyD` (signed distance to a convex polygon).

## `bg(t)`: the backdrop

Optional but it is what makes a scene look polished. It paints smooth colour behind the glyphs on a
small canvas that is refreshed about 15 times a second and scaled up, so keep it to gradients and glows:

* `vgrad([[0, '#102'], [1, '#000']])`: vertical gradient from stops `[position 0..1, css color]`
* `glow(x, y, radius, '255,200,120', alpha)`: soft radial light (the colour is `'r,g,b'`)
* `bokeh(x, y, radius, 'r,g,b', alpha)`: a lens-blur disc

The backdrop draws on the global canvas context `ctx`, so you can also use ordinary canvas calls
(`ctx.fillStyle`, `ctx.beginPath()`, ...) in grid units. Set `bgScale: 0.35` on the scene for an even smaller backdrop.

## Tips

* Look at `web/scenes-more.js` for scenes of all kinds: a sky with stars, rolling dunes, falling rain.
* Preview quickly without the tray: serve the `web` folder (for example `python -m http.server` inside it) and open
  `wall.html?scene=my-scene&detail=2&t=10`. Arrow keys switch scenes; `t` freezes time. Add your scene's script with a
  `<script>` tag or paste it in the console.
* Keep `field` cheap: it runs for every cell, every frame. Skip cells you know are empty by using a tight `fill` box.
* A throwing scene is caught and logged to the page console, and does not stop the others.
* Scene files run inside the wallpaper page, which has access to the local control API. Only install scene files you trust.
