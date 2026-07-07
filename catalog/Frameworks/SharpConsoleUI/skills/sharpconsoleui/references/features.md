# SharpConsoleUI visual features

Gradients, transparency/compositing, animations, desktop background, and
image/video/syntax rendering. Exact API names are as they appear in the docs.

## Gradients

Built on `ColorGradient`. Works in text (markup) and window/desktop backgrounds.

- Markup tag: `[gradient=name]`, `[gradient=c1->c2]`, `[gradient=c1->c2->c3]` (separator `->` or `→`; nested `[bold]`/`[underline]` preserved).
- `ColorGradient.Predefined["spectrum"|"warm"|"cool"]`; `ColorGradient.FromColors(Color, Color, …)`; `ColorGradient.Parse("red->blue" | "#FF0000->#0000FF" | "spectrum")`; `.Interpolate(t)` for a single sampled color.
- `GradientDirection`: `Horizontal`, `Vertical`, `DiagonalDown`, `DiagonalUp`.
- Window backgrounds: `WindowBuilder.WithBackgroundGradient(gradient, direction)` or `window.BackgroundGradient = new GradientBackground(gradient, direction)` (set `null` to remove; painted via `PreBufferPaint`, controls paint on top).
- Direct buffer fill: `buffer.FillGradient(LayoutRect, gradient, direction)`.
- Gradients can fade to transparent — `ColorGradient.BlendColors` interpolates all 4 RGBA channels: `ColorGradient.FromColors(Color.Cyan, Color.FromArgb(0,0,255,255))`.

```csharp
new WindowBuilder(ws)
    .WithBackgroundGradient(ColorGradient.FromColors(Color.DarkBlue, Color.Black), GradientDirection.Vertical)
    .Build();
```

## Alpha blending and transparency

Per-cell Porter-Duff "over" compositing; the result is always opaque (A=255).

- Colors: `new Color(r,g,b,a)`, `Color.Transparent` (A=0), `Color.Red.WithAlpha(128)`; `Color.Blend(src, dst)` (src.A==255→src, ==0→dst, else RGB blend). Foreground blends against the *resolved* background.
- Transparent window: `WindowBuilder.WithBackgroundColor(new Color(r,g,b,a))` with a<255. Default (no brush) = true transparency: empty cells bubble up characters from below with a parabolic foreground fade.
- `TransparencyBrush` overrides the compositing style (not the color/alpha): `Acrylic()` (Gaussian fg+bg blend, `Acrylic(fadeExponent:…)`/`Acrylic(textCoverage:…)`), `Mica()` (color blend, no character bubble-up), `Tinted()` (flat bg overlay), `WithCustom((topCell, cellBelow, alpha) => new Cell(...))`. Apply via `WindowBuilder.WithTransparencyBrush(...)`.
- Control-level alpha via markup: `[#RRGGBBAA]text[/]` (fg), `[on #RRGGBBAA]text[/]` (bg).
- Terminal-native transparency: set `windowSystem.DesktopBackgroundColor = Color.Transparent` and/or window `.WithBackgroundColor(Color.Transparent)` — emits ANSI `\x1b[49m` (terminal default bg) when a cell's alpha is 0. Needs emulator support (Kitty `background_opacity`, Alacritty `opacity`, WezTerm `window_background_opacity`). `ConsoleWindowSystemOptions.TerminalTransparencyMode`: `PreserveWindowColor` (default) vs `PreserveTerminalTransparency`.
- Perf: opaque windows take a zero-overhead fast path; transparent windows pay a per-cell `ResolveCellBelow` cost that scales with overlap. Keep alpha ~160–220 for a usable effect.

```csharp
new WindowBuilder(ws)
    .WithBackgroundColor(new Color(0, 20, 60, 180))
    .WithTransparencyBrush(TransparencyBrush.Mica())
    .BuildAndShow();
```

## Compositor effects

Hook the window's `CharacterBuffer` before/after control painting for blur, fade,
glow, overlays, or screenshots.

- Order per frame: rebuild DOM → layout → `Buffer.Clear()` → **`PreBufferPaint`** → `PaintDOM()` → **`PostBufferPaint`** → convert to lines.
- `Window.Renderer` (`WindowRenderer?`) exposes `Buffer` and the two events. `PreBufferPaint` is for backgrounds (fires after clear); `PostBufferPaint` is for post-processing (fires after controls, within the render lock — defer heavy work via `Task.Run`). Delegate signature: `(CharacterBuffer buffer, LayoutRect dirtyRegion, LayoutRect clipRect)`.
- `CharacterBuffer`: `GetCell/SetCell(x,y,…)`, `WriteString(x,y,text,fg,bg)`, `CopyFrom(other, rect)`, `CreateSnapshot()` → deep-copied `BufferSnapshot` (safe for screenshots/recording). `Cell` has `Rune Character`, `Foreground`, `Background`, `Decorations`, `Dirty`, `IsWideContinuation`, `Combiners` — always skip/preserve `IsWideContinuation` cells so you don't split wide (CJK/emoji) pairs.
- Best practice: process only `dirtyRegion`; use a temp buffer + `CopyFrom` to avoid feedback loops. `ColorBlendHelper.ApplyColorOverlay(buffer, color, intensity, …)` for tints. `CanvasControl` is the per-control alternative (local coords, `BeginPaint()/EndPaint()`, 30+ drawing primitives, thread-safe).

## Animations

Time-based tweens integrated into the render loop via `ConsoleWindowSystem.Animations` (`AnimationManager`).

- `ws.Animations.Animate(from, to, duration, easing?, onUpdate, onComplete?)` — overloads for `float`/`int`/`byte`/`Color` or a custom `IInterpolator<T>`. Also `HasActiveAnimations`, `ActiveCount`, `Cancel(anim)`, `CancelAll()`, `Add(IAnimation)`.
- `EasingFunctions`: `Linear`, `EaseIn`, `EaseOut`, `EaseInOut`, `Bounce`, `Elastic`, `SinePulse` (flash/pulse). Any `t => …` delegate works as custom easing.
- `WindowAnimations` transitions (use `PostBufferPaint` internally): `FadeIn(window, …)`, `FadeOut(window, …)`, `SlideIn(window, SlideDirection, …)`, `SlideOut(window, SlideDirection, …)`. `SlideDirection`: `Left/Right/Top/Bottom`.
- Delta capped at `MaxFrameDeltaMs` (33ms) so animations don't jump-complete after idle. Disable globally with `ConsoleWindowSystemOptions.EnableAnimations`.

```csharp
WindowAnimations.FadeOut(window, onComplete: () => ws.CloseWindow(window));
WindowAnimations.SlideIn(window, SlideDirection.Left, easing: EasingFunctions.Bounce);
```

## Desktop background

The area behind all windows, managed by `DesktopBackgroundService` (cached buffer,
re-rendered only on change). Layers: base fill → gradient overlay → pattern overlay.

- Startup: `ConsoleWindowSystemOptions(DesktopBackground: DesktopBackgroundConfig.FromGradient(gradient, direction))`. Runtime: `windowSystem.DesktopBackground = DesktopBackgroundConfig.FromGradient(...)` (set `null` to revert).
- Patterns: `DesktopBackgroundConfig.FromPattern(DesktopPattern)`. Built-ins in `DesktopPatterns`: `Checkerboard`, `Dots`, `HatchDown`/`HatchUp`, `Crosshatch`, `LightShade`/`MediumShade`/`DenseShade`, `HorizontalLines`, `VerticalLines`, `Grid`. Custom: `new DesktopPattern(char[,])` with optional per-cell color grids.
- Animated: set `PaintCallback` on `DesktopBackgroundConfig` (runs on the render thread every `AnimationIntervalMs`, default 100ms — keep it fast). Built-in effects: `DesktopEffects.ColorCycling(...)`, `DesktopEffects.Pulse(...)`, `DesktopEffects.DriftingGradient(...)`.

```csharp
windowSystem.DesktopBackground = DesktopEffects.ColorCycling(cycleDurationSeconds: 12.0, direction: GradientDirection.Vertical);
```

## Syntax highlighting

Lexical (token-based) highlighting; namespace `SharpConsoleUI.Highlighting`. 13
built-ins (`csharp`/`cs`, `bash`/`sh`/`shell`/`zsh`, `json`, `javascript`/`js`,
`css`, `html`, `xml`, `yaml`/`yml`, `razor`/`cshtml`, `dockerfile`/`docker`, `sln`,
`diff`/`patch`, `markdown`/`md`), all `ISyntaxHighlighter`.

- Registry: `SyntaxHighlighters.For(lang)` → `ISyntaxHighlighter?`, `Register(lang, highlighter)` (additive), `Has(lang)`.
- Consumers: Markdown fenced code blocks (auto), and `MultilineEditControl` via `.WithSyntaxHighlighter(SyntaxHighlighters.For("csharp"))` or the `SyntaxHighlighter` property.
- Custom: implement `ISyntaxHighlighter.Tokenize(line, lineIndex, startState)` → `(tokens, endState)`; register to reach both consumers.

## Image rendering

`ImageControl` over a `PixelBuffer`, auto-selecting Kitty graphics (full-res) or
Unicode half-block fallback.

- Load: `PixelBuffer.FromFile(path)` / `FromStream(stream)` / `FromImageSharp(...)` / `FromArgbArray(...)` (ImageSharp: PNG/JPEG/BMP/GIF/TIFF/TGA/PBM/WebP). `Controls.Image(pixelBuffer)`.
- `ImageControl.Source` (`PixelBuffer?`), `ScaleMode` (`ImageScaleMode`: `Fit` default, `Fill`, `Stretch`, `None`); `InvalidateImageCache()` to swap at runtime.
- Kitty protocol (Kitty/WezTerm/Ghostty) is detected automatically; half-block (`▀`) fallback gives 2× vertical resolution elsewhere. Cap height explicitly (e.g. `.Height = 12`) so an image doesn't crowd out other controls.

```csharp
window.AddControl(Controls.Image(PixelBuffer.FromFile("photo.png")));
```

## Video playback

`VideoControl` plays files/streams via an FFmpeg subprocess (FFmpeg must be on
PATH; otherwise the control shows an in-place install hint).

- `Controls.Video()` / `Controls.Video("path")`; builder `.WithSource(...)`/`.WithFile(...)`, `.WithRenderMode(mode)`, `.WithTargetFps(n)`, `.WithLooping()`, `.WithOverlay()`, `.Fill()`, `.OnPlaybackStateChanged(...)`, `.OnPlaybackEnded(...)`.
- `VideoRenderMode`: `Auto` (default — Kitty where capable, else HalfBlock), `Kitty`, `HalfBlock`, `Ascii`, `Braille`. Requesting Kitty on a non-Kitty terminal silently falls back to HalfBlock.
- Methods: `Play()`, `Pause()`, `TogglePlayPause()`, `Stop()`, `CycleRenderMode()`, `PlayFile(path)`, `Stream(url)` (HTTP/HLS/RTSP/RTMP; live streams have no seek, `DurationSeconds`=0). Keys when focused: Space play/pause, M cycle mode, L loop, R refresh, Esc stop.
- Threading: playback runs on a background thread and calls `Container?.Invalidate(Invalidation.Relayout)`; marshal state changes to the UI thread with `EnqueueOnUIThread`. Dispose on window close.

```csharp
var video = Controls.Video("video.mp4").Fill().Build();
window.AddControl(video);
video.Play();
window.OnClosed += (_, _) => { video.Stop(); video.Dispose(); };
```
