/* wall.js: the wallpaper runtime. Lays out one viewport per screen (or one spanning all),
   renders the chosen scene into each, and follows the state the tray host streams over SSE.
   Opened without a token (a plain browser), it runs standalone for previewing:
   wall.html?scene=koi-pond&detail=2&debug=1   (arrow keys switch scenes, C cycles the clock).

   Efficiency, in order of how much it saves:
     - nothing renders while every screen is covered by a maximised window, or paused
     - the backdrop is a half-size canvas refreshed ~15x/s; the browser scales it for free
     - the vignette and dim are CSS overlays, composited by the GPU, never redrawn
     - only the glyph layer is redrawn each frame, at the chosen frame rate */
(function () {
  var params = new URLSearchParams(location.search);
  var token = params.get('k') || '';
  var standalone = !token;
  var ROWS_BY_DETAIL = [44, 60, 80];
  var BG_SCALE = 0.5, BG_HZ = 15, FADE_MS = 1400;

  var state = {
    scene: 'ember', screens: [], mode: 'mirror', fps: 30, fpsCap: 0, detail: 1, speed: 1, dim: 0,
    tint: 'none', hue: 0, sat: 1,
    clock: 'off', clock24: false, clockDate: true, clockSize: 1, clockAll: false,
    paused: false, hold: false, covered: [], monitors: null
  };
  /* colour grades are one CSS filter on each screen: composited by the GPU, free for the scene code */
  var TINTS = {
    none: '',
    mono: 'grayscale(1)',
    phosphor: 'grayscale(1) sepia(1) saturate(5) hue-rotate(58deg)',
    amber: 'grayscale(1) sepia(1) saturate(3.2) hue-rotate(-8deg)',
    ice: 'grayscale(1) sepia(1) saturate(4) hue-rotate(150deg)',
    rose: 'grayscale(1) sepia(1) saturate(4) hue-rotate(295deg)'
  };
  if (standalone) {
    if (params.get('scene')) state.scene = params.get('scene');
    if (params.get('detail')) state.detail = +params.get('detail');
    if (params.get('fps')) state.fps = +params.get('fps');
    if (params.get('clock')) state.clock = params.get('clock');
    if (params.get('tint')) state.tint = params.get('tint');
    if (params.get('hue')) state.hue = +params.get('hue');
    if (params.get('t')) state.fixedT = +params.get('t');
  }

  var vps = [], simT = state.fixedT || 0, lastTick = 0, raf = 0, ms = 0, dbg = null;

  function opts(vp) {
    var showClock = state.clockAll || vp.index === 0;
    return {
      rows: ROWS_BY_DETAIL[clamp(state.detail | 0, 0, 2)],
      clock: showClock ? state.clock : 'off', clock24: state.clock24, clockDate: state.clockDate, clockSize: state.clockSize
    };
  }

  function slugFor(vp) {
    var s = state.mode === 'span' ? '' : (state.screens && state.screens[vp.mon]);
    return findScene(s) ? s : findScene(state.scene) ? state.scene : SCENES[0].slug;
  }

  function isActive(vp) {
    var c = state.covered || [];
    if (vp.mon < 0) return !(c.length && c.every(Boolean));
    return !c[vp.mon];
  }

  /* ---- layers: a scene is a bg canvas + a glyph canvas; a scene change stacks a new one and fades it in ---- */
  function makeLayer(vp, slug) {
    var el = document.createElement('div'); el.className = 'layer';
    var bg = document.createElement('canvas'), fg = document.createElement('canvas');
    bg.className = 'bg'; fg.className = 'fg';
    el.appendChild(bg); el.appendChild(fg);
    var sc = findScene(slug), bs = (sc && sc.bgScale) || BG_SCALE;
    bg.width = Math.max(2, Math.round(vp.w * bs)); bg.height = Math.max(2, Math.round(vp.h * bs));
    fg.width = vp.w; fg.height = vp.h;
    var L = { el: el, bg: bg, fg: fg, bgx: bg.getContext('2d', { alpha: false }), fgx: fg.getContext('2d'), slug: slug, bgT: -1e9 };
    vp.el.insertBefore(el, vp.vig);
    vp.layers.push(L);
    return L;
  }

  function renderLayer(vp, L) {
    var sc = findScene(L.slug);
    if (!sc) return;
    if (L.broken) return;
    var o = opts(vp);
    try {
      if (simT - L.bgT >= 1 / BG_HZ || L.bgT > simT) { drawBackdrop(L.bgx, sc, vp.w, vp.h, L.bg.width, L.bg.height, simT, o); L.bgT = simT; }
      drawField(L.fgx, sc, vp.w, vp.h, simT, o);
    } catch (err) {
      /* a faulty user scene must not take the wallpaper down: report once and stop drawing that layer */
      L.broken = true;
      console.error('scene "' + L.slug + '" failed:', err);
    }
  }

  function dropLayers(vp, keepLast) {
    var drop = keepLast ? vp.layers.slice(0, -1) : vp.layers.slice();
    drop.forEach(function (b) { b.el.remove(); var i = vp.layers.indexOf(b); if (i >= 0) vp.layers.splice(i, 1); });
  }

  function setScene(vp, slug) {
    if (vp.slug === slug && vp.layers.length) return;
    var first = !vp.layers.length;
    var L = makeLayer(vp, slug);
    vp.slug = slug;
    if (vp.w >= 8) renderLayer(vp, L);
    if (first) return;
    L.el.style.opacity = '0';
    void L.el.offsetWidth;
    L.el.style.transition = 'opacity ' + FADE_MS + 'ms ease';
    L.el.style.opacity = '1';
    setTimeout(function () { if (vp.layers.indexOf(L) >= 0) dropLayers(vp, true); }, FADE_MS + 100);
  }

  /* ---- layout: viewports from the monitor rectangles the host reports (physical pixels) ---- */
  function wantedRects() {
    var dpr = window.devicePixelRatio || 1, mons = state.monitors;
    if (!mons || !mons.length) mons = [{ x: 0, y: 0, w: Math.round(innerWidth * dpr), h: Math.round(innerHeight * dpr) }];
    if (state.mode === 'span' && mons.length > 1) {
      var x0 = 1e9, y0 = 1e9, x1 = -1e9, y1 = -1e9;
      mons.forEach(function (m) { x0 = Math.min(x0, m.x); y0 = Math.min(y0, m.y); x1 = Math.max(x1, m.x + m.w); y1 = Math.max(y1, m.y + m.h); });
      return [{ x: x0, y: y0, w: x1 - x0, h: y1 - y0, mon: -1 }];
    }
    return mons.map(function (m, i) { return { x: m.x, y: m.y, w: m.w, h: m.h, mon: i }; });
  }

  function makeVp(index) {
    var el = document.createElement('div'); el.className = 'vp';
    var vig = document.createElement('div'); vig.className = 'vig';
    var dim = document.createElement('div'); dim.className = 'dim';
    el.appendChild(vig); el.appendChild(dim);
    document.body.appendChild(el);
    return { el: el, vig: vig, dimEl: dim, layers: [], slug: '', index: index, mon: 0, x: -1, y: -1, w: 0, h: 0 };
  }

  function layout() {
    var rects = wantedRects(), dpr = window.devicePixelRatio || 1;
    while (vps.length > rects.length) { var old = vps.pop(); old.el.remove(); }
    rects.forEach(function (r, i) {
      var vp = vps[i] || (vps[i] = makeVp(i));
      vp.mon = r.mon; vp.index = i;
      if (vp.x !== r.x || vp.y !== r.y || vp.w !== r.w || vp.h !== r.h) {
        vp.x = r.x; vp.y = r.y; vp.w = r.w; vp.h = r.h;
        vp.el.style.left = (r.x / dpr) + 'px'; vp.el.style.top = (r.y / dpr) + 'px';
        vp.el.style.width = (r.w / dpr) + 'px'; vp.el.style.height = (r.h / dpr) + 'px';
        dropLayers(vp, false); vp.slug = '';
      }
      vp.dimEl.style.background = state.dim > 0 ? 'rgba(0,0,0,' + state.dim + ')' : 'transparent';
      var f = TINTS[state.tint] || '';
      if (!f && state.hue) f = 'hue-rotate(' + state.hue + 'deg)';
      if (state.sat !== 1) f += (f ? ' ' : '') + 'saturate(' + state.sat + ')';
      if (vp.filter !== f) { vp.filter = f; vp.el.style.filter = f; }
      setScene(vp, slugFor(vp));
    });
  }

  function drawAll(force) {
    var t0 = performance.now();
    for (var i = 0; i < vps.length; i++) {
      var vp = vps[i];
      if (!(force || isActive(vp)) || vp.w < 8) continue;
      for (var j = 0; j < vp.layers.length; j++) renderLayer(vp, vp.layers[j]);
    }
    ms = ms * 0.9 + (performance.now() - t0) * 0.1;
    if (dbg) dbg.textContent = vps.map(function (v) { return v.slug + ' ' + v.w + 'x' + v.h; }).join(' | ') + '   ' + ms.toFixed(1) + ' ms/frame   ' + state.fps + ' fps cap   t=' + simT.toFixed(1);
  }

  function frame(now) {
    raf = requestAnimationFrame(frame);
    var fps = state.fpsCap || state.fps;
    if (now - lastTick < 1000 / fps - 2) return;
    var dt = lastTick ? Math.min(0.25, (now - lastTick) / 1000) : 0;
    lastTick = now;
    if (state.fixedT === undefined) simT += dt * state.speed;
    drawAll(false);
  }

  /* run only while something is visible: paused, or every screen covered by a maximised window, costs nothing */
  function sync() {
    var should = !state.paused && !state.hold && vps.some(isActive);
    if (should && !raf) { lastTick = 0; raf = requestAnimationFrame(frame); }
    else if (!should && raf) { cancelAnimationFrame(raf); raf = 0; }
  }

  function apply(s) {
    for (var k in s) if (Object.prototype.hasOwnProperty.call(s, k)) state[k] = s[k];
    layout(); sync();
    if (!raf) drawAll(true);
  }

  function sceneList() {
    return SCENES.map(function (s) { return { slug: s.slug, name: s.name, accent: s.accent }; });
  }

  function connect() {
    var es = new EventSource('/api/events?role=wall&k=' + encodeURIComponent(token));
    es.onmessage = function (e) { try { apply(JSON.parse(e.data)); } catch (err) { /* ignore a bad frame */ } };
    fetch('/api/scenes?k=' + encodeURIComponent(token), { method: 'POST', body: JSON.stringify(sceneList()) }).catch(function () {});
  }

  window.addEventListener('resize', function () { layout(); drawAll(true); });
  window.__wall = { state: state, apply: apply, vps: vps };

  if (params.get('debug')) { dbg = document.getElementById('dbg'); dbg.style.display = 'block'; }
  layout(); sync(); drawAll(true);

  if (standalone) {
    window.addEventListener('keydown', function (e) {
      var i = SCENES.findIndex(function (s) { return s.slug === state.scene; });
      if (e.key === 'ArrowRight') apply({ scene: SCENES[(i + 1) % SCENES.length].slug });
      else if (e.key === 'ArrowLeft') apply({ scene: SCENES[(i + SCENES.length - 1) % SCENES.length].slug });
      else if (e.key === 'c') { var o = ['off', 'center', 'top-right', 'bottom-right', 'bottom-center']; apply({ clock: o[(o.indexOf(state.clock) + 1) % o.length] }); }
      else if (e.key === 'd') apply({ detail: (state.detail + 1) % 3 });
    });
  } else {
    connect();
  }
})();
