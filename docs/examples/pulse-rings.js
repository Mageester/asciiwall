/* Pulse Rings: a complete example scene. Copy this file into
     %LOCALAPPDATA%\AsciiWall\scenes\
   and choose "Pulse Rings" from the tray menu (restart the wallpaper from the tray if it is not listed). */
SCENES.push({
  name: 'Pulse Rings',
  slug: 'pulse-rings',          // lowercase letters, digits and dashes
  accent: '#ff7ac8',            // the swatch colour in the tray menu and settings
  ramp: ' .:-=+*#%@',           // characters from faint (left) to bright (right)
  colors: ['#ff7ac8', '#7ad7ff'], // palette: field() picks one by setting _c to 0 or 1

  // Backdrop: painted on a small canvas a few times a second and scaled up. W x H are in grid units.
  bg: function (t) {
    vgrad([[0, '#14061f'], [1, '#05030f']]);
    glow(W * 0.5, H * 0.5, H * 0.75, '255,122,200', 0.1 + 0.04 * Math.sin(t * 0.8));
  },

  // Glyph layer: called every frame. Return a brightness 0..1 for the point (x, y), and say which
  // palette colour it uses with _c. fill() visits every cell the callback can touch.
  field: function (t) {
    var cx = W / 2, cy = H / 2, spacing = 70 * S;
    fill(0, 0, W, H, function (x, y) {
      var d = Math.hypot(x - cx, y - cy) / spacing - t * 0.22;   // distance from the middle, drifting outwards
      var ring = Math.floor(d);                                  // which ring this point is on
      var f = d - ring;                                          // 0..1 across that ring
      _c = ring & 1;                                             // alternate the two colours
      return Math.pow(1 - Math.abs(f - 0.5) * 2, 2.4);           // bright in the middle of each ring
    });
  }
});
