/* scenes-more.js: nine scenes written for the desktop version.
   Same contract as scenes-classic.js: bg(t) paints the backdrop, field(t) fills the glyph grid.
   Helpers are prefixed (sk, dn, sp, topo) so they cannot collide with the classic scenes. */

/* ================= 9. AURORA ================= */
SCENES.push({
  name: 'Aurora', slug: 'aurora', accent: '#6dffb0',
  ramp: ' .·:+*#%@',
  colors: ['#7dffb2', '#3fe0c5', '#8f7bff', '#d56bff', '#ffffff', '#2f6a82', '#4a8da0'],
  bg: function (t) {
    vgrad([[0, '#02040c'], [0.5, '#06142c'], [0.82, '#0b2a3c'], [1, '#103a46']]);
    ctx.globalCompositeOperation = 'lighter';
    glow(W * 0.5, H * 0.34, H * 0.95, '40,255,170', 0.07 + 0.02 * Math.sin(t * 0.2));
    glow(W * 0.82, H * 0.22, H * 0.6, '150,90,255', 0.06);
    glow(W * 0.15, H * 0.3, H * 0.55, '60,200,255', 0.05);
    ctx.globalCompositeOperation = 'source-over';
    var cols = ['#0c2533', '#07181f', '#030b10'];
    for (var k = 0; k < 3; k++) {
      ctx.beginPath(); ctx.moveTo(0, H);
      for (var x = 0; x <= W + 8; x += 8) {
        var nx = x / H;
        ctx.lineTo(x, H * (0.83 + 0.045 * k) - (34 - k * 8) * S * (Math.sin(nx * (4.5 + k) + k * 2.1) + 0.55 * Math.sin(nx * 11 + k * 1.3) + 0.25 * Math.sin(nx * 27 + k)));
      }
      ctx.lineTo(W, H); ctx.closePath(); ctx.fillStyle = cols[k]; ctx.fill();
    }
  },
  field: function (t) {
    var i, j, k, L;
    for (k = 0; k < 110; k++) {
      var sx = Math.floor(hash(k + 1) * COLS), sy = Math.floor(hash(k + 2) * ROWS * 0.62);
      var tw = 0.5 + 0.5 * Math.sin(t * (0.5 + hash(k + 3) * 1.3) + hash(k + 4) * 6);
      put(sx, sy, 0.2 + 0.5 * tw, 4, hash(k + 5) > 0.9 ? 10 : 8);
    }
    for (L = 0; L < 3; L++) {
      for (i = 0; i < COLS; i++) {
        var nx = (i + 0.5) * CW / H;
        var yc = H * (0.3 + 0.05 * L) + Math.sin(nx * 4.2 + t * 0.11 * (1 + L * 0.3) + L * 2) * H * 0.07 + Math.sin(nx * 9.5 - t * 0.19 + L) * H * 0.04 + Math.sin(nx * 17 + t * 0.31 + L * 2) * H * 0.016;
        var env = 0.3 + 0.7 * (0.5 + 0.5 * Math.sin(nx * 1.9 + t * 0.06 + L * 1.7)), jmax = Math.floor(ROWS * 0.82);
        for (j = 0; j < jmax; j++) {
          var nxs = nx + ((j + 0.5) * CH / H - 0.3) * 0.16;
          var ray = clamp(0.5 + 0.32 * Math.sin(nxs * 24 + t * (0.45 + L * 0.2) + 2.2 * Math.sin(nxs * 7 + t * 0.27 + L * 3)) + 0.12 * Math.sin(nxs * 38 - t * 0.7 + L), 0, 1);
          var th = H * (0.08 + 0.04 * L) * (0.5 + 0.9 * ray);
          var dy = ((j + 0.5) * CH - yc) / th;
          var prof = dy > 0 ? Math.exp(-dy * dy * 6) : Math.exp(-dy * dy * 0.6);
          var v = prof * (0.2 + 0.8 * ray) * env * (0.95 - 0.12 * L);
          if (v < 0.06) continue;
          put(i, j, v, dy > 0.15 ? 0 : dy > -0.55 ? 1 : dy > -1.2 ? 2 : 3, v < 0.55 && hash(i * 7.13 + j * 1.7 + L) > 0.45 ? 3 : 0);
        }
      }
    }
    var trees = Math.round(COLS * 0.09);
    for (k = 0; k < trees; k++) {
      var tx = Math.floor(hash(k * 3.3 + 40) * COLS), th2 = 4 + Math.floor(hash(k * 5.1 + 41) * 6);
      for (j = 0; j < th2; j++) {
        var hw = Math.floor(j * 0.55);
        for (i = -hw; i <= hw; i++) over(tx + i, ROWS - th2 + j, 0.62 + 0.3 * (j / th2), (tx + i) & 1 ? 5 : 6, 6);
      }
    }
  }
});

/* ================= 10. NIGHT RAIN ================= */
SCENES.push({
  name: 'Night Rain', slug: 'night-rain', accent: '#8fb8ff',
  ramp: ' .·:+*#%@',
  colors: ['#d6e6ff', '#9db8ee', '#6b86c4', '#46598f'],
  bg: function (t) {
    vgrad([[0, '#04060d'], [0.5, '#0a1224'], [0.8, '#131c38'], [1, '#1b2748']]);
    ctx.globalCompositeOperation = 'lighter';
    var pal = ['255,150,90', '255,110,160', '120,215,255', '255,214,120', '160,130,255'];
    for (var k = 0; k < 18; k++) {
      var bx = W * (0.03 + 0.94 * hash(k * 3.1 + 1)) + Math.sin(t * 0.05 + k) * W * 0.008;
      var by = H * (0.4 + 0.36 * hash(k * 5.3 + 2));
      var r = (26 + hash(k * 2.7 + 3) * 64) * S, pulse = 0.55 + 0.45 * Math.sin(t * (0.25 + hash(k) * 0.4) + k * 2);
      bokeh(bx, by, r, pal[k % 5], 0.085 * pulse);
    }
    glow(W * 0.5, H * 1.02, H * 0.9, '70,100,200', 0.14);
    ctx.globalCompositeOperation = 'source-over';
  },
  field: function (t) {
    var i, j, k, r0 = Math.floor(ROWS * 0.86);
    for (j = r0; j < ROWS; j++) for (i = 0; i < COLS; i++) {
      var sh = 0.5 + 0.5 * Math.sin(i * 0.5 + j * 1.7 - t * 0.7 + Math.sin(i * 0.11 + t * 0.3) * 2);
      if (sh > 0.72) put(i, j, (sh - 0.72) * 0.9, 3, 1);
    }
    for (k = 0; k < 30; k++) {
      var per = 1.1 + hash(k + 5) * 1.3, cyc = t / per + hash(k) * 5, idx = Math.floor(cyc), age = fract(cyc) * per;
      var fade = 1 - age / (per * 0.92);
      if (fade <= 0) continue;
      var R = age * 46 * S, ext = R + SOFT * 3;
      var cx = W * hash(k * 13.1 + idx * 0.37 + 3), cy = H * (0.875 + 0.115 * hash(k * 17.7 + idx * 0.91 + 4));
      (function (cx2, cy2, R2, f2, e2) {
        fill(cx2 - e2, cy2 - e2 * 0.35, cx2 + e2, cy2 + e2 * 0.35, function (px, py) {
          var d = Math.hypot(px - cx2, (py - cy2) * 3) - R2;
          _c = 1; return Math.exp(-d * d / (SOFT * SOFT * 1.2)) * f2 * 0.85;
        });
      })(cx, cy, R, fade, ext);
    }
    var LY = [{ n: 0.5, sp: 400, len: 4, c: 0, v: 0.95 }, { n: 0.8, sp: 300, len: 3, c: 1, v: 0.7 }, { n: 1.0, sp: 220, len: 2, c: 2, v: 0.5 }];
    for (var li = 0; li < LY.length; li++) {
      var Ly = LY[li], N = Math.round(COLS * Ly.n), span = H + 10 * CH;
      for (var d = 0; d < N; d++) {
        var xc = Math.floor(hash(d * 7.13 + li * 101 + 1) * COLS);
        var y = mod(hash(d * 3.7 + li * 31 + 3) * span + t * Ly.sp * S * (0.85 + 0.3 * hash(d + li * 50 + 2)), span) - 5 * CH;
        var head = Math.floor(y / CH);
        for (var q = 0; q < Ly.len + 2; q++) put(xc, head - q, Ly.v * (1 - q / (Ly.len + 2)), Ly.c, 3);
      }
    }
  }
});

/* ================= 11. WINTER PINES ================= */
function spPine(x, base, h, w) {
  var R = [], m, yy, ww;
  for (m = 1; m <= 4; m++) {
    yy = base - h + h * (m / 4.4); ww = w * (0.3 + 0.7 * m / 4);
    R.push([ww, yy]);
    R.push([m < 4 ? ww * 0.42 : w * 0.1, yy - h * 0.045]);
  }
  ctx.beginPath(); ctx.moveTo(x, base - h);
  for (m = 0; m < R.length; m++) ctx.lineTo(x + R[m][0], R[m][1]);
  ctx.lineTo(x + w * 0.1, base); ctx.lineTo(x - w * 0.1, base);
  for (m = R.length - 1; m >= 0; m--) ctx.lineTo(x - R[m][0], R[m][1]);
  ctx.closePath(); ctx.fill();
}
SCENES.push({
  name: 'Winter Pines', slug: 'winter-pines', accent: '#cfe2ff',
  ramp: ' .·:+*#%@',
  colors: ['#ffffff', '#d5e5ff', '#9bb8f0', '#fffbe8'],
  bg: function (t) {
    vgrad([[0, '#060b22'], [0.55, '#12214a'], [0.9, '#26397a'], [1, '#2d4287']]);
    ctx.globalCompositeOperation = 'lighter';
    glow(W * 0.22, H * 0.2, H * 0.75, '170,200,255', 0.2 + 0.02 * Math.sin(t * 0.3));
    glow(W * 0.5, H * 0.95, H * 0.7, '90,120,220', 0.12);
    ctx.globalCompositeOperation = 'source-over';
    var LYR = [
      { base: 0.8, h: 120, w: 36, gap: 50, col: '#1c2d62', seed: 1 },
      { base: 0.9, h: 190, w: 54, gap: 100, col: '#111f48', seed: 2 },
      { base: 1.02, h: 250, w: 80, gap: 220, col: '#08122e', seed: 3 }
    ];
    for (var L = 0; L < LYR.length; L++) {
      var Y = LYR[L];
      ctx.fillStyle = Y.col;
      var n = Math.ceil(W / (Y.gap * S)) + 1;
      for (var k = 0; k < n; k++) {
        var hh = Y.h * S * (0.75 + 0.5 * hash(k * 3.7 + Y.seed * 10)), x = (k + 0.15 + 0.7 * hash(k * 5.9 + Y.seed * 20)) * Y.gap * S;
        spPine(x, H * Y.base, hh, Y.w * S * (0.85 + 0.3 * hash(k + Y.seed * 30)));
      }
      if (L < 2) {
        var hz = ctx.createLinearGradient(0, H * 0.55, 0, H);
        hz.addColorStop(0, 'rgba(60,90,170,0)'); hz.addColorStop(1, 'rgba(60,90,170,' + (0.26 - 0.1 * L) + ')');
        ctx.fillStyle = hz; ctx.fillRect(0, H * 0.55, W, H * 0.45);
      }
    }
    ctx.beginPath(); ctx.moveTo(0, H);
    for (var x2 = 0; x2 <= W + 8; x2 += 8) ctx.lineTo(x2, H * 0.965 - 10 * S * (Math.sin(x2 / H * 5 + 1) + 0.5 * Math.sin(x2 / H * 13)));
    ctx.lineTo(W, H); ctx.closePath(); ctx.fillStyle = '#34498f'; ctx.fill();
  },
  field: function (t) {
    var k, mx = W * 0.22, my = H * 0.2, mr = 40 * S;
    fill(mx - mr - SOFT, my - mr - SOFT, mx + mr + SOFT, my + mr + SOFT, function (px, py) {
      var d = Math.hypot(px - mx, py - my) - mr; _c = 3; return cov(d) * (0.78 + 0.22 * sstep(-mr, mr * 0.4, -d));
    });
    var star = Math.round(COLS * 0.35);
    for (k = 0; k < star; k++) {
      var tw = 0.5 + 0.5 * Math.sin(t * (0.5 + hash(k + 3) * 1.2) + hash(k + 4) * 6);
      put(Math.floor(hash(k + 1) * COLS), Math.floor(hash(k + 2) * ROWS * 0.5), 0.15 + 0.4 * tw, 1, 8);
    }
    var FL = [
      { n: 0.9, v0: 10, v1: 18, g: 8, c: 2, a: 0.5, sw: 10 },
      { n: 0.55, v0: 22, v1: 34, g: gi('·'), c: 1, a: 0.75, sw: 16 },
      { n: 0.24, v0: 44, v1: 72, g: 10, c: 0, a: 1, sw: 26 }
    ];
    for (var li = 0; li < FL.length; li++) {
      var F = FL[li], N = Math.round(COLS * F.n);
      for (k = 0; k < N; k++) {
        var sd = k * 5.7 + li * 91, spd = (F.v0 + (F.v1 - F.v0) * hash(sd + 2)) * S;
        var x = mod(hash(sd + 1) * W + t * 7 * S * (0.4 + li * 0.5) + Math.sin(t * (0.3 + hash(sd + 4) * 0.5) + k) * F.sw * S, W);
        var y = mod(hash(sd + 3) * H + t * spd, H);
        splat(x, y, F.a, F.c, F.g === 10 && hash(sd + 6) > 0.6 ? 9 : F.g);
      }
    }
  }
});

/* ================= 12. GALAXY ================= */
SCENES.push({
  name: 'Galaxy', slug: 'galaxy', accent: '#b9a2ff',
  ramp: ' .·:+*#%@',
  colors: ['#fff3d6', '#ffd3a1', '#9fc0ff', '#c58bff', '#ff8fc8', '#ffffff'],
  bg: function (t) {
    vgrad([[0, '#02010a'], [0.5, '#07041a'], [1, '#0b0626']]);
    ctx.globalCompositeOperation = 'lighter';
    var gx = W * 0.5, gy = H * 0.5;
    glow(gx, gy, H * 0.55, '255,190,140', 0.16 + 0.02 * Math.sin(t * 0.3));
    glow(gx, gy, H * 1.1, '120,80,220', 0.1);
    glow(W * 0.12, H * 0.25, H * 0.8, '170,60,200', 0.07);
    glow(W * 0.9, H * 0.8, H * 0.9, '40,100,230', 0.08);
    ctx.globalCompositeOperation = 'source-over';
  },
  field: function (t) {
    var k, gx = W * 0.5, gy = H * 0.5, R = Math.min(W * 0.48, H * 1.05), rot = t * 0.03;
    var ph = -0.38, cp = Math.cos(ph), sp = Math.sin(ph), N = Math.min(4400, Math.round(COLS * ROWS * 0.36));
    var bgStars = Math.round(COLS * ROWS * 0.012);
    for (k = 0; k < bgStars; k++) {
      var tw = 0.5 + 0.5 * Math.sin(t * (0.5 + hash(k + 3) * 1.4) + hash(k + 4) * 6);
      put(Math.floor(hash(k + 1) * COLS), Math.floor(hash(k + 2) * ROWS), 0.2 + 0.5 * tw, 5, hash(k + 6) > 0.92 ? 10 : 8);
    }
    for (k = 0; k < N; k++) {
      var arm = k & 1, s = hash(k * 1.7 + 1), r = R * Math.pow(s, 0.72);
      var scat = hash(k * 2.9 + 2) + hash(k * 4.3 + 3) - 1;
      var th = arm * Math.PI + (r / R) * 5.2 + rot + scat * 0.55 * (0.35 + s);
      var rr = r * (1 + (hash(k * 6.1 + 4) - 0.5) * 0.12);
      var x = rr * Math.cos(th), y = rr * Math.sin(th) * 0.46;
      var inten = (0.3 + 0.7 * Math.pow(1 - s, 1.6)) * (0.75 + 0.25 * Math.sin(t * (0.6 + hash(k * 8.7) * 1.5) + k));
      var c = s < 0.1 ? 0 : s < 0.35 ? 1 : s < 0.72 ? 2 : 3;
      if (s > 0.3 && hash(k * 9.1 + 5) > 0.93) c = 4;
      splat(gx + x * cp - y * sp, gy + x * sp + y * cp, inten * 0.8, c, hash(k * 11.3 + 6) > 0.85 ? 10 : 0);
    }
    fill(gx - R * 0.2, gy - R * 0.14, gx + R * 0.2, gy + R * 0.14, function (px, py) {
      var d = Math.hypot(px - gx, (py - gy) / 0.7) / (R * 0.11);
      _c = 0; return Math.exp(-d * d);
    });
    var cyc = t / 11, idx = Math.floor(cyc), pp = fract(cyc);
    if (pp < 0.09 && hash(idx + 7) > 0.3) {
      var p = pp / 0.09, sx0 = W * (0.05 + 0.5 * hash(idx + 7)), sy0 = H * (0.04 + 0.25 * hash(idx + 8)), dl = Math.hypot(1, 0.45);
      var hx = sx0 + p * W * 0.3, hy2 = sy0 + p * W * 0.3 * 0.45;
      for (k = 0; k < 14; k++) splat(hx - k * CW * 0.9 / dl, hy2 - k * CW * 0.9 * 0.45 / dl, (1 - k / 14) * (1 - p * 0.4), k === 0 ? 5 : 3);
    }
  }
});

/* ================= 13. SAKURA ================= */
function skBez(p, u) {
  var a = 1 - u;
  return [a * a * a * p[0][0] + 3 * a * a * u * p[1][0] + 3 * a * u * u * p[2][0] + u * u * u * p[3][0],
          a * a * a * p[0][1] + 3 * a * a * u * p[1][1] + 3 * a * u * u * p[2][1] + u * u * u * p[3][1]];
}
function skBranches() {
  var m = [[W + 0.05 * H, 0], [W - 0.2 * H, 0.13 * H], [W - 0.75 * H, 0.09 * H], [W - 1.15 * H, 0.27 * H]];
  var a = skBez(m, 0.42), b = skBez(m, 0.7), c = skBez(m, 0.18), e = m[3];
  return [
    { p: m, w: 16 },
    { p: [a, [a[0] - 0.05 * H, a[1] - 0.09 * H], [a[0] - 0.2 * H, a[1] - 0.12 * H], [a[0] - 0.34 * H, a[1] - 0.03 * H]], w: 9 },
    { p: [b, [b[0] + 0.02 * H, b[1] + 0.1 * H], [b[0] - 0.04 * H, b[1] + 0.2 * H], [b[0] - 0.14 * H, b[1] + 0.28 * H]], w: 8 },
    { p: [e, [e[0] - 0.08 * H, e[1] + 0.05 * H], [e[0] - 0.17 * H, e[1] + 0.02 * H], [e[0] - 0.26 * H, e[1] + 0.09 * H]], w: 6 },
    { p: [c, [c[0] + 0.04 * H, c[1] + 0.1 * H], [c[0] + 0.02 * H, c[1] + 0.22 * H], [c[0] - 0.05 * H, c[1] + 0.32 * H]], w: 8 }
  ];
}
SCENES.push({
  name: 'Sakura', slug: 'sakura', accent: '#ffa6c6',
  ramp: ' .·:+*#%@',
  colors: ['#ffe9f0', '#ffc7d9', '#ff9fc0', '#ff7aa8', '#fff0c8'],
  bg: function (t) {
    vgrad([[0, '#3b2a63'], [0.4, '#9a5f9a'], [0.75, '#f2a0a0'], [1, '#ffd8b8']]);
    ctx.globalCompositeOperation = 'lighter';
    glow(W * 0.28, H * 0.72, H * 0.9, '255,196,170', 0.32 + 0.03 * Math.sin(t * 0.25));
    ctx.globalCompositeOperation = 'source-over';
    var hills = ['#8a5590', '#60377a'], k, x;
    for (k = 0; k < 2; k++) {
      ctx.beginPath(); ctx.moveTo(0, H);
      for (x = 0; x <= W + 8; x += 8) ctx.lineTo(x, H * (0.8 + 0.08 * k) - 24 * S * (Math.sin(x / H * (2.6 + k) + k * 1.9) + 0.5 * Math.sin(x / H * 7 + k)));
      ctx.lineTo(W, H); ctx.closePath(); ctx.fillStyle = hills[k]; ctx.fill();
    }
    var br = skBranches(), u, q, pa, pb;
    ctx.lineCap = 'round'; ctx.strokeStyle = '#2a1826';
    for (k = 0; k < br.length; k++) {
      for (q = 0; q < 24; q++) {
        pa = skBez(br[k].p, q / 24); pb = skBez(br[k].p, (q + 1) / 24);
        ctx.lineWidth = br[k].w * S * 1.3 * (1 - 0.6 * (q / 24));
        ctx.beginPath(); ctx.moveTo(pa[0], pa[1]); ctx.lineTo(pb[0], pb[1]); ctx.stroke();
      }
    }
  },
  field: function (t) {
    var br = skBranches(), k, n, q, GLY = [gi('*'), gi('@'), gi('o'), gi('%')];
    for (k = 0; k < br.length; k++) {
      var nb = k === 0 ? 8 : 5;
      for (n = 0; n < nb; n++) {
        var u = 0.22 + 0.78 * (n + hash(k * 9 + n)) / nb, p = skBez(br[k].p, u);
        var cx = p[0] + Math.sin(t * 0.5 + k * 3 + n) * CW * 0.25, cy = p[1] + Math.cos(t * 0.4 + n) * CH * 0.2;
        var rc = (22 + 20 * hash(k * 17 + n * 3)) * S;
        for (q = 0; q < 34; q++) {
          var a = hash(k * 31 + n * 7 + q) * 6.283, rad = Math.pow(hash(k * 13 + n * 5 + q + 3), 0.6) * rc;
          var hs = hash(k * 41 + n * 11 + q + 9);
          put(Math.floor((cx + Math.cos(a) * rad * 1.3) / CW), Math.floor((cy + Math.sin(a) * rad * 0.75) / CH),
              0.72 + 0.28 * hash(q + n * 3), hs > 0.85 ? 4 : Math.floor(hs * 4), GLY[Math.floor(hash(q * 3 + n + k) * 4)]);
        }
      }
    }
    for (k = 0; k < 110; k++) {
      var dp = hash(k * 3.1 + 1), spd = (12 + dp * 34) * S;
      var x = mod(hash(k * 5.7 + 2) * (W + 200) + t * (5 + dp * 10) * S + Math.sin(t * (0.6 + hash(k) * 0.7) + k * 2) * (12 + dp * 18) * S, W + 200) - 100;
      var y = mod(hash(k * 7.3 + 3) * (H + 120) + t * spd, H + 120) - 60;
      var ph = fract(t * (0.3 + hash(k + 9) * 0.5) + hash(k + 4));
      splat(x, y, 0.6 + 0.4 * dp, dp > 0.6 ? 0 : dp > 0.3 ? 1 : 2, ph < 0.3 ? 1 : ph < 0.6 ? 10 : ph < 0.8 ? 8 : gi('o'));
    }
  }
});

/* ================= 14. DUNES ================= */
function dnY(L, x) {
  var nx = x / H;
  return H * (0.6 + 0.085 * L) - (22 + L * 14) * S * (Math.sin(nx * (2.2 + L * 0.5) + L * 2.3) + 0.5 * Math.sin(nx * (5.1 + L) + L * 1.1) + 0.2 * Math.sin(nx * 11 + L));
}
SCENES.push({
  name: 'Dunes', slug: 'dunes', accent: '#ffb067',
  ramp: ' .·:+oO@',
  colors: ['#fff4d6', '#ffd9a0', '#ffb067', '#e0735a', '#c46d7e', '#fff0d0'],
  bg: function (t) {
    vgrad([[0, '#1b1445'], [0.3, '#5b3377'], [0.52, '#d9637a'], [0.62, '#ffa26a'], [0.7, '#ffd08a'], [1, '#ffe3b0']]);
    ctx.globalCompositeOperation = 'lighter';
    glow(W * 0.68, H * 0.57, H * 0.75, '255,205,130', 0.42 + 0.04 * Math.sin(t * 0.3));
    ctx.globalCompositeOperation = 'source-over';
    var cols = ['#c46d7e', '#9a4a73', '#6a2f66', '#3a1a45'];
    for (var L = 0; L < 4; L++) {
      ctx.beginPath(); ctx.moveTo(0, H);
      for (var x = 0; x <= W + 8; x += 8) ctx.lineTo(x, dnY(L, x));
      ctx.lineTo(W, H); ctx.closePath(); ctx.fillStyle = cols[L]; ctx.fill();
    }
  },
  field: function (t) {
    var i, j, k, L, sx = W * 0.68, sy = H * 0.535, sr = 54 * S;
    for (k = 0; k < 40; k++) {
      var tw = 0.5 + 0.5 * Math.sin(t * (0.5 + hash(k + 3) * 1.2) + hash(k + 4) * 6);
      put(Math.floor(hash(k + 1) * COLS), Math.floor(hash(k + 2) * ROWS * 0.28), 0.15 + 0.4 * tw, 5, 8);
    }
    fill(sx - sr - SOFT, sy - sr - SOFT, sx + sr + SOFT, sy + sr + SOFT, function (px, py) {
      if (py > dnY(0, px)) return 0;
      _c = 0; return cov(Math.hypot(px - sx, py - sy) - sr) * (0.88 + 0.12 * Math.sin(py * 0.08 + t));
    });
    for (k = 0; k < 4; k++) {
      var cy = H * (0.17 + 0.05 * k), len = 22 + Math.floor(hash(k + 60) * 24), xs = mod(hash(k + 61) * COLS * 2 + t * (0.9 + hash(k + 62) * 0.8), COLS + len * 2) - len;
      for (var q = 0; q < len; q++) put(Math.floor(xs + q), Math.floor(cy / CH), 0.22 * Math.sin(Math.PI * q / len), 2, k & 1 ? 2 : 1);
    }
    for (L = 0; L < 4; L++) {
      for (i = 0; i < COLS; i++) {
        var x = (i + 0.5) * CW, yc = dnY(L, x), sl = (dnY(L, x + CW) - dnY(L, x - CW)) / (2 * CW), row = Math.floor(yc / CH);
        put(i, row, 0.9, 1 + L, sl < -0.35 ? 4 : sl > 0.35 ? 5 : gi('_'));
        if (sl < 0.05) for (j = 1; j <= 3; j++) put(i, row + j, 0.3 * (1 - j / 4), 1 + L, 8);
        else if (L >= 2) put(i, row + 1, 0.16 + 0.1 * Math.sin(i * 0.9 + t * 0.4), 1 + L, 8);
      }
    }
    for (k = 0; k < 16; k++) {
      var Ls = 1 + (k % 3), len2 = 8 + Math.floor(hash(k + 80) * 14), xs2 = mod(hash(k + 81) * COLS * 2 + t * (4 + hash(k + 82) * 6), COLS + len2 * 2) - len2;
      for (var q2 = 0; q2 < len2; q2++) {
        var ii = Math.floor(xs2 + q2), yy = dnY(Ls, (ii + 0.5) * CW);
        put(ii, Math.floor(yy / CH) - 1 - (k & 1), 0.38 * Math.sin(Math.PI * q2 / len2), 1 + Ls, 1);
      }
    }
  }
});

/* ================= 15. CONTOURS ================= */
var topoBuf = new Float32Array(1);
function topoH(nx, ny, t) {
  return 0.5 + 0.26 * Math.sin(nx * 4.1 + t * 0.045 + 1.4 * Math.sin(ny * 3.3 - t * 0.033))
             + 0.22 * Math.sin(ny * 5.2 - t * 0.037 + 1.3 * Math.sin(nx * 2.9 + t * 0.041))
             + 0.13 * Math.sin((nx + ny) * 7.7 + t * 0.052)
             + 0.07 * Math.sin(nx * 13.1 - ny * 9.7 + t * 0.06);
}
SCENES.push({
  name: 'Contours', slug: 'contours', accent: '#8fd6a0',
  ramp: ' .·:+*#%@',
  colors: ['#2f7f9e', '#4fb3b0', '#8fd6a0', '#d8e6a0', '#f4d9a6'],
  bg: function (t) {
    vgrad([[0, '#0a1620'], [1, '#0f2430']]);
    ctx.globalCompositeOperation = 'lighter';
    glow(W * 0.5, H * 0.5, H * 1.1, '60,140,150', 0.09);
    ctx.globalCompositeOperation = 'source-over';
  },
  field: function (t) {
    var i, j, LV = 11, w2 = COLS + 2, n2 = w2 * (ROWS + 2);
    if (topoBuf.length !== n2) topoBuf = new Float32Array(n2);
    for (j = 0; j < ROWS + 2; j++) {
      var ny = ((j - 0.5) * CH) / H;
      for (i = 0; i < w2; i++) topoBuf[j * w2 + i] = topoH(((i - 0.5) * CW) / H, ny, t);
    }
    for (j = 0; j < ROWS; j++) for (i = 0; i < COLS; i++) {
      var k = (j + 1) * w2 + i + 1, h = topoBuf[k];
      var gx = (topoBuf[k + 1] - topoBuf[k - 1]) / (2 * CW), gy = (topoBuf[k + w2] - topoBuf[k - w2]) / (2 * CH);
      var gm = Math.hypot(gx, gy) + 1e-6, q = h * LV, f = fract(q), de = Math.min(f, 1 - f) / LV;
      var idxLine = (Math.floor(q + 0.5) % 4 + 4) % 4 === 0;
      var nx = gx / gm, ny2 = gy / gm, thr = 0.5 * (Math.abs(nx) * CW + Math.abs(ny2) * CH) * (idxLine ? 1.15 : 0.8);
      var dist = de / gm;
      if (dist >= thr) continue;
      var a = Math.atan2(nx, -ny2);
      if (a < 0) a += Math.PI;
      var g = (a < 0.39 || a >= 2.75) ? 2 : a < 1.18 ? 5 : a < 1.96 ? 3 : 4;
      put(i, j, (idxLine ? 0.7 : 0.45) + 0.3 * (1 - dist / thr), clamp(Math.floor(h * 5.2), 0, 4), g);
    }
  }
});

/* ================= 16. LAVA ================= */
SCENES.push({
  name: 'Lava', slug: 'lava', accent: '#ff8a3d',
  ramp: ' .·:+*#%@',
  colors: ['#ff3d6e', '#ff7a3d', '#ffb347', '#fff0b8', '#8a3bd6'],
  bg: function (t) {
    vgrad([[0, '#12041f'], [0.55, '#220735'], [1, '#33093e']]);
    ctx.globalCompositeOperation = 'lighter';
    glow(W * 0.5, H * 1.05, H * 0.9, '255,80,100', 0.2);
    glow(W * 0.5, -H * 0.05, H * 0.8, '140,60,220', 0.12);
    ctx.globalCompositeOperation = 'source-over';
  },
  field: function (t) {
    var n = 12, bx = [], by = [], br = [], k, i, j;
    for (k = 0; k < n; k++) {
      bx.push(W * (0.08 + 0.84 * hash(k * 1.9 + 1)) + Math.sin(t * (0.07 + hash(k + 20) * 0.07) + k * 2.3) * W * 0.05);
      by.push(H * (0.5 + 0.56 * Math.sin(t * (0.045 + hash(k + 30) * 0.05) + k * 1.7)));
      br.push((24 + hash(k * 3.3 + 2) * 44) * S);
      br[k] *= br[k];
    }
    for (j = 0; j < ROWS; j++) {
      var py = (j + 0.5) * CH;
      for (i = 0; i < COLS; i++) {
        var px = (i + 0.5) * CW, f = 0;
        for (k = 0; k < n; k++) { var dx = px - bx[k], dy = py - by[k]; f += br[k] / (dx * dx + dy * dy + 40); }
        if (f < 0.35) continue;
        if (f < 1) { put(i, j, 0.3 * sstep(0.35, 1, f), 4, 8); continue; }
        put(i, j, 0.42 + 0.58 * sstep(1, 2.6, f), f < 1.25 ? 0 : f < 1.8 ? 1 : f < 2.6 ? 2 : 3);
      }
    }
  }
});

/* ================= 17. DIGITAL RAIN ================= */
var RAINCH = null;
SCENES.push({
  name: 'Digital Rain', slug: 'digital-rain', accent: '#3dff8a',
  ramp: ' .:+*#%@',
  colors: ['#e4ffee', '#46ff88', '#16c25a', '#0b7a38', '#06451f'],
  bg: function (t) {
    vgrad([[0, '#000604'], [0.6, '#00110a'], [1, '#001d10']]);
    ctx.globalCompositeOperation = 'lighter';
    glow(W * 0.5, H * 1.1, H * 0.9, '0,200,100', 0.12);
    ctx.globalCompositeOperation = 'source-over';
  },
  field: function (t) {
    if (!RAINCH) {
      RAINCH = [];
      '0123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghkmnpqrstuvwxyz<>/\\|=+-*:;'.split('').forEach(function (ch) { RAINCH.push(gi(ch)); });
    }
    for (var i = 0; i < COLS; i++) {
      if (hash(i * 1.77 + 5) < 0.16) continue;
      var sp = 6 + hash(i * 3.1 + 7) * 16, len = 8 + Math.floor(hash(i * 5.3 + 9) * 22), cycle = ROWS + len + 6;
      var head = Math.floor(mod(t * sp + hash(i * 7.7 + 11) * cycle, cycle)) - len;
      var dim = 0.55 + 0.45 * hash(i * 2.3 + 13), rate = 1.5 + hash(i * 0.7) * 2;
      for (var q = 0; q < len; q++) {
        var j = head - q;
        if (j < 0 || j >= ROWS) continue;
        var f = 1 - q / len, tick = Math.floor(t * rate + hash(i + j) * 3);
        var gch = RAINCH[Math.floor(hash(i * 131.7 + j * 17.3 + tick * 7.1) * RAINCH.length) % RAINCH.length];
        over(i, j, (q === 0 ? 1 : 0.22 + 0.78 * f * f) * dim, q === 0 ? 0 : q < 3 ? 1 : f > 0.5 ? 2 : f > 0.22 ? 3 : 4, gch);
      }
    }
  }
});
