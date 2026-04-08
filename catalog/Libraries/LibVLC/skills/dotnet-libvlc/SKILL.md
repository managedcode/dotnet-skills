---
name: dotnet-libvlc
version: 1.0.0
category: Desktop
description: Expert knowledge of the libvlc C API (3.x and 4.x), the multimedia framework behind VLC media player. Use when helping with LibVLC or LibVLCSharp for media playback, streaming, or transcoding.
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
