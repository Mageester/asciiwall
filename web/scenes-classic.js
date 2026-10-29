/* scenes-classic.js: the eight scenes from the Claude pane mod, unchanged.
   Classic script; shares the globals declared in engine.js. */
/* ================= 1. EMBER ================= */
SCENES.push({
  name: 'Ember', slug: 'ember', accent: '#ff7a1a',
  ramp: ' .·:+*#%@',
  colors: ['#ff3b1f', '#ff7a1a', '#ffb347', '#ffe6a8', '#8c2a18'],
  bg: function (t) {
    vgrad([[0, '#070405'], [0.55, '#150907'], [1, '#2b0f08']]);
    var p = 0.85 + 0.15 * Math.sin(t * 0.7);
    ctx.globalCompositeOperation = 'lighter';
    glow(W * 0.5, H * 1.02, H * 0.85, '255,90,30', 0.2 * p);
    glow(W * 0.25, H * 1.05, H * 0.5, '255,60,20', 0.12);
    glow(W * 0.76, H * 1.05, H * 0.5, '255,140,40', 0.1);
    ctx.globalCompositeOperation = 'source-over';
  },
  field: function (t) {
    var r0 = Math.floor(ROWS * 0.62), i, j, k;
    for (j = r0; j < ROWS; j++) {
      var f = (j - r0) / (ROWS - r0);
      for (i = 0; i < COLS; i++) {
        var n = Math.sin(i * 0.13 + t * 0.55 + Math.sin(j * 0.3 - t * 0.4) * 1.6) * Math.sin(j * 0.34 - t * 0.9 + i * 0.05);
        var v = (n - 0.15) * f * 0.9;
        if (v > 0.04) put(i, j, v * 0.75, f > 0.6 ? 0 : 4);
      }
    }
    var N = Math.round(COLS * 0.85);
    for (k = 0; k < N; k++) {
      var P = 7 + hash(k + 1) * 10, u = fract(t / P + hash(k * 3.1 + 2));
      var sw = (18 + hash(k + 9) * 34) * S;
      var x = hash(k * 7.7 + 4) * W + Math.sin(t * (0.35 + hash(k + 5) * 0.5) + k * 2.1) * sw * (0.4 + u) + u * u * 40 * S * (hash(k + 11) - 0.5) * 2;
      var y = H * 1.04 - u * H * 1.12;
      var env = sstep(0, 0.07, u) * (1 - sstep(0.5, 0.98, u));
      var fl = 0.78 + 0.22 * Math.sin(t * (3 + hash(k) * 3) + k * 5);
      var c = u < 0.18 ? 3 : u < 0.45 ? 2 : u < 0.72 ? 1 : 0;
      var big = hash(k * 2.3 + 8) > 0.82, v2 = env * fl * (big ? 1.1 : 0.8);
      splat(x, y, v2, c);
      if (big) { splat(x - CW, y, v2 * 0.25, 0); splat(x + CW, y, v2 * 0.25, 0); splat(x, y - CH, v2 * 0.25, 0); splat(x, y + CH, v2 * 0.25, 0); }
      for (var q = 1; q <= 3; q++) splat(x, y + q * CH * 0.9, v2 * (0.5 / q), c > 0 ? c - 1 : 0);
    }
  }
});

/* ================= 2. CLOUD DRIFT ================= */
var CLOUDS = [];
(function () {
  for (var k = 0; k < 9; k++) {
    var z = 0.45 + hash(k * 3 + 1) * 0.85, nb = 5 + Math.floor(hash(k + 7) * 3), span = 120 + hash(k + 2) * 140, bl = [];
    for (var b = 0; b < nb; b++) {
      var f = b / (nb - 1), e = 1 - Math.abs(f - 0.5) * 2;
      bl.push([(f - 0.5) * span, -e * 14 * hash(k * 5 + b + 1) + (hash(k + b * 3) - 0.5) * 10, 26 + e * 30 * (0.6 + hash(k * 9 + b) * 0.7)]);
    }
    CLOUDS.push({ z: z, y: 0.08 + hash(k * 5 + 4) * 0.62, x0: (k + hash(k * 11 + 6) * 0.6) / 9, sp: 5 + z * 13, bl: bl, span: span, ph: hash(k) * 6 });
  }
  CLOUDS.sort(function (a, b) { return a.z - b.z; });
})();
function drawCloud(c, t) {
  var sc = S * c.z * 1.25, pad = (c.span + 140) * sc, tot = W + 2 * pad;
  var cx = mod(c.x0 * tot + t * c.sp * S, tot) - pad, cy = c.y * H;
  var dim = 0.5 + 0.5 * clamp(c.z / 1.3, 0, 1), ext = (c.span * 0.5 + 80) * sc, vy = 90 * sc, bl = c.bl, n = bl.length;
  fill(cx - ext, cy - vy, cx + ext, cy + vy * 0.8, function (px, py) {
    var f = 0;
    for (var b = 0; b < n; b++) {
      var B = bl[b], r = B[2] * sc * (1 + 0.05 * Math.sin(t * 0.35 + c.ph + b * 1.7));
      var dx = px - (cx + B[0] * sc), dy = py - (cy + B[1] * sc);
      f += r * r / (dx * dx + dy * dy + r * r * 0.04);
    }
    var cv = sstep(0.7, 1.3, f);
    if (cv <= 0.03) return 0;
    var lit = clamp(0.5 - (py - cy) / (80 * sc), 0, 1);
    _c = lit > 0.62 ? 0 : lit > 0.32 ? 1 : lit > 0.1 ? 2 : 3;
    return cv * (0.34 + 0.66 * lit) * dim;
  });
}
SCENES.push({
  name: 'Cloud Drift', slug: 'cloud-drift', accent: '#ffd0a8',
  ramp: ' ·:+oO@',
  colors: ['#ffffff', '#fff1e6', '#d4e6fb', '#b4c6ee'],
  bg: function (t) {
    vgrad([[0, '#3f86d6'], [0.5, '#86bff0'], [0.82, '#d9e0f2'], [1, '#ffd6bd']]);
    ctx.globalCompositeOperation = 'lighter';
    glow(W * 0.82, H * 0.92, H * 0.9, '255,214,170', 0.35 + 0.05 * Math.sin(t * 0.3));
    ctx.globalCompositeOperation = 'source-over';
  },
  field: function (t) {
    var k, q;
    for (k = 0; k < 6; k++) {
      var len = 12 + Math.floor(hash(k + 40) * 22), xs = mod(hash(k + 41) * COLS * 2 + t * (1.2 + hash(k + 42) * 1.6), COLS + len * 2) - len;
      var j = Math.floor(hash(k + 43) * ROWS * 0.5);
      for (q = 0; q < len; q++) put(Math.floor(xs + q), j, 0.3 * Math.sin(Math.PI * q / len), 0, 2);
    }
    for (k = 0; k < CLOUDS.length; k++) drawCloud(CLOUDS[k], t);
  }
});

/* ================= 3. LAGOON ================= */
function drawTurtle(x, y, ang, sc, t, ph, vm) {
  var ca = Math.cos(ang), sa = Math.sin(ang), R = 84 * sc;
  var f = 0.5 + 0.5 * Math.sin(t * 1.5 + ph), psi = 0.35 + f * 0.75, sp = Math.sin(psi), cp = Math.cos(psi);
  var rp = 0.95 + (1 - f) * 0.25, sr = Math.sin(rp), cr = Math.cos(rp);
  var fx = 22 - 40 * sp, fy = 30 + 40 * cp, rx2 = -26 - 22 * sr, ry2 = 22 + 22 * cr;
  fill(x - R, y - R, x + R, y + R, function (px, py) {
    var dx = px - x, dy = py - y, lx = (dx * ca + dy * sa) / sc, ly = (-dx * sa + dy * ca) / sc, ay = Math.abs(ly);
    var dB = (Math.sqrt(lx * lx / 2116 + ly * ly / 1296) - 1) * 36;
    var dH = Math.hypot(lx - 57, ly) - 12;
    var dF = Math.min(segD(lx, ay, 22, 30, fx, fy) - 7, segD(lx, ay, -26, 22, rx2, ry2) - 5);
    var dT = segD(lx, ay, -44, 0, -56, 0) - 3;
    var dm = Math.min(dB, dH, dF, dT), cv = cov(dm * sc);
    if (cv <= 0.03) return 0;
    var v;
    if (dB <= dm + 0.001) {
      if (dB > -6) { _c = 4; v = 0.9; }
      else {
        var pat = Math.cos(lx * 0.19) * Math.cos(ly * 0.23);
        if (pat > 0.3) { _c = 2; v = 0.95; } else { _c = 1; v = 0.72 + 0.2 * pat; }
      }
    } else if (dH <= dm + 0.001) { _c = 5; v = 0.95; }
    else { _c = 3; v = 0.7; }
    return cv * v * vm;
  });
}
function turtlePath(k, t) {
  var a = t * (0.09 + 0.03 * hash(k)) + hash(k + 3) * 6.28, rx = W * (0.26 + 0.08 * hash(k + 1)), ry = H * (0.2 + 0.08 * hash(k + 2));
  return [W * (0.3 + 0.4 * hash(k + 8)) + rx * Math.cos(a) * 0.9, H * (0.35 + 0.3 * hash(k + 9)) + ry * Math.sin(a * 2) * 0.7];
}
SCENES.push({
  name: 'Lagoon', slug: 'lagoon', accent: '#5fe3cf',
  ramp: ' .·:+*oO@',
  colors: ['#d8fff0', '#3aa86a', '#17623f', '#a8e8b8', '#e2cf8f', '#c8f0a0', '#e9fffb', '#7fe3d4'],
  bg: function (t) {
    vgrad([[0, '#0a5f72'], [0.5, '#10959b'], [1, '#27c4b4']]);
    ctx.globalCompositeOperation = 'lighter';
    glow(W * (0.55 + 0.05 * Math.sin(t * 0.2)), -H * 0.1, H * 1.2, '200,255,240', 0.18);
    ctx.globalCompositeOperation = 'source-over';
  },
  field: function (t) {
    var i, j, k;
    for (j = 0; j < ROWS; j++) for (i = 0; i < COLS; i++) {
      var x = i * CW, y = j * CH;
      var c = Math.sin(x * 0.02 + t * 0.5 + Math.sin(y * 0.03 + t * 0.4) * 2) + Math.sin(y * 0.025 - t * 0.35 + Math.sin(x * 0.027 + t * 0.3) * 2);
      var q = (c + 2) / 4 - 0.58;
      if (q > 0) { var v = Math.pow(q / 0.42, 1.6) * 0.75; put(i, j, v, v > 0.5 ? 0 : 7); }
    }
    for (k = 0; k < 3; k++) {
      var gx = W * (0.5 + 0.35 * Math.sin(t * 0.07 + k * 2.1)), gy = H * (0.3 + 0.4 * hash(k + 60)) + Math.sin(t * 0.11 + k) * H * 0.12;
      for (var f = 0; f < 9; f++) {
        var a = hash(k * 20 + f) * 6.28 + t * 0.2, rr = (20 + hash(k * 30 + f) * 50) * S;
        splat(gx + Math.cos(a + f) * rr, gy + Math.sin(a * 1.3 + f) * rr * 0.6, 0.45, 6);
      }
    }
    var sz = [1.55, 0.7, 1.05], ks = [1, 4, 7];
    for (k = 0; k < 3; k++) {
      var p0 = turtlePath(ks[k], t), p1 = turtlePath(ks[k], t + 0.4);
      drawTurtle(p0[0], p0[1], Math.atan2(p1[1] - p0[1], p1[0] - p0[0]), S * sz[k], t, k * 1.7, k === 1 ? 0.7 : 1);
    }
  }
});

/* ================= 4. KOI POND ================= */
var KOI = [
  { pat: [0, 0, 1, 0, 0, 1, 0, 0, 1, 0], ax: 0.32, ay: 0.27, w1: 0.15, w2: 0.2, p1: 0.4, p2: 1.3, sc: 1.25 },
  { pat: [1, 1, 0, 1, 3, 1, 1, 0, 1, 1], ax: 0.36, ay: 0.22, w1: 0.12, w2: 0.17, p1: 2.4, p2: 0.2, sc: 1.1 },
  { pat: [2, 2, 2, 2, 2, 0, 2, 2, 2, 2], ax: 0.3, ay: 0.3, w1: 0.17, w2: 0.13, p1: 4.1, p2: 2.7, sc: 1.0 },
  { pat: [3, 0, 3, 1, 0, 1, 3, 0, 1, 1], ax: 0.38, ay: 0.25, w1: 0.11, w2: 0.16, p1: 5.2, p2: 3.9, sc: 1.2 },
  { pat: [0, 0, 0, 0, 0, 0, 0, 0, 0, 0], ax: 0.27, ay: 0.31, w1: 0.14, w2: 0.19, p1: 1.6, p2: 5.1, sc: 0.9 }
];
var KRAD = [9.5, 10.5, 10.5, 10, 9, 7.8, 6.6, 5.4, 4.4, 3.6];
var PADS = [[0.12, 0.2, 0.9], [0.3, 0.78, 1.1], [0.55, 0.18, 0.8], [0.8, 0.72, 1.0], [0.9, 0.3, 0.7], [0.45, 0.55, 0.6], [0.15, 0.6, 0.7]];
function koiP(K, t) {
  return [W * 0.5 + W * K.ax * Math.sin(K.w1 * t + K.p1) + W * 0.05 * Math.sin(K.w2 * 2.3 * t + K.p2),
          H * 0.5 + H * K.ay * Math.sin(K.w2 * t + K.p2) + H * 0.04 * Math.sin(K.w1 * 1.7 * t + K.p1)];
}
function drawKoi(K, t, idx) {
  var seg = [], k, s = K.sc * S;
  for (k = 0; k < 10; k++) {
    var tau = t - k * 0.15, p = koiP(K, tau), p2 = koiP(K, tau + 0.06);
    var tx = p2[0] - p[0], ty = p2[1] - p[1], L = Math.hypot(tx, ty) || 1;
    var wig = Math.sin(t * 3.1 - k * 0.65 + idx) * (0.6 + k * 0.9) * s;
    seg.push([p[0] - ty / L * wig, p[1] + tx / L * wig, KRAD[k] * s]);
  }
  var e = seg[9], d = seg[8], bx = e[0] - d[0], by = e[1] - d[1], bl = Math.hypot(bx, by) || 1;
  bx /= bl; by /= bl;
  var tc = K.pat[9], fan = Math.sin(t * 2.6 + idx) * 0.25;
  for (var sd = -1; sd <= 1; sd += 2) {
    var ca = Math.cos(sd * 0.45 + fan), sa = Math.sin(sd * 0.45 + fan);
    var ex = e[0] + (bx * ca - by * sa) * 22 * s, ey = e[1] + (bx * sa + by * ca) * 22 * s;
    (function (ex2, ey2) {
      fill(Math.min(e[0], ex2) - 12, Math.min(e[1], ey2) - 12, Math.max(e[0], ex2) + 12, Math.max(e[1], ey2) + 12, function (px, py) {
        _c = tc; return cov(segD(px, py, e[0], e[1], ex2, ey2) - 2.5 * s) * 0.55;
      });
    })(ex, ey);
  }
  var a2 = seg[1], b2 = seg[3], tx2 = b2[0] - a2[0], ty2 = b2[1] - a2[1], tl = Math.hypot(tx2, ty2) || 1;
  var nx = -ty2 / tl, ny = tx2 / tl, fl = 0.7 + 0.5 * (0.5 + 0.5 * Math.sin(t * 2.4 + idx * 2)), s2 = seg[2];
  for (var sg = -1; sg <= 1; sg += 2) {
    (function (sg2) {
      var sx = s2[0] + nx * sg2 * s2[2] * 0.8, sy = s2[1] + ny * sg2 * s2[2] * 0.8;
      var ex3 = sx + (nx * sg2 * 0.7 - tx2 / tl * 0.7) * 11 * s * fl, ey3 = sy + (ny * sg2 * 0.7 - ty2 / tl * 0.7) * 11 * s * fl;
      fill(Math.min(sx, ex3) - 8, Math.min(sy, ey3) - 8, Math.max(sx, ex3) + 8, Math.max(sy, ey3) + 8, function (px, py) {
        _c = K.pat[2]; return cov(segD(px, py, sx, sy, ex3, ey3) - 2 * s) * 0.55;
      });
    })(sg);
  }
  for (k = 9; k >= 0; k--) {
    (function (S0, c0) {
      fill(S0[0] - S0[2] - SOFT, S0[1] - S0[2] - SOFT, S0[0] + S0[2] + SOFT, S0[1] + S0[2] + SOFT, function (px, py) {
        var dd = Math.hypot(px - S0[0], py - S0[1]) - S0[2];
        _c = c0; return cov(dd) * (0.62 + 0.38 * clamp(-dd / S0[2], 0, 1));
      });
    })(seg[k], K.pat[k]);
  }
}
function drawPad(m, t, k) {
  var x = W * m[0] + Math.sin(t * 0.12 + k * 2) * 8 * S, y = H * m[1] + Math.cos(t * 0.1 + k) * 6 * S;
  var r = 34 * S * m[2], a0 = k * 1.9 + t * 0.03;
  fill(x - r - SOFT, y - r - SOFT, x + r + SOFT, y + r + SOFT, function (px, py) {
    var dx = px - x, dy = py - y, dist = Math.hypot(dx, dy), d = dist - r;
    var cv = cov(d);
    if (cv <= 0.03) return 0;
    var an = Math.atan2(dy, dx) - a0;
    an = Math.atan2(Math.sin(an), Math.cos(an));
    if (Math.abs(an) < 0.2 && dist > r * 0.1) return 0;
    _c = (d > -5) ? 5 : (k % 2 ? 4 : 5);
    return cv * (0.5 + 0.2 * Math.cos(an * 9));
  }, true);
  if (k === 1 || k === 3 || k === 5) {
    for (var q = 0; q < 8; q++) {
      var aa = q / 8 * 6.283 + t * 0.05;
      splat(x + Math.cos(aa) * r * 0.3, y + Math.sin(aa) * r * 0.3 * 0.7, 0.95, 7);
    }
    splat(x, y, 0.95, 2);
  }
}
SCENES.push({
  name: 'Koi Pond', slug: 'koi-pond', accent: '#ff8a4a',
  ramp: ' .·:oO@',
  colors: ['#ff6a2b', '#fff3df', '#ffb02e', '#6b6f88', '#4fbf7a', '#2e8f58', '#bfe9ff', '#ff9db8'],
  bg: function (t) {
    vgrad([[0, '#06211f'], [0.55, '#0b3a33'], [1, '#0f4d3f']]);
    ctx.globalCompositeOperation = 'lighter';
    glow(W * 0.3, H * 0.2, H * 0.8, '90,200,160', 0.08 + 0.02 * Math.sin(t * 0.25));
    ctx.globalCompositeOperation = 'source-over';
  },
  field: function (t) {
    var m, k;
    for (m = 0; m < 6; m++) {
      (function (m2) {
        var per = 5 + hash(m2 + 3) * 4, cyc = t / per + hash(m2) * 3, idx = Math.floor(cyc), age = fract(cyc) * per;
        var R = age * 40 * S, fade = Math.max(0, 1 - age / (per * 0.8));
        if (fade <= 0) return;
        var cx = W * (0.1 + 0.8 * hash(m2 * 13 + idx)), cy = H * (0.12 + 0.76 * hash(m2 * 17 + idx + 1)), ext = R + SOFT * 3;
        fill(cx - ext, cy - ext, cx + ext, cy + ext, function (px, py) {
          var dist = Math.hypot(px - cx, py - cy), d1 = dist - R, d2 = dist - R * 0.62;
          var g = Math.exp(-d1 * d1 / (SOFT * SOFT * 1.4)) + 0.5 * Math.exp(-d2 * d2 / (SOFT * SOFT * 1.4));
          _c = 6; return g * fade * 0.7;
        });
      })(m);
    }
    for (k = 0; k < KOI.length; k++) drawKoi(KOI[k], t, k);
    for (k = 0; k < PADS.length; k++) drawPad(PADS[k], t, k);
  }
});

/* ================= 5. JELLYFISH ================= */
var JELLY = [
  { x: 0.2, y: 0.42, R: 70, sp: 0.9, pal: 0, ph: 0.0 }, { x: 0.72, y: 0.55, R: 96, sp: 0.75, pal: 3, ph: 1.7 },
  { x: 0.46, y: 0.3, R: 48, sp: 1.05, pal: 6, ph: 3.1 }, { x: 0.88, y: 0.25, R: 42, sp: 1.1, pal: 9, ph: 4.2 },
  { x: 0.05, y: 0.8, R: 56, sp: 0.95, pal: 0, ph: 5.0 }
];
function drawJelly(J, t) {
  var Rb = J.R * S, phi = t * J.sp * 1.8 + J.ph, c = 0.5 + 0.5 * Math.sin(phi);
  var x = W * J.x + Math.sin(t * 0.17 + J.ph * 1.3) * W * 0.07;
  var y = H * J.y + Math.sin(t * 0.23 + J.ph) * H * 0.12 - Rb * 0.12 * Math.sin(phi);
  var ang = Math.sin(t * 0.3 + J.ph) * 0.1, ca = Math.cos(ang), sa = Math.sin(ang);
  var rx = Rb * (1 - 0.17 * c), ry = Rb * (0.72 + 0.2 * c), rimY = Rb * 0.1;
  var body = J.pal, rim = J.pal + 1, tent = J.pal + 2, k, s;
  var L = Rb * (1.7 + 0.4 * Math.sin(J.ph)), nT = 7;
  for (k = 0; k < nT; k++) {
    var u = (k / (nT - 1) - 0.5) * 2 * rx * 0.82, cnt = Math.floor(L / (CH * 0.7));
    for (s = 0; s < cnt; s++) {
      var f = s / cnt, lx = u + Math.sin(f * 6.5 - t * 2.2 + k * 1.3) * Rb * 0.14 * f * (1 + 0.6 * c), ly = rimY + f * L * (1 - 0.1 * c);
      splat(x + lx * ca - ly * sa, y + lx * sa + ly * ca, 0.85 * (1 - f * 0.85), tent);
    }
  }
  for (k = 0; k < 3; k++) {
    var u2 = (k - 1) * rx * 0.22, cnt2 = Math.floor(L * 0.55 / (CH * 0.7));
    for (s = 0; s < cnt2; s++) {
      var f2 = s / cnt2, lx2 = u2 + Math.sin(f2 * 8 - t * 2.8 + k) * Rb * 0.1 * f2, ly2 = rimY + f2 * L * 0.55;
      splat(x + lx2 * ca - ly2 * sa, y + lx2 * sa + ly2 * ca, 0.9 * (1 - f2 * 0.6), rim);
    }
  }
  fill(x - Rb * 1.3, y - Rb * 1.1, x + Rb * 1.3, y + Rb * 0.5, function (px, py) {
    var dx = px - x, dy = py - y, lx = dx * ca + dy * sa, ly = -dx * sa + dy * ca;
    var e = (Math.sqrt(lx * lx / (rx * rx) + ly * ly / (ry * ry)) - 1) * Math.min(rx, ry);
    var d = Math.max(e, ly - rimY), cv = cov(d);
    if (cv <= 0.03) return 0;
    var inner = -d;
    if (inner < SOFT * 0.9) { _c = rim; return cv * 0.95; }
    var ribs = 0.5 + 0.5 * Math.cos(Math.atan2(ly, lx) * 9 + J.ph);
    _c = body; return cv * (0.22 + 0.2 * ribs + 0.18 * (1 - clamp(inner / ry, 0, 1)));
  });
}
SCENES.push({
  name: 'Jellyfish', slug: 'jellyfish', accent: '#ff8ec4',
  ramp: ' .·:+*#%@',
  colors: ['#ff5fa8', '#ffc2e2', '#ff8ec4', '#8c6bff', '#d4c7ff', '#a891ff', '#33d4ff', '#b8f2ff', '#6fe0ff', '#ff9a5a', '#ffd9b8', '#ffb98a', '#9fd0ff', '#3c78d8'],
  bg: function (t) {
    vgrad([[0, '#02081c'], [0.5, '#061538'], [1, '#0a2a52']]);
    ctx.globalCompositeOperation = 'lighter';
    glow(W * 0.5, -H * 0.1, H * 1.1, '70,130,230', 0.14 + 0.03 * Math.sin(t * 0.3));
    ctx.globalCompositeOperation = 'source-over';
  },
  field: function (t) {
    var i, j, k, rowsTop = Math.floor(ROWS * 0.75);
    for (j = 0; j < rowsTop; j++) for (i = 0; i < COLS; i++) {
      var a = Math.sin(i * CW * 0.0075 + 1.3 + Math.sin(t * 0.09) * 0.9) * Math.sin(i * CW * 0.0041 - t * 0.04 + 0.6);
      a = (a - 0.35) / 0.65;
      if (a > 0) { var v = a * 0.26 * (1 - j / ROWS); if (v > 0.04) put(i, j, v, 13); }
    }
    for (k = 0; k < 70; k++) {
      var x = mod(hash(k) * W + t * (3 + hash(k + 1) * 6) * S + Math.sin(t * 0.3 + k) * 14, W);
      var y = mod(hash(k + 2) * H - t * (4 + hash(k + 3) * 8) * S, H);
      put(Math.floor(x / CW), Math.floor(y / CH), 0.25 + 0.35 * (0.5 + 0.5 * Math.sin(t * 1.3 + k)), 12, hash(k + 9) > 0.9 ? 9 : 8);
    }
    for (k = 0; k < JELLY.length; k++) drawJelly(JELLY[k], t);
  }
});

/* ================= 6. PAPER BOATS ================= */
var PB = { n: 7, boats: [
  { x0: 0.2, sp: 11, L: 3, sc: 1.15, pal: 0 }, { x0: 0.62, sp: 8, L: 2, sc: 0.8, pal: 2 }, { x0: 0.9, sp: 14, L: 4, sc: 1.45, pal: 4 }
] };
function hy() { return H * 0.46; }
function waveY(x, t, L) {
  var yb = hy() + (H - hy()) * Math.pow((L + 0.6) / PB.n, 1.35), amp = (2 + L * 2.6) * S, kk = 0.012 - 0.0007 * L, sp = 0.5 + L * 0.12;
  return yb + amp * (Math.sin(x * kk + t * sp + L * 1.7) + 0.5 * Math.sin(x * kk * 2.1 - t * sp * 1.3 + L));
}
function drawWave(L, t, crestOnly) {
  var sunX = W * 0.74, c = L < 2 ? 8 : L < 4 ? 7 : 6;
  for (var i = 0; i < COLS; i++) {
    var px = (i + 0.5) * CW, yv = waveY(px, t, L), fr = yv / CH - 0.5, j = Math.floor(fr), a = fr - j;
    var cc = Math.abs(px - sunX) < 90 * S && L < 4 ? 9 : c;
    over(i, j, 0.25 + 0.7 * (1 - a), cc, 1); over(i, j + 1, 0.25 + 0.7 * a, cc, 1);
    if (!crestOnly) for (var r = 2; r <= 6; r++) {
      var nv = 0.2 * (1 - r / 7) * (0.6 + 0.4 * Math.sin(i * 0.4 + t * 0.8 + r));
      if (nv > 0.04) over(i, j + r, nv, L < 3 ? 8 : 7);
    }
  }
}
var HULL = [[-35, -6], [35, -6], [23, 9], [-23, 9]];
var SAILL = [[-30, -6], [0, -52], [0, -6]];
var SAILR = [[0, -52], [30, -6], [0, -6]];
function drawBoat(b, t) {
  var pad = 90 * S * b.sc, tot = W + 2 * pad, x = mod(b.x0 * tot + t * b.sp * S, tot) - pad;
  var y = waveY(x, t, b.L), ang = Math.atan2(waveY(x + 8, t, b.L) - waveY(x - 8, t, b.L), 16) * 0.8;
  var sc = S * b.sc * 1.2, ca = Math.cos(ang), sa = Math.sin(ang), R = 64 * sc;
  fill(x - R, y - R, x + R, y + R * 0.4, function (px, py) {
    var dx = px - x, dy = py - y, lx = (dx * ca + dy * sa) / sc, ly = (-dx * sa + dy * ca) / sc;
    var dl = polyD(lx, ly, SAILL), dr = polyD(lx, ly, SAILR), dh = polyD(lx, ly, HULL), dm = Math.min(dl, dr, dh);
    var cv = cov(dm * sc);
    if (cv <= 0.03) return 0;
    if (dm === dl) { _c = b.pal; return cv * 0.95; }
    if (dm === dr) { _c = b.pal + 1; return cv * 0.7; }
    if (lx < 0) { _c = b.pal; return cv * 0.85; }
    _c = b.pal + 1; return cv * 0.6;
  }, true);
}
SCENES.push({
  name: 'Paper Boats', slug: 'paper-boats', accent: '#ff9272',
  ramp: ' .·:+oO@',
  colors: ['#fffaf0', '#e6d2b8', '#ff9272', '#d96a50', '#ffe08e', '#e6b95a', '#effcff', '#a6e6f0', '#58b3c8', '#fff0b8', '#8a5d6b'],
  bg: function (t) {
    var h = hy() / H;
    vgrad([[0, '#fbe6d0'], [h * 0.9, '#ffd1b0'], [h, '#ffc4a0'], [h + 0.01, '#58b8c8'], [1, '#1e6f8a']]);
    ctx.globalCompositeOperation = 'lighter';
    glow(W * 0.74, hy() - 30 * S, H * 0.55, '255,226,170', 0.4 + 0.05 * Math.sin(t * 0.4));
    ctx.globalCompositeOperation = 'source-over';
  },
  field: function (t) {
    var k, L, b;
    fill(W * 0.74 - 50 * S, hy() - 90 * S, W * 0.74 + 50 * S, hy() + 10, function (px, py) {
      _c = 9; return cov(Math.hypot(px - W * 0.74, py - (hy() - 34 * S)) - 34 * S) * 0.95;
    });
    for (k = 0; k < 3; k++) {
      var bx = mod(t * 14 * S + k * 300, W + 100) - 50, by = H * 0.2 + Math.sin(t * 0.5 + k) * 18 + k * 20;
      put(Math.floor(bx / CW), Math.floor(by / CH), 0.8, 10, Math.sin(t * 6 + k) > 0 ? 6 : 11);
    }
    for (L = 0; L < PB.n; L++) {
      drawWave(L, t, false);
      for (b = 0; b < PB.boats.length; b++) if (PB.boats[b].L === L) { drawBoat(PB.boats[b], t); drawWave(L, t, true); }
    }
  }
});

/* ================= 7. FIREFLIES ================= */
function ffPos(k, t) {
  var a = hash(k * 3.3 + 1), b = hash(k * 5.1 + 2), c = hash(k * 7.7 + 3), d = hash(k * 2.9 + 4);
  return [W * (0.06 + 0.88 * a) + Math.sin(t * (0.12 + 0.1 * b) + k * 2.3) * W * 0.07 + Math.sin(t * (0.31 + 0.12 * c) + k) * W * 0.02,
          H * (0.28 + 0.58 * b) + Math.sin(t * (0.1 + 0.09 * d) + k * 1.7) * H * 0.07 + Math.cos(t * (0.27 + 0.1 * a) + k * 0.7) * H * 0.02];
}
function ffGlow(k, t) {
  var per = 2.6 + hash(k + 20) * 3.2, b = Math.max(0, Math.sin(6.2832 * (t / per + hash(k + 30))));
  return 0.05 + 0.95 * Math.pow(b, 2.2);
}
SCENES.push({
  name: 'Fireflies', slug: 'fireflies', accent: '#e8ff7a',
  ramp: ' .·:+*oO@',
  colors: ['#e8ff7a', '#9fdc5a', '#fffbc4', '#12402a', '#1c5c3a', '#2e8a55', '#8a6a3a'],
  bg: function (t) {
    vgrad([[0, '#020605'], [0.6, '#061510'], [1, '#0a2318']]);
    ctx.globalCompositeOperation = 'lighter';
    glow(W * 0.5, H * 1.05, H * 0.7, '30,120,90', 0.22);
    for (var k = 0; k < 34; k++) {
      var g = ffGlow(k, t);
      if (g > 0.08) { var p = ffPos(k, t); glow(p[0], p[1], 70 * S, '210,255,110', g * 0.16); }
    }
    ctx.globalCompositeOperation = 'source-over';
  },
  field: function (t) {
    var i, j, k, q, r;
    for (i = 0; i < COLS; i++) {
      var hgt = 2 + Math.floor(hash(i * 1.31) * 5), reed = i % 17 === 5, hh = reed ? 8 + Math.floor(hash(i * 2.7) * 8) : hgt;
      for (r = 0; r < hh; r++) {
        var sway = Math.sin(t * 0.8 + i * 0.19 + Math.sin(t * 0.23 + i * 0.05) * 1.7) * (r / hh);
        var g = sway > 0.3 ? 4 : sway < -0.3 ? 5 : 3, f = r / hh;
        if (reed && r >= hh - 2) over(i, ROWS - 1 - r, 0.9, 6, 12);
        else over(i, ROWS - 1 - r, 0.5 + 0.35 * f, reed ? 4 : f > 0.6 ? 5 : f > 0.3 ? 4 : 3, g);
      }
    }
    for (k = 0; k < 30; k++) {
      var mx = mod(hash(k + 70) * W + t * (2 + hash(k + 71) * 4) * S + Math.sin(t * 0.4 + k) * 20, W);
      put(Math.floor(mx / CW), Math.floor((H * (0.3 + 0.55 * hash(k + 72)) + Math.sin(t * 0.3 + k) * 18) / CH), 0.2, 1, 8);
    }
    for (k = 0; k < 34; k++) {
      var gl2 = ffGlow(k, t), p = ffPos(k, t);
      if (gl2 <= 0.07) continue;
      var ci = Math.floor(p[0] / CW), cj = Math.floor(p[1] / CH);
      for (j = -2; j <= 2; j++) for (i = -2; i <= 2; i++) {
        var dx = (ci + i + 0.5) * CW - p[0], dy = (cj + j + 0.5) * CH - p[1], dd = Math.hypot(dx, dy) / (CW * 1.6);
        var f2 = Math.exp(-dd * dd) * gl2 * 0.7;
        put(ci + i, cj + j, f2, f2 > 0.35 ? 0 : 1);
      }
      splat(p[0], p[1], gl2 * 1.3, 2);
      for (q = 1; q <= 5; q++) { var pq = ffPos(k, t - 0.18 * q); splat(pq[0], pq[1], gl2 * 0.22 * (1 - q / 5), 1); }
    }
  }
});

/* ================= 8. MOONLIT ================= */
function moonXY() { return [W * 0.74, H * 0.27, Math.min(64 * S, Math.min(W, H) * 0.12)]; }
var MCL = [];
(function () {
  for (var k = 0; k < 4; k++) {
    var bl = [], nb = 5;
    for (var b = 0; b < nb; b++) bl.push([(b / (nb - 1) - 0.5) * 360, (hash(k * 9 + b) - 0.5) * 20, 70 + hash(k * 5 + b) * 50, 14 + hash(k * 3 + b) * 12]);
    MCL.push({ y: 0.14 + hash(k + 90) * 0.36, x0: hash(k + 91), sp: 5 + hash(k + 92) * 6, bl: bl });
  }
})();
SCENES.push({
  name: 'Moonlit', slug: 'moonlit', accent: '#aab8ff',
  ramp: ' .·:+*#%@',
  colors: ['#cfd9ff', '#ffe7bd', '#5a6fc0', '#8396d9', '#dfe6ff', '#f6efcf', '#b8c6ff'],
  bg: function (t) {
    vgrad([[0, '#050a22'], [0.55, '#0b1a4a'], [0.85, '#1d2f6e'], [1, '#243a80']]);
    var m = moonXY();
    ctx.globalCompositeOperation = 'lighter';
    glow(m[0], m[1], m[2] * 4 * (1 + 0.04 * Math.sin(t * 0.5)), '160,185,255', 0.28);
    ctx.globalCompositeOperation = 'source-over';
    var cols = ['#101a47', '#0a1236', '#050a22'];
    for (var k = 0; k < 3; k++) {
      ctx.beginPath(); ctx.moveTo(0, H);
      for (var x = 0; x <= W + 8; x += 8) ctx.lineTo(x, H * (0.76 + 0.06 * k) - (26 - k * 6) * S * (Math.sin(x * 0.004 * (1 + 0.3 * k) + k * 2) + 0.5 * Math.sin(x * 0.011 + k)));
      ctx.lineTo(W, H); ctx.closePath(); ctx.fillStyle = cols[k]; ctx.fill();
    }
  },
  field: function (t) {
    var m = moonXY(), mx = m[0], my = m[1], R = m[2], k, q, i, j;
    for (k = 0; k < 140; k++) {
      var sx = Math.floor(hash(k + 1) * COLS), sy = Math.floor(hash(k + 2) * ROWS * 0.66), tw = 0.5 + 0.5 * Math.sin(t * (0.6 + hash(k + 3) * 1.4) + hash(k + 4) * 6);
      put(sx, sy, 0.25 + 0.55 * tw, hash(k + 5) > 0.8 ? 1 : 0, hash(k + 6) > 0.93 ? 9 : 8);
    }
    fill(mx - R * 3.2, my - R * 3.2, mx + R * 3.2, my + R * 3.2, function (px, py) {
      var dist = Math.hypot(px - mx, py - my);
      if (dist < R * 1.15) return 0;
      _c = 6; return 0.2 * Math.exp(-(dist - R * 1.2) / (R * 0.9)) * (0.85 + 0.15 * Math.sin(t * 0.6 + dist * 0.05));
    });
    var tilt = Math.sin(t * 0.05) * 0.15, cx2 = mx + Math.cos(tilt) * 0.42 * R, cy2 = my - 0.12 * R + Math.sin(tilt) * 0.42 * R;
    fill(mx - R - SOFT, my - R - SOFT, mx + R + SOFT, my + R + SOFT, function (px, py) {
      var d1 = Math.hypot(px - mx, py - my) - R, d2 = Math.hypot(px - cx2, py - cy2) - R * 0.92, d = Math.max(d1, -d2);
      if (cov(d) > 0.03) { _c = 5; return cov(d) * (0.8 + 0.2 * Math.sin(py * 0.05 + t * 0.4)); }
      _c = 2; return cov(d1) * 0.1;
    });
    for (k = 0; k < MCL.length; k++) {
      (function (c) {
        var tot = W + 600, cx = mod(c.x0 * tot + t * c.sp * S, tot) - 300, cy = c.y * H, bright;
        fill(cx - 260 * S, cy - 90 * S, cx + 260 * S, cy + 90 * S, function (px, py) {
          var f = 0;
          for (var b = 0; b < c.bl.length; b++) {
            var B = c.bl[b], dx = (px - (cx + B[0] * S)) / (B[2] * S), dy = (py - (cy + B[1] * S)) / (B[3] * S);
            f += 1 / (1 + dx * dx + dy * dy);
          }
          var cv = sstep(0.55, 1.0, f);
          if (cv <= 0.03) return 0;
          bright = 1 / (1 + Math.hypot(px - mx, py - my) / (R * 5));
          _c = bright > 0.3 ? 4 : 3;
          return cv * (0.14 + 0.5 * bright);
        });
      })(MCL[k]);
    }
    var cyc = t / 9, idx = Math.floor(cyc), ph = fract(cyc);
    if (ph < 0.1 && hash(idx) > 0.25) {
      var p = ph / 0.1, sx0 = W * (0.1 + 0.6 * hash(idx)), sy0 = H * (0.05 + 0.25 * hash(idx + 3)), dl = Math.hypot(1, 0.45);
      var hx = sx0 + p * W * 0.35, hyy = sy0 + p * W * 0.35 * 0.45;
      for (q = 0; q < 14; q++) splat(hx - q * CW * 0.9 / dl, hyy - q * CW * 0.9 * 0.45 / dl, (1 - q / 14) * (1 - p * 0.4), q === 0 ? 0 : 6);
    }
  }
});
