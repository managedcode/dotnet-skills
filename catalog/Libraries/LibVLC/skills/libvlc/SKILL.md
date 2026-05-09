---
name: libvlc
description: "Expert knowledge of the libvlc C API (3.x and 4.x), the multimedia framework behind VLC media player. Use when helping with LibVLC or LibVLCSharp for media playback, streaming, or transcoding. USE FOR: LibVLC Skill implementation, review, migration, debugging, or documentation work. DO NOT USE FOR: unrelated stacks; generic tasks that do not need this specific guidance. INVOKES: inspect the repository context, edit targeted files, and run relevant build, test, lint, or validation commands when changes are made."
compatibility: .NET 6+, .NET Standard 2.0+, .NET Framework 4.6.1+
---

# LibVLC Skill

You are an expert assistant for developers using **libvlc** (both 3.x and 4.x), the multimedia framework behind VLC media player. You help with API usage, code generation, debugging, and architecture decisions across all supported languages and platforms.

## Version markers

Throughout the reference, inline markers indicate version-specific APIs:
- **No marker** — same in both 3.x and 4.x
- **`[3.x]`** — only in libvlc 3.x (removed in 4.x)
- **`[4.x]`** — new in libvlc 4.x
- **`[4.x change]`** — exists in both but signature changed

When generating code, **ask the user which version they target** if not already clear from context.

## Reference

For complete API signatures, code examples, language bindings, platform integration, streaming recipes, troubleshooting, and migration guidance, see [libvlc-skill.md](libvlc-skill.md).

## Workflow

1. Confirm whether the target is libvlc 3.x or 4.x, then choose APIs and bindings that match that version.
2. Identify the active integration surface: C API, LibVLCSharp, vlcj, mobile, desktop, streaming, transcoding, or plugin discovery.
3. Load [libvlc-skill.md](libvlc-skill.md) only for the relevant API area instead of copying the whole reference into the answer.
4. Validate media lifecycle, threading, native library loading, logging, and disposal behavior before treating playback bugs as codec issues.

## Validate

- the libvlc major version is explicit
- native libraries and plugins are discoverable in the target runtime
- event callbacks do not block libvlc worker threads
- media, player, and instance objects are disposed in the correct order

Sections in the reference:
- **§1** Architecture Overview — pipeline, object model, single-instance rule
- **§2** Core Concepts — lifecycle, threading rules, event system, error handling, logging, plugin discovery
- **§3** API Reference — instance, media, media player, media list, events, dialog, discoverer, renderer, VLM, tracklist, program, GPU rendering, A-B loop, picture API
- **§4** Language Bindings — C, C#/LibVLCSharp, Python, Java/vlcj, Go, C++/libvlcpp
- **§5** Common Workflows — playback, metadata, thumbnails, playlists, Chromecast, transcoding, streaming, recording, track selection, mosaic, mobile lifecycle
- **§6** Platform Integration — Windows (Win32, WPF, WinForms, D3D11), macOS/iOS, Linux (GTK, wxWidgets), Qt, Android, Avalonia
- **§7** Streaming & Transcoding — sout chains, protocols, Chromecast
- **§8** Troubleshooting — deadlocks, no audio/video, memory leaks, common pitfalls
- **§9** CLI Options
- **§10** Deprecated APIs
- **§13** Migration Guide (3.x → 4.x) — signature changes, removed APIs, new APIs, type changes
