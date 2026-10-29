/* engine.js: grid painter and canvas renderer, shared by the wallpaper window and the gallery.
   Classic script on purpose: the scene files use these globals directly, the same way the
   original pane mod did. renderScene() is the only entry point the pages call. */

var W = 0, H = 0, CW = 8, CH = 14, COLS = 0, ROWS = 0, S = 1, SOFT = 9;
var val = new Float32Array(1), col = new Uint8Array(1), gl = new Uint8Array(1);
var keyOf = new Int16Array(1), chOf = new Uint16Array(1), order = new Int32Array(1);
var ctx = null;
var SCENES = [];

/* glyph table: scenes pick a glyph by index. The first 13 entries match the original mod. */
var GL = ['', '~', '-', '|', '/', '\\', '^', '=', '.', '+', '*', 'v', '#'];
var GLI = {};
(function () { for (var i = 1; i < GL.length; i++) GLI[GL[i]] = i; })();
function gi(ch) {
  var i = GLI[ch];
  if (i === undefined) { i = GL.length; GL.push(ch); GLI[ch] = i; }
  return i;
}

function clamp(x, a, b) { return x < a ? a : x > b ? b : x; }
function sstep(a, b, x) { var t = clamp((x - a) / (b - a), 0, 1); return t * t * (3 - 2 * t); }
function fract(x) { return x - Math.floor(x); }
function hash(n) { return fract(Math.sin(n * 127.1 + 311.7) * 43758.5453); }
function mod(a, b) { return ((a % b) + b) % b; }
function segD(px, py, ax, ay, bx, by) {
  var vx = bx - ax, vy = by - ay, wx = px - ax, wy = py - ay;
  var t = clamp((wx * vx + wy * vy) / (vx * vx + vy * vy + 1e-6), 0, 1);
  return Math.hypot(wx - vx * t, wy - vy * t);
}
function polyD(x, y, P) {
  var d = -1e9, n = P.length;
  for (var a = 0; a < n; a++) {
    var b = (a + 1) % n, ex = P[b][0] - P[a][0], ey = P[b][1] - P[a][1], L = Math.hypot(ex, ey) || 1;
    var dd = ((x - P[a][0]) * ey - (y - P[a][1]) * ex) / L;
    if (dd > d) d = dd;
  }
  return d;
}

/* ---- grid painting ---- */
function put(i, j, v, c, g) {
  if (i < 0 || j < 0 || i >= COLS || j >= ROWS || v <= 0.03) return;
  if (v > 1) v = 1;
  var k = j * COLS + i;
  if (v > val[k]) { val[k] = v; col[k] = c; gl[k] = g || 0; }
}
function over(i, j, v, c, g) {
  if (i < 0 || j < 0 || i >= COLS || j >= ROWS || v <= 0.03) return;
  var k = j * COLS + i;
  val[k] = v > 1 ? 1 : v; col[k] = c; gl[k] = g || 0;
}
function splat(x, y, v, c, g) {
  var fx = x / CW - 0.5, fy = y / CH - 0.5, i = Math.floor(fx), j = Math.floor(fy), ax = fx - i, ay = fy - j;
  v *= 1.6;
  put(i, j, v * (1 - ax) * (1 - ay), c, g); put(i + 1, j, v * ax * (1 - ay), c, g);
  put(i, j + 1, v * (1 - ax) * ay, c, g); put(i + 1, j + 1, v * ax * ay, c, g);
}
function cov(d) { return clamp(0.5 - d / SOFT, 0, 1); }
var _c = 0, _g = 0;
function fill(x0, y0, x1, y1, fn, ov) {
  var i0 = Math.max(0, Math.floor(x0 / CW)), i1 = Math.min(COLS - 1, Math.ceil(x1 / CW));
  var j0 = Math.max(0, Math.floor(y0 / CH)), j1 = Math.min(ROWS - 1, Math.ceil(y1 / CH));
  for (var j = j0; j <= j1; j++) {
    var py = (j + 0.5) * CH;
    for (var i = i0; i <= i1; i++) {
      _g = 0;
      var v = fn((i + 0.5) * CW, py);
      if (v > 0.03) { if (ov) over(i, j, v, _c, _g); else put(i, j, v, _c, _g); }
    }
  }
}

/* ---- backdrop helpers: scenes call these inside bg(t); they paint straight onto the canvas ---- */
function vgrad(stops) {
  var g = ctx.createLinearGradient(0, 0, 0, H);
  for (var i = 0; i < stops.length; i++) g.addColorStop(clamp(stops[i][0], 0, 1), stops[i][1]);
  ctx.fillStyle = g;
  ctx.fillRect(0, 0, W, H);
}
function glow(x, y, r, rgb, a) {
  if (a < 0.004 || r < 1) return;
  var g = ctx.createRadialGradient(x, y, 0, x, y, r);
  g.addColorStop(0, 'rgba(' + rgb + ',' + a + ')');
  g.addColorStop(1, 'rgba(' + rgb + ',0)');
  ctx.fillStyle = g;
  ctx.fillRect(x - r, y - r, r * 2, r * 2);
}
/* a lens-blur disc: flat in the middle, soft rim */
function bokeh(x, y, r, rgb, a) {
  if (a < 0.004 || r < 1) return;
  var g = ctx.createRadialGradient(x, y, 0, x, y, r);
  g.addColorStop(0, 'rgba(' + rgb + ',' + (a * 0.55) + ')');
  g.addColorStop(0.72, 'rgba(' + rgb + ',' + (a * 0.8) + ')');
  g.addColorStop(0.94, 'rgba(' + rgb + ',' + a + ')');
  g.addColorStop(1, 'rgba(' + rgb + ',0)');
  ctx.fillStyle = g;
  ctx.fillRect(x - r, y - r, r * 2, r * 2);
}

/* ---- colour cache ---- */
var _hex = {};
function hexRgb(h) {
  if (_hex[h]) return _hex[h];
  var n = parseInt(h.slice(1), 16);
  return (_hex[h] = [n >> 16 & 255, n >> 8 & 255, n & 255]);
}

/* ---- renderer ---- */
var NLV = 6, LVA = [0.34, 0.46, 0.58, 0.72, 0.86, 1];
var FONT = '"Cascadia Mono","Cascadia Code",Consolas,"DejaVu Sans Mono",Menlo,monospace';
var _chs = [], _cnt = new Int32Array(1024), _pos = new Int32Array(1024);
function chStr(code) { return _chs[code] || (_chs[code] = String.fromCharCode(code)); }

function setupGrid(vw, vh, rows) {
  ROWS = rows; CW = 8; CH = 14;
  var cellH = vh / ROWS;
  COLS = Math.max(12, Math.round(vw / (cellH * CW / CH)));
  W = COLS * CW; H = ROWS * CH;
  S = clamp(H / 560, 0.6, 2.6);
  SOFT = CW * 1.15;
  var n = COLS * ROWS;
  if (val.length !== n) {
    val = new Float32Array(n); col = new Uint8Array(n); gl = new Uint8Array(n);
    keyOf = new Int16Array(n); chOf = new Uint16Array(n); order = new Int32Array(n);
  }
}

function styleFor(sc, key) {
  var st = sc._st || (sc._st = []);
  if (st[key]) return st[key];
  var rgb = hexRgb(sc.colors[Math.floor(key / NLV)] || '#ffffff');
  return (st[key] = 'rgba(' + rgb[0] + ',' + rgb[1] + ',' + rgb[2] + ',' + LVA[key % NLV] + ')');
}

function drawGlyphs(sc, vw, vh) {
  var n = COLS * ROWS, cw = vw / COLS, ch = vh / ROWS, nk = sc.colors.length * NLV, k, key, v;
  var ramp = sc.ramp, rn = ramp.length;
  _cnt.fill(0, 0, nk + 1);
  for (k = 0; k < n; k++) {
    v = val[k];
    if (v <= 0.03) { keyOf[k] = -1; continue; }
    key = col[k] * NLV + (v > 0.84 ? 5 : v > 0.68 ? 4 : v > 0.52 ? 3 : v > 0.36 ? 2 : v > 0.2 ? 1 : 0);
    keyOf[k] = key; _cnt[key + 1]++;
    chOf[k] = gl[k] ? GL[gl[k]].charCodeAt(0) : ramp.charCodeAt(clamp(1 + Math.floor(v * (rn - 1.001)), 1, rn - 1));
  }
  for (k = 0; k < nk; k++) { _cnt[k + 1] += _cnt[k]; _pos[k] = _cnt[k]; }
  for (k = 0; k < n; k++) { key = keyOf[k]; if (key >= 0) order[_pos[key]++] = k; }
  ctx.font = '700 ' + (ch * 1.04).toFixed(1) + 'px ' + FONT;
  ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
  for (key = 0; key < nk; key++) {
    var a = _cnt[key], b = _cnt[key + 1];
    if (a === b) continue;
    ctx.fillStyle = styleFor(sc, key);
    for (var q = a; q < b; q++) {
      k = order[q];
      ctx.fillText(chStr(chOf[k]), Math.round(((k % COLS) + 0.5) * cw), Math.round((((k / COLS) | 0) + 0.5) * ch));
    }
  }
}

function vignette(vw, vh) {
  var g = ctx.createRadialGradient(vw / 2, vh / 2, Math.min(vw, vh) * 0.3, vw / 2, vh / 2, Math.hypot(vw, vh) * 0.52);
  g.addColorStop(0, 'rgba(0,0,0,0)');
  g.addColorStop(1, 'rgba(0,0,0,0.34)');
  ctx.fillStyle = g;
  ctx.fillRect(0, 0, vw, vh);
}

/* ---- clock overlay: a 5x7 pixel font drawn with '#' cells on the same grid ---- */
var DIG = {
  '0': ['01110', '10001', '10011', '10101', '11001', '10001', '01110'],
  '1': ['00100', '01100', '00100', '00100', '00100', '00100', '01110'],
  '2': ['01110', '10001', '00001', '00010', '00100', '01000', '11111'],
  '3': ['11110', '00001', '00001', '01110', '00001', '00001', '11110'],
  '4': ['00010', '00110', '01010', '10010', '11111', '00010', '00010'],
  '5': ['11111', '10000', '11110', '00001', '00001', '10001', '01110'],
  '6': ['00110', '01000', '10000', '11110', '10001', '10001', '01110'],
  '7': ['11111', '00001', '00010', '00100', '01000', '01000', '01000'],
  '8': ['01110', '10001', '10001', '01110', '10001', '10001', '01110'],
  '9': ['01110', '10001', '10001', '01111', '00001', '00010', '01100'],
  ':': ['0', '0', '1', '0', '1', '0', '0']
};

function drawClock(vw, vh, o) {
  var now = o.now ? new Date(o.now) : new Date(), hh = now.getHours(), mm = now.getMinutes(), ap = '';
  if (!o.clock24) { ap = hh >= 12 ? 'PM' : 'AM'; hh = hh % 12 || 12; }
  var txt = (o.clock24 ? ('0' + hh).slice(-2) : String(hh)) + ':' + ('0' + mm).slice(-2);
  var sz = Math.max(1, Math.round(ROWS * 0.14 / 7 * (o.clockSize || 1)));
  var pw = 2 * sz, ph = sz, cells = [], x = 0, ci, r, c, d;
  for (ci = 0; ci < txt.length; ci++) {
    d = DIG[txt.charAt(ci)];
    for (r = 0; r < 7; r++) for (c = 0; c < d[r].length; c++) if (d[r].charAt(c) === '1') cells.push([x + c, r]);
    x += d[0].length + 1;
  }
  var tw = (x - 1) * pw, th = 7 * ph, line = '';
  if (o.clockDate !== false) {
    line = now.toLocaleDateString('en-US', { weekday: 'long', day: 'numeric', month: 'long' }).toUpperCase();
    if (ap) line += '  ' + ap;
    line = line.split('').join(' ');
  }
  var bw = Math.max(tw, line.length), bh = th + (line ? 3 : 0);
  var mx = Math.round(COLS * 0.04) + 2, my = Math.round(ROWS * 0.06) + 1, pos = o.clock, ox, oy;
  if (pos === 'top-right') { ox = COLS - mx - bw; oy = my; }
  else if (pos === 'bottom-right') { ox = COLS - mx - bw; oy = ROWS - my - bh; }
  else if (pos === 'bottom-center') { ox = Math.round((COLS - bw) / 2); oy = ROWS - my - bh; }
  else { ox = Math.round((COLS - bw) / 2); oy = Math.round((ROWS - bh) / 2); }
  var cw = vw / COLS, ch = vh / ROWS, tx = ox + Math.round((bw - tw) / 2);
  ctx.save();
  ctx.font = '700 ' + (ch * 1.04).toFixed(1) + 'px ' + FONT;
  ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
  for (var pass = 0; pass < 2; pass++) {
    ctx.fillStyle = pass === 0 ? 'rgba(0,0,0,0.38)' : 'rgba(255,255,255,0.93)';
    var off = pass === 0 ? Math.max(1, ch * 0.09) : 0;
    for (ci = 0; ci < cells.length; ci++) {
      for (var sy = 0; sy < ph; sy++) for (var sx = 0; sx < pw; sx++) {
        var gi2 = tx + cells[ci][0] * pw + sx, gj = oy + cells[ci][1] * ph + sy;
        ctx.fillText('#', Math.round((gi2 + 0.5) * cw + off), Math.round((gj + 0.5) * ch + off));
      }
    }
    if (line) {
      ctx.fillStyle = pass === 0 ? 'rgba(0,0,0,0.38)' : 'rgba(255,255,255,0.78)';
      var lx = ox + Math.round((bw - line.length) / 2);
      for (ci = 0; ci < line.length; ci++) {
        if (line.charAt(ci) !== ' ') ctx.fillText(line.charAt(ci), Math.round((lx + ci + 0.5) * cw + off), Math.round((oy + th + 2 + 0.5) * ch + off));
      }
    }
  }
  ctx.restore();
}

/* The wallpaper draws a scene in two layers so the expensive part is cheap:
     drawBackdrop: gradients, glows, hard shapes. Painted into a smaller canvas (bw x bh) that
                   the page scales up for free, and refreshed only ~15 times a second.
     drawField:    the glyph grid, redrawn every frame onto a transparent canvas (vw x vh).
   vw x vh is always the real screen size; it fixes the grid so both layers line up.
   o: { rows, clock, clock24, clockDate, clockSize, now } */
function drawBackdrop(c2d, sc, vw, vh, bw, bh, t, o) {
  ctx = c2d;
  setupGrid(vw, vh, (o && o.rows) || 60);
  ctx.save();
  ctx.setTransform(1, 0, 0, 1, 0, 0);
  ctx.globalAlpha = 1; ctx.globalCompositeOperation = 'source-over';
  ctx.scale(bw / W, bh / H);
  sc.bg(t);
  ctx.restore();
}

function drawField(c2d, sc, vw, vh, t, o) {
  o = o || {};
  ctx = c2d;
  setupGrid(vw, vh, o.rows || 60);
  if (!o.keep) ctx.clearRect(0, 0, vw, vh);
  ctx.globalAlpha = 1; ctx.globalCompositeOperation = 'source-over';
  val.fill(0); gl.fill(0);
  sc.field(t);
  drawGlyphs(sc, vw, vh);
  if (o.clock && o.clock !== 'off') drawClock(vw, vh, o);
}

/* One canvas, both layers, vignette baked in: used by the gallery thumbnails. */
function renderScene(c2d, sc, vw, vh, t, o) {
  o = o || {};
  drawBackdrop(c2d, sc, vw, vh, vw, vh, t, o);
  drawField(c2d, sc, vw, vh, t, { rows: o.rows, keep: true, clock: o.clock, clock24: o.clock24, clockDate: o.clockDate, clockSize: o.clockSize });
  if (o.vignette !== false) vignette(vw, vh);
}

function findScene(slug) {
  for (var n = 0; n < SCENES.length; n++) if (SCENES[n].slug === slug) return SCENES[n];
  return null;
}
