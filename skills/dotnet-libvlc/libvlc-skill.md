# LibVLC — LLM Skill Document

> **Version scope: libvlc 3.x and 4.x.** This document covers both the stable **3.x** release line (VLC 3.0.x) and the **4.x** release line (VLC 4.0+). Where APIs are identical, no version marker is shown. Where they differ, inline markers indicate the version: `[3.x]` for 3.x-only APIs, `[4.x]` for 4.x-only APIs, and `[4.x change]` for APIs whose signatures changed. When generating code, **ask the user which version they target** if not already clear from context.

You are an expert assistant for developers using **libvlc** — the multimedia framework behind VLC media player. You help with API usage, code generation, debugging, and architecture decisions across all supported languages and platforms.

## How to Use This Document

- **API lookup**: Jump to §3 (API Reference) for function signatures, parameters, return types
- **Code generation**: Jump to §4 (Language Bindings) for the target language, then §5 (Workflows) for the pattern
- **Debugging**: Jump to §8 (Troubleshooting) for known pitfalls and fixes
- **Platform setup**: Jump to §6 (Platform Integration) for OS/framework-specific embedding
- **Streaming**: Jump to §7 (Streaming & Transcoding) for sout chains and Chromecast
- **Migrating 3.x → 4.x**: Jump to §13 (Migration Guide) for a concise mapping table

### Version Markers

Throughout this document:
- **No marker** — API is the same in both 3.x and 4.x
- **`[3.x]`** — Only available in libvlc 3.x (removed or replaced in 4.x)
- **`[4.x]`** — New in libvlc 4.x (not available in 3.x)
- **`[4.x change]`** — Exists in both versions but the signature changed in 4.x

---

## §1. Architecture Overview

### What is LibVLC

LibVLC is a C library providing the core multimedia engine of VLC. It handles media playback, streaming, transcoding, and device discovery. Applications embed libvlc to add multimedia capabilities.

**Three-layer architecture:**
1. **`libvlc.dll`/`libvlc.so`/`libvlc.dylib`** — Public API (what bindings call). ~200 functions.
2. **`libvlccore`** — Internal API (not for public consumption). The VLC desktop app uses this directly, NOT libvlc.
3. **360+ plugins** — Organized in subdirectories: `access/`, `codec/`, `demux/`, `video_output/`, `audio_output/`, `stream_out/`, etc. Loaded dynamically at runtime.

### Processing Pipeline

**Regular playback:**
```
Input → Access → Demux → Decode → Video/Audio Output
```

**Streaming/transcoding:**
```
Input → Access → Demux → Decode → Encode (optional) → Remux → Stream Output
```

### Object Model

All libvlc types are **opaque pointers** with **reference counting** (`retain`/`release`). The core types:

```
libvlc_instance_t          — Root context. Create ONE per application.
├── libvlc_media_t         — A media resource (file, URL, stream, file descriptor)
├── libvlc_media_player_t  — Playback engine (most-used type, ~123 C functions)
├── libvlc_media_list_t    — Ordered collection of media items
├── libvlc_media_list_player_t — Plays a media list sequentially/randomly
├── libvlc_media_discoverer_t  — Discovers network services (UPnP, DLNA)
├── libvlc_renderer_discoverer_t — Discovers renderers (Chromecast)
├── libvlc_media_library_t — Media library (minimal API)
└── libvlc_event_manager_t — Per-object event subscription
```

### Critical Rule: Single Instance

**Create exactly ONE `libvlc_instance_t` per application.** Multiple instances cause undefined behavior due to global state (plugin registry, locale settings). Multiple media players sharing one instance is the correct pattern.

---

## §2. Core Concepts

### 2.1 Object Lifecycle (Reference Counting)

Every libvlc object uses manual reference counting:
- `*_new()` / `*_new_*()` — Creates object (refcount = 1)
- `*_retain()` — Increments refcount
- `*_release()` — Decrements refcount; frees at 0

**In C:** You must call `_release()` on every object you create or retain.
**In bindings:** Varies — C# uses `IDisposable`, Python uses GC integration, Java requires explicit `release()`.

```c
// C lifecycle — libvlc 3.x
libvlc_instance_t *inst = libvlc_new(0, NULL);
libvlc_media_t *media = libvlc_media_new_path(inst, "/path/to/file.mp4");       // [3.x] inst required
libvlc_media_player_t *mp = libvlc_media_player_new_from_media(media);           // [3.x]
libvlc_media_release(media);
libvlc_media_player_play(mp);
// ... later ...
libvlc_media_player_stop(mp);             // [3.x] synchronous
libvlc_media_player_release(mp);
libvlc_release(inst);
```

```c
// C lifecycle — libvlc 4.x
libvlc_instance_t *inst = libvlc_new(0, NULL);
libvlc_media_t *media = libvlc_media_new_path("/path/to/file.mp4");              // [4.x] no inst
libvlc_media_player_t *mp = libvlc_media_player_new_from_media(inst, media);     // [4.x] inst required
libvlc_media_release(media);
libvlc_media_player_play(mp);
// ... later ...
libvlc_media_player_stop_async(mp);       // [4.x] asynchronous, returns int
libvlc_media_player_release(mp);
libvlc_release(inst);
```

**Key 3.x → 4.x lifecycle changes:**
- Media creation (`_new_path`, `_new_location`, `_new_fd`, `_new_callbacks`, `_new_as_node`) **no longer takes** `libvlc_instance_t*` in 4.x
- `libvlc_media_player_new_from_media()` **now requires** `libvlc_instance_t*` as first parameter in 4.x
- `libvlc_media_player_stop()` is replaced by `libvlc_media_player_stop_async()` in 4.x (non-blocking, returns 0 on success)
- `libvlc_media_list_new()` no longer takes instance in 4.x

### 2.2 Threading Rules

**CRITICAL — The #1 source of bugs across all bindings:**

> **NEVER call any libvlc function from within a libvlc event callback.** LibVLC is not reentrant. Calling back into libvlc from a callback thread causes **deadlock**.

**Wrong (ALL languages):**
```
on_end_reached(event):
    player.play(next_media)   // DEADLOCK — calling libvlc from callback thread
```

**Correct pattern — offload to another thread:**

| Language | Solution |
|----------|----------|
| C | `pthread_create()` or queue + worker thread |
| C# | `ThreadPool.QueueUserWorkItem(_ => player.Play(next))` |
| Python | `queue.Queue()` → process in main loop |
| Java | `mediaPlayer.submit(() -> mp.media().play(next))` |
| Go | `go func() { player.Play(next) }()` |

**Toolkit-specific callback→UI thread patterns (C):**

When using a UI toolkit, post VLC events back to the UI thread using the toolkit's mechanism:

```c
/* GTK — use g_idle_add() to run on the GTK main loop */
void on_end_vlc(const libvlc_event_t *event, void *data) {
    g_idle_add((GSourceFunc)handle_end_on_main_thread, NULL);
}

/* wxWidgets — post a custom event to the wx event loop */
void OnEndReached_VLC(const libvlc_event_t *event, void *data) {
    wxCommandEvent evt(vlcEVT_END, wxID_ANY);
    mainWindow->GetEventHandler()->AddPendingEvent(evt);
}

/* Qt — use QTimer for polling (avoids cross-thread event posting entirely) */
QTimer *timer = new QTimer(this);
connect(timer, &QTimer::timeout, this, [this]() {
    if (vlcPlayer &&
        libvlc_media_player_get_state(vlcPlayer) == libvlc_Ended)
        handleEnd();
});
timer->start(100);  /* poll every 100ms */

/* POSIX — use pthread_cond to signal a waiting thread */
void on_event_vlc(const libvlc_event_t *event, void *data) {
    pthread_mutex_lock(&lock);
    event_received = true;
    pthread_cond_signal(&cond);
    pthread_mutex_unlock(&lock);
}
```

**`[4.x]` Concurrency API — built-in lock/wait/signal:**

LibVLC 4.x provides a built-in mutex+condition variable on the media player, removing the need for external synchronization primitives in many cases:

```c
// [4.x] Wait for playback to stop using built-in concurrency
libvlc_media_player_lock(mp);
while (libvlc_media_player_get_state(mp) != libvlc_Stopped)
    libvlc_media_player_wait(mp);    // waits on internal condvar
libvlc_media_player_unlock(mp);
```

`[4.x]` The lock is recursive and safe to call from any thread. Use `libvlc_media_player_signal(mp)` to wake waiting threads from event callbacks. Note: `wait()` may spuriously wake up; always check the condition in a loop.

**`[4.x]` Watch Time API — precise time tracking:**

For UI time displays (seekbar, elapsed time), 4.x provides a high-precision timer instead of polling:

```c
// [4.x] Watch time — get precise interpolated playback time
void on_time_update(const libvlc_media_player_time_point_t *pt, void *data) {
    // WARNING: do NOT call libvlc functions here
    // Store the point and interpolate from your UI timer
    memcpy(&last_point, pt, sizeof(*pt));
}
void on_time_paused(int64_t system_date_us, void *data) { /* stop UI timer */ }

libvlc_media_player_watch_time(mp,
    100000,          // min 100ms between updates
    on_time_update,
    on_time_paused,
    NULL,            // on_seek (optional)
    user_data);

// In your UI timer callback, interpolate to current system time:
int64_t now = libvlc_clock();
int64_t ts_us;
double pos;
if (libvlc_media_player_time_point_interpolate(&last_point, now, &ts_us, &pos) == 0) {
    update_seekbar(pos);
    update_time_label(ts_us / 1000000);  // convert us to seconds
}

// Get next second boundary for timer scheduling:
int64_t next = libvlc_media_player_time_point_get_next_date(
    &last_point, now, ts_us, 1000000 /* 1 second interval */);
int64_t delay_us = libvlc_delay(next);
schedule_timer(delay_us);
```

### 2.3 Event System

Each object has an event manager obtained via `*_event_manager()`. Events are typed — see §3.6 for the full list.

**Pattern (C):**
```c
void on_playing(const libvlc_event_t *event, void *userdata) {
    // DO NOT call libvlc functions here
    // Signal your main thread instead
}

libvlc_event_manager_t *em = libvlc_media_player_event_manager(mp);
libvlc_event_attach(em, libvlc_MediaPlayerPlaying, on_playing, my_context);
// ... later ...
libvlc_event_detach(em, libvlc_MediaPlayerPlaying, on_playing, my_context);
```

### 2.4 Error Handling

- Most functions return `0` on success, `-1` on error
- `libvlc_errmsg()` returns the last error message (thread-local)
- `libvlc_clearerr()` clears the error
- Some functions return `NULL` on failure (e.g., `libvlc_new()`)

### 2.5 Logging

```c
// Set log callback
void log_cb(void *data, int level, const libvlc_log_t *ctx,
            const char *fmt, va_list args) {
    // level: LIBVLC_DEBUG=0, LIBVLC_NOTICE=2, LIBVLC_WARNING=3, LIBVLC_ERROR=4
    vfprintf(stderr, fmt, args);
}
libvlc_log_set(inst, log_cb, NULL);

// Or log to file
libvlc_log_set_file(inst, fopen("vlc.log", "w"));

// Unset (restore default)
libvlc_log_unset(inst);
```

### 2.6 Clock & Timing

```c
int64_t libvlc_clock(void);   // Current system clock in microseconds
int64_t libvlc_delay(int64_t pts);  // pts - clock (how long until pts)
```

### 2.7 Plugin Discovery & `VLC_PLUGIN_PATH`

LibVLC discovers plugins (codecs, demuxers, video outputs, etc.) at startup during `libvlc_new()`. Understanding the plugin loading mechanism is **critical** for deployment — most "no suitable decoder" or "no audio/video output" errors trace back to plugins not being found.

**Plugin search order** (from `src/modules/bank.c`):

1. **Static modules** — compiled-in plugins (used on iOS, some embedded builds)
2. **Default plugin directory** — platform-dependent:
   - **Linux/macOS/Windows desktop**: `<libvlc-install-dir>/plugins/` (relative to `libvlc.so`/`libvlc.dll`/`libvlc.dylib`)
   - **Windows Store (UWP)**: `plugins/` relative to app package root
   - **iOS**: plugins are flattened into the app bundle's library directory (no `plugins/` subfolder)
3. **`VLC_PLUGIN_PATH` environment variable** — **additive**, checked AFTER the default directory. Supports multiple paths separated by `:` (Unix) or `;` (Windows)

**Key behaviors:**
- `VLC_PLUGIN_PATH` **does not replace** the default path — it adds additional directories to scan
- Directories are scanned recursively up to **5 levels deep**
- Plugins must match the naming pattern `lib*_plugin.so` (Linux), `lib*_plugin.dylib` (macOS), or `*_plugin.dll` (Windows)
- A **`plugins.dat` cache file** is generated in each plugin directory after the first scan, significantly speeding up subsequent loads
- If the cache is stale (plugin file modified/updated), it is automatically invalidated and the plugin is re-scanned

**Setting `VLC_PLUGIN_PATH`:**

```c
// Before calling libvlc_new():
// Linux/macOS:
setenv("VLC_PLUGIN_PATH", "/opt/vlc-plugins:/usr/local/lib/vlc/plugins", 1);
// Windows:
_putenv("VLC_PLUGIN_PATH=C:\\vlc\\plugins;D:\\extra-plugins");

libvlc_instance_t *inst = libvlc_new(0, NULL);
```

```csharp
// C# — set before creating LibVLC:
Environment.SetEnvironmentVariable("VLC_PLUGIN_PATH", "/path/to/plugins");
using var libVLC = new LibVLC();
```

```python
# Python — set before importing vlc or creating Instance:
import os
os.environ["VLC_PLUGIN_PATH"] = "/path/to/plugins"
import vlc
instance = vlc.Instance()
```

**Related CLI options** (passed to `libvlc_new()`):
- `--plugins-cache` — use the plugins cache (default: enabled). Disable with `--no-plugins-cache` to force re-scanning on every startup.
- `--plugins-scan` — scan plugin directories (default: enabled). Disable with `--no-plugins-scan` to only load from cache (faster startup, but new plugins won't be found).
- `--reset-plugins-cache` — force rebuild the `plugins.dat` cache file on next startup.

**Common deployment issues:**
- **"No suitable decoder"** — plugins directory not found or codec plugin missing. Verify `VLC_PLUGIN_PATH` points to a directory containing `libavcodec_plugin.*`
- **"No audio/video output"** — output plugins missing. Ensure `libaout_*` / `libvout_*` plugins are present
- **Slow startup** — first launch scans all plugins. Pre-generate cache or reuse the instance. On mobile, prefer static linking.
- **Custom plugin directory** — when redistributing libvlc, ship `plugins/` alongside `libvlc.so`/`.dll`, or set `VLC_PLUGIN_PATH`

---

## §3. API Reference by Domain

### 3.1 Instance (`libvlc_instance_t`)

| Function | Description |
|----------|-------------|
| `libvlc_new(argc, argv)` | Create instance. `argv` = VLC CLI args (e.g., `"--verbose=2"`). Returns `NULL` on failure. |
| `libvlc_release(inst)` | Release instance (decrement refcount) |
| `libvlc_retain(inst)` | Increment refcount |
| `libvlc_add_intf(inst, name)` | `[3.x]` Add interface module (e.g., `"http"` for web control). `NULL` = default. Removed in 4.x. |
| `libvlc_set_exit_handler(inst, cb, opaque)` | `[3.x]` Callback when libvlc wants to exit. Removed in 4.x. |
| `libvlc_set_user_agent(inst, name, http)` | Set application name and HTTP User-Agent |
| `libvlc_set_app_id(inst, id, version, icon)` | Set app ID (e.g., `"com.example.myapp"`) |
| `libvlc_get_version()` | Returns version string (e.g., `"3.0.18 Vetinari"`) |
| `libvlc_get_compiler()` | Returns compiler used to build libvlc |
| `libvlc_get_changeset()` | Returns git changeset hash |
| `libvlc_abi_version()` | `[4.x]` Returns ABI version string for compatibility checks |

**Constructor arguments** use VLC CLI format: `"--option=value"`. Common:
```c
const char *args[] = {
    "--verbose=2",          // Debug logging
    "--no-video-title-show", // Don't show title overlay
    "--network-caching=1000", // 1 second network buffer
};
libvlc_instance_t *inst = libvlc_new(3, args);
```

### 3.2 Media (`libvlc_media_t`)

#### Creation

**`[4.x change]`** In 4.x, all media creation functions **drop** the `libvlc_instance_t*` parameter — media is no longer bound to an instance at creation time.

| Function (3.x) | Function (4.x) | Description |
|----------|----------|-------------|
| `libvlc_media_new_location(inst, mrl)` | `libvlc_media_new_location(mrl)` | From URL/MRL (e.g., `"https://..."`, `"rtsp://..."`) |
| `libvlc_media_new_path(inst, path)` | `libvlc_media_new_path(path)` | From local file path (auto-converts to `file://` MRL) |
| `libvlc_media_new_fd(inst, fd)` | `libvlc_media_new_fd(fd)` | From file descriptor (ownership transfers to libvlc) |
| `libvlc_media_new_callbacks(inst, open, read, seek, close, opaque)` | `libvlc_media_new_callbacks(open, read, seek, close, opaque)` | From custom bitstream callbacks |
| `libvlc_media_new_as_node(inst, name)` | `libvlc_media_new_as_node(name)` | Create empty node (for playlists) |

#### Options

```c
// Per-media options use ":option=value" format (note: colon, not double-dash)
libvlc_media_add_option(media, ":no-audio");
libvlc_media_add_option(media, ":network-caching=1000");
libvlc_media_add_option(media, ":sout=#transcode{...}:std{...}");
libvlc_media_add_option_flag(media, ":option", libvlc_media_option_trusted);
```

**Important:** Most audio/video filter options (text renderer, video filters) must be set at Instance level, not per-Media.

#### Parsing (Metadata Extraction)

```c
// [3.x] Asynchronous parsing
libvlc_media_parse_with_options(media,
    libvlc_media_parse_local | libvlc_media_fetch_local,
    5000);                      // timeout in ms (-1 = infinite)
```

```c
// [4.x] Asynchronous parsing — now takes instance, returns int
int ret = libvlc_media_parse_request(inst, media,
    libvlc_media_parse_local | libvlc_media_fetch_local,
    5000);                      // timeout in ms (-1 = infinite)
// ret: 0 on success, -1 on error
// Can cancel: libvlc_media_parse_stop(inst, media);
```

```c
// Check result (both versions)
libvlc_media_parsed_status_t status = libvlc_media_get_parsed_status(media);
// [3.x] Values: _skipped, _failed, _timeout, _done
// [4.x] Values: _none, _pending, _skipped, _failed, _timeout, _done, _cancelled
```

**Parse options flags (combinable):**

| Flag | 3.x Value | 4.x Value | Description |
|------|-----------|-----------|-------------|
| `libvlc_media_parse_local` | 0x00 | 0x01 | Parse local files |
| `libvlc_media_parse_network` | 0x01 | 0x02 | Parse network streams too |
| `libvlc_media_parse_forced` | — | 0x04 | `[4.x]` Force parsing even if already parsed |
| `libvlc_media_fetch_local` | 0x02 | 0x08 | Fetch art from local files |
| `libvlc_media_fetch_network` | 0x04 | 0x10 | Fetch art from network |
| `libvlc_media_do_interact` | 0x08 | 0x20 | Allow interaction (login dialogs) |

**Note:** Flag values changed between versions. Use the symbolic constants, not raw integers.

#### Metadata

```c
char *title = libvlc_media_get_meta(media, libvlc_meta_Title);
// Must free returned string with libvlc_free()
libvlc_free(title);

libvlc_media_set_meta(media, libvlc_meta_Title, "New Title");
libvlc_media_save_meta(media);       // [3.x] Persist to file
libvlc_media_save_meta(inst, media); // [4.x change] Now requires instance
```

**Meta types:** `Title`, `Artist`, `Genre`, `Copyright`, `Album`, `TrackNumber`, `Description`, `Rating`, `Date`, `Setting`, `URL`, `Language`, `NowPlaying`, `Publisher`, `EncodedBy`, `ArtworkURL`, `TrackID`, `TrackTotal`, `Director`, `Season`, `Episode`, `ShowName`, `Actors`, `AlbumArtist`, `DiscNumber`, `DiscTotal`

**`[4.x]` Meta Extra API** — custom key/value metadata beyond the predefined types:
```c
// [4.x] Get/set arbitrary metadata
char *val = libvlc_media_get_meta_extra(media, "MY_CUSTOM_KEY");
libvlc_free(val);

libvlc_media_set_meta_extra(media, "MY_CUSTOM_KEY", "value");

// Enumerate all extra meta keys
char **names;
unsigned count = libvlc_media_get_meta_extra_names(media, &names);
for (unsigned i = 0; i < count; i++)
    printf("Extra: %s\n", names[i]);
libvlc_media_meta_extra_names_release(names, count);
```

#### Track Information

```c
// [3.x] Track enumeration — flat array
libvlc_media_track_t **tracks;
unsigned count = libvlc_media_tracks_get(media, &tracks);
for (unsigned i = 0; i < count; i++) {
    switch (tracks[i]->i_type) {
        case libvlc_track_audio:
            printf("Audio: %d channels, %d Hz\n",
                   tracks[i]->audio->i_channels,
                   tracks[i]->audio->i_rate);
            break;
        case libvlc_track_video:
            printf("Video: %dx%d\n",
                   tracks[i]->video->i_width,
                   tracks[i]->video->i_height);
            break;
        case libvlc_track_text:
            printf("Subtitle: %s\n", tracks[i]->psz_description);
            break;
    }
}
libvlc_media_tracks_release(tracks, count);
```

```c
// [4.x] Tracklist API — typed tracklist, string IDs, hold/release
libvlc_media_tracklist_t *tl = libvlc_media_get_tracklist(media, libvlc_track_video);
size_t count = libvlc_media_tracklist_count(tl);
for (size_t i = 0; i < count; i++) {
    libvlc_media_track_t *t = libvlc_media_tracklist_at(tl, i);
    printf("Video track '%s': %dx%d (codec: %s)\n",
           t->psz_id, t->video->i_width, t->video->i_height,
           libvlc_media_get_codec_description(t->i_type, t->i_codec));
    // t->psz_name: human-readable name (when from media_player)
    // t->id_stable: true if ID is stable across playback sessions
    // t->selected: true if currently selected (when from media_player)
}
libvlc_media_tracklist_delete(tl);

// To keep a track beyond the tracklist lifetime:
libvlc_media_track_t *held = libvlc_media_track_hold(t);
// ... use held ...
libvlc_media_track_release(held);
```

#### Statistics

```c
libvlc_media_stats_t stats;
libvlc_media_get_stats(media, &stats);
// stats.i_decoded_video, stats.i_decoded_audio,
// stats.i_demux_read_bytes, stats.f_demux_bitrate,
// stats.i_lost_pictures, stats.i_played_abuffers, etc.
```

#### Other

| Function | Description |
|----------|-------------|
| `libvlc_media_get_mrl(media)` | Get MRL string (must free with `libvlc_free`) |
| `libvlc_media_duplicate(media)` | Clone media object |
| `libvlc_media_get_state(media)` | `[3.x]` Get state: `NothingSpecial`, `Opening`, `Buffering`, `Playing`, `Paused`, `Stopped`, `Ended`, `Error`. Removed in 4.x (use `libvlc_media_player_get_state()` instead, which adds `Stopping`). |
| `libvlc_media_get_duration(media)` | Duration in ms (-1 if unknown; parse first) |
| `libvlc_media_get_type(media)` | `unknown`, `file`, `directory`, `disc`, `stream`, `playlist` |
| `libvlc_media_subitems(media)` | Get sub-items as `libvlc_media_list_t` (for playlists, YouTube URLs, m3u8) |
| `libvlc_media_slaves_add(media, type, priority, uri)` | Add subtitle/audio slave |
| `libvlc_media_slaves_get(media, &slaves)` | Get attached slaves |
| `libvlc_media_retain(media)` / `libvlc_media_release(media)` | Refcounting |
| `libvlc_media_get_filestat(media, type, &val)` | `[4.x]` Get file stat: type 0 = mtime (epoch), type 1 = size (bytes) |
| `libvlc_media_get_codec_description(type, fourcc)` | `[4.x]` Get human-readable codec name from fourcc |

**`[4.x]` Thumbnail Request API** — asynchronous thumbnail generation from media (without playing):
```c
// [4.x] Request a thumbnail at a specific time
libvlc_media_thumbnail_request_t *req =
    libvlc_media_thumbnail_request_by_time(inst, media,
        10000000,                          // time in us (10 seconds)
        libvlc_media_thumbnail_seek_fast,  // or _precise
        320, 240,                          // width, height
        false,                             // crop (false = fit)
        libvlc_picture_Png,                // output format
        5000);                             // timeout in ms
// Or by position (same params as by_time, but with double pos instead of time):
// libvlc_media_thumbnail_request_by_pos(inst, media, 0.5,
//     libvlc_media_thumbnail_seek_fast, 320, 240, false,
//     libvlc_picture_Png, 5000);

// Listen for libvlc_MediaThumbnailGenerated event on media's event manager
// The event provides a libvlc_picture_t*

// Cancel / destroy
libvlc_media_thumbnail_request_cancel(req);
libvlc_media_thumbnail_request_destroy(req);
```

### 3.3 Media Player (`libvlc_media_player_t`)

The largest API surface (~123 C functions).

#### Creation & Media

| Function | Description |
|----------|-------------|
| `libvlc_media_player_new(inst)` | Create empty player |
| `libvlc_media_player_new_from_media(media)` | `[3.x]` Create player pre-loaded with media |
| `libvlc_media_player_new_from_media(inst, media)` | `[4.x change]` Now requires instance as first parameter |
| `libvlc_media_player_set_media(mp, media)` | Set/change current media |
| `libvlc_media_player_get_media(mp)` | Get current media (caller must release) |
| `libvlc_media_player_release(mp)` | Release player |
| `libvlc_media_player_retain(mp)` | Retain player |

#### Playback Control

| Function | Description |
|----------|-------------|
| `libvlc_media_player_play(mp)` | Start playback (async — returns immediately) |
| `libvlc_media_player_pause(mp)` | Toggle pause |
| `libvlc_media_player_set_pause(mp, pause)` | Set pause state (1=pause, 0=resume) |
| `libvlc_media_player_stop(mp)` | `[3.x]` Stop playback (synchronous, can be slow — offload to thread) |
| `libvlc_media_player_stop_async(mp)` | `[4.x]` Stop playback (async, returns 0 on success). Listen for `libvlc_MediaPlayerStopping` → `libvlc_MediaPlayerStopped` events. |
| `libvlc_media_player_is_playing(mp)` | Returns 1 `[3.x]` / `bool` `[4.x]` if playing |
| `libvlc_media_player_get_state(mp)` | Get player state enum. `[4.x]` adds `libvlc_Stopping` state. |
| `libvlc_media_player_get_length(mp)` | Duration in ms |
| `libvlc_media_player_get_time(mp)` | Current time in ms |
| `libvlc_media_player_set_time(mp, time)` | `[3.x]` Seek to time in ms |
| `libvlc_media_player_set_time(mp, time, b_fast)` | `[4.x change]` Seek to time in ms. `b_fast=true` for fast (imprecise) seek. |
| `libvlc_media_player_get_position(mp)` | Position 0.0–1.0 (`float` `[3.x]` / `double` `[4.x]`) |
| `libvlc_media_player_set_position(mp, pos)` | `[3.x]` Seek to position (float) |
| `libvlc_media_player_set_position(mp, pos, b_fast)` | `[4.x change]` Seek to position (double). `b_fast=true` for fast seek. |
| `libvlc_media_player_jump_time(mp, time)` | `[4.x]` Relative seek by `time` ms (positive = forward, negative = backward) |
| `libvlc_media_player_set_rate(mp, rate)` | Playback speed (1.0 = normal, 2.0 = 2x) |
| `libvlc_media_player_get_rate(mp)` | Get current rate |
| `libvlc_media_player_will_play(mp)` | `[3.x]` Can this media be played? Removed in 4.x. |
| `libvlc_media_player_is_seekable(mp)` | Is seeking supported? (`int` `[3.x]` / `bool` `[4.x]`) |
| `libvlc_media_player_can_pause(mp)` | Is pausing supported? (`int` `[3.x]` / `bool` `[4.x]`) |
| `libvlc_media_player_program_scrambled(mp)` | Is stream scrambled? |
| `libvlc_media_player_next_frame(mp)` | Advance one frame (while paused) |
| `libvlc_media_player_navigate(mp, nav)` | DVD navigation: `activate`, `up`, `down`, `left`, `right` |
| `libvlc_media_player_record(mp, enable, dir)` | `[4.x]` Start/stop recording. `dir` = output directory (NULL for default). Listen for `libvlc_MediaPlayerRecordChanged`. |

#### Video Output & Window Embedding

| Function | Platform | Description |
|----------|----------|-------------|
| `libvlc_media_player_set_hwnd(mp, hwnd)` | Windows | Set Win32 window handle (`HWND`) |
| `libvlc_media_player_set_xwindow(mp, xid)` | Linux/X11 | Set X11 window ID |
| `libvlc_media_player_set_nsobject(mp, view)` | macOS/iOS | Set `NSView*` / `UIView*`. `[4.x]` The view can implement `VLCDrawable` protocol for resize notifications and PictureInPicture support. |
| `libvlc_media_player_set_android_context(mp, ctx)` | Android | Set Android `AWindow` context |
| `libvlc_media_player_set_evas_object(mp, obj)` | Tizen/EFL | `[3.x]` Set Evas object. Removed in 4.x. |

**Windows `WS_CLIPCHILDREN` requirement:** When embedding video in a Win32 window, the parent window **must** have the `WS_CLIPCHILDREN` style set. Without it, GDI repaints will overwrite the video surface, causing flickering or a blank/white area. Set it either in `CreateWindowEx` flags or dynamically before calling `set_hwnd`:
```c
LONG style = GetWindowLong(hwnd, GWL_STYLE);
if (!(style & WS_CLIPCHILDREN))
    SetWindowLong(hwnd, GWL_STYLE, style | WS_CLIPCHILDREN);
libvlc_media_player_set_hwnd(mp, hwnd);
```

#### Video Properties

| Function | Description |
|----------|-------------|
| `libvlc_video_get_size(mp, num, &w, &h)` | Get video dimensions for track `num` |
| `libvlc_video_get_cursor(mp, num, &x, &y)` | Get cursor position in video |
| `libvlc_video_get_scale(mp)` / `set_scale` | Video scaling factor (0 = auto-fit) |
| `libvlc_video_get_aspect_ratio(mp)` / `set_aspect_ratio` | Aspect ratio string (e.g., `"16:9"`, `"4:3"`, `"fill"`) |
| `libvlc_video_set_crop_geometry(mp, geo)` | `[3.x]` Crop geometry (e.g., `"16:10"`). Removed in 4.x. |
| `libvlc_video_set_crop_ratio(mp, num, den)` | `[4.x]` Set crop ratio (e.g., 16,9). Set den=0 to disable. |
| `libvlc_video_set_crop_window(mp, x, y, w, h)` | `[4.x]` Crop to pixel rectangle |
| `libvlc_video_set_crop_border(mp, left, right, top, bottom)` | `[4.x]` Crop by border sizes |
| `libvlc_video_set_deinterlace(mp, mode)` | `[3.x]` Deinterlace mode: `"blend"`, `"linear"`, `"x"`, `"yadif"`, `"yadif2x"`, `""` (disable) |
| `libvlc_video_set_deinterlace(mp, state, mode)` | `[4.x change]` `state`: -1=auto, 0=off, 1=on. `mode`: filter name or NULL for default. |
| `libvlc_video_get_spu_delay(mp)` / `set_spu_delay` | Subtitle delay in microseconds |
| `libvlc_video_get_spu_text_scale(mp)` / `set_spu_text_scale` | `[4.x]` Subtitle text scale factor (0.1–5.0, default 1.0) |
| `libvlc_video_set_teletext(mp, page)` | Teletext page |
| `libvlc_video_set_teletext_transparency(mp, b)` / `get_` | `[4.x]` Teletext background transparency |
| `libvlc_video_take_snapshot(mp, num, path, w, h)` | Save screenshot to file |
| `libvlc_video_get_display_fit(mp)` / `set_display_fit` | `[4.x]` Display fit mode: `none`, `contain`, `cover`, `fit_width`, `fit_height` (`libvlc_video_fit_mode_t`) |
| `libvlc_video_get_video_stereo_mode(mp)` / `set_` | `[4.x]` Video stereo mode: `Auto`, `Stereo`, `LeftEye`, `RightEye`, `SideBySide` |
| `libvlc_video_set_projection_mode(mp, mode)` | `[4.x]` Force projection mode (rectangular, equirectangular, cubemap) for 360 content |
| `libvlc_video_unset_projection_mode(mp)` | `[4.x]` Remove forced projection mode |
| `libvlc_video_get_track_count(mp)` | `[3.x]` Number of video tracks. Use tracklist API in 4.x. |
| `libvlc_video_get_track(mp)` / `set_track` | `[3.x]` Select video track. Use tracklist API in 4.x. |
| `libvlc_video_get_track_description(mp)` | `[3.x]` List of track descriptions. Use tracklist API in 4.x. |
| `libvlc_video_get_spu(mp)` / `set_spu` | `[3.x]` Subtitle track selection. Use tracklist API in 4.x. |
| `libvlc_video_get_spu_count(mp)` | `[3.x]` Number of subtitle tracks. Use tracklist API in 4.x. |

#### Video Marquee (Text Overlay)

```c
libvlc_video_set_marquee_int(mp, libvlc_marquee_Enable, 1);
libvlc_video_set_marquee_string(mp, libvlc_marquee_Text, "Hello World");
libvlc_video_set_marquee_int(mp, libvlc_marquee_Color, 0xFF0000);    // Red
libvlc_video_set_marquee_int(mp, libvlc_marquee_Size, 24);           // Font size
libvlc_video_set_marquee_int(mp, libvlc_marquee_Position, 8);        // Bottom
libvlc_video_set_marquee_int(mp, libvlc_marquee_Timeout, 5000);      // 5 seconds
libvlc_video_set_marquee_int(mp, libvlc_marquee_Opacity, 200);       // 0-255
libvlc_video_set_marquee_int(mp, libvlc_marquee_X, 10);              // X position
libvlc_video_set_marquee_int(mp, libvlc_marquee_Y, 10);              // Y position
libvlc_video_set_marquee_int(mp, libvlc_marquee_Refresh, 1000);      // Refresh interval ms
```

#### Video Logo (Image Overlay)

```c
libvlc_video_set_logo_int(mp, libvlc_logo_enable, 1);
libvlc_video_set_logo_string(mp, libvlc_logo_file, "/path/to/logo.png");
libvlc_video_set_logo_int(mp, libvlc_logo_x, 10);
libvlc_video_set_logo_int(mp, libvlc_logo_y, 10);
libvlc_video_set_logo_int(mp, libvlc_logo_opacity, 200);  // 0-255
libvlc_video_set_logo_int(mp, libvlc_logo_delay, 0);      // ms between images
libvlc_video_set_logo_int(mp, libvlc_logo_repeat, -1);     // -1 = infinite
```

#### Audio

| Function | Description |
|----------|-------------|
| `libvlc_audio_get_volume(mp)` / `set_volume` | Volume 0–200 (100 = normal, >100 = amplify) |
| `libvlc_audio_get_mute(mp)` / `set_mute` / `toggle_mute` | Mute control |
| `libvlc_audio_get_track(mp)` / `set_track` | `[3.x]` Audio track selection. Use tracklist API in 4.x. |
| `libvlc_audio_get_track_count(mp)` | `[3.x]` Number of audio tracks. Use tracklist API in 4.x. |
| `libvlc_audio_get_track_description(mp)` | `[3.x]` List of track descriptions. Use tracklist API in 4.x. |
| `libvlc_audio_get_delay(mp)` / `set_delay` | Audio delay in microseconds |
| `libvlc_audio_get_channel(mp)` / `set_channel` | `[3.x]` Audio channel mode: `Stereo`, `RStereo`, `Left`, `Right`, `Dolbys` |
| `libvlc_audio_get_stereomode(mp)` / `set_stereomode` | `[4.x]` Replaces `get/set_channel`. Stereo mode: `Unset`, `Stereo`, `RStereo`, `Left`, `Right`, `Dolbys`, `Mono` |
| `libvlc_audio_get_mixmode(mp)` / `set_mixmode` | `[4.x]` Audio mix/upmix mode: `Unset`, `Stereo`, `Binaural`, `4_0`, `5_1`, `7_1`. Force channel layout regardless of source. |
| `libvlc_audio_output_list_get(inst)` | List available audio outputs |
| `libvlc_audio_output_set(mp, name)` | Set audio output module |
| `libvlc_audio_output_device_list_get(inst, aout)` | `[3.x]` List devices for output. Use `device_enum` in 4.x. |
| `libvlc_audio_output_device_enum(mp)` | List devices for current output (both versions, preferred in 4.x) |
| `libvlc_audio_output_device_set(mp, module, device_id)` | `[3.x]` Set specific audio device (3 params) |
| `libvlc_audio_output_device_set(mp, device_id)` | `[4.x change]` Set audio device (2 params, module param removed) |
| `libvlc_audio_output_device_get(mp)` | Get current audio device identifier (free with `free()`) |

#### Audio Equalizer

LibVLC provides a 10-band audio equalizer with 18 built-in presets (Flat, Classical, Club, Dance, Full bass, etc.). The equalizer is an independent object that you configure and then apply to a media player. Changes take effect immediately, even during playback.

**C — Full equalizer workflow:**
```c
// List available presets
unsigned preset_count = libvlc_audio_equalizer_get_preset_count();
for (unsigned i = 0; i < preset_count; i++)
    printf("Preset %u: %s\n", i, libvlc_audio_equalizer_get_preset_name(i));

// Create from preset (e.g., "Rock" = preset index 1)
libvlc_equalizer_t *eq = libvlc_audio_equalizer_new_from_preset(1);
// Or create blank (all bands at 0 dB):
// libvlc_equalizer_t *eq = libvlc_audio_equalizer_new();

// Pre-amplification: -20.0 to +20.0 dB
libvlc_audio_equalizer_set_preamp(eq, 12.0);

// 10 frequency bands — get frequencies:
unsigned band_count = libvlc_audio_equalizer_get_band_count();  // Always 10
for (unsigned i = 0; i < band_count; i++)
    printf("Band %u: %.0f Hz\n", i, libvlc_audio_equalizer_get_band_frequency(i));
// Bands: 60Hz, 170Hz, 310Hz, 600Hz, 1kHz, 3kHz, 6kHz, 12kHz, 14kHz, 16kHz

// Set amplification per band: -20.0 to +20.0 dB
libvlc_audio_equalizer_set_amp_at_index(eq, 8.0, 0);   // Boost 60Hz bass
libvlc_audio_equalizer_set_amp_at_index(eq, -3.0, 9);   // Cut 16kHz treble

// Apply to player (can be done before or during playback)
libvlc_media_player_set_equalizer(mp, eq);

// Disable equalizer:
libvlc_media_player_set_equalizer(mp, NULL);

// Release when done (player does NOT keep a reference)
libvlc_audio_equalizer_release(eq);
```

**C# (LibVLCSharp):**
```csharp
// Create from preset
using var eq = new Equalizer(presetIndex: 1);  // "Rock"

// Or blank:
// using var eq = new Equalizer();

// Configure
eq.SetPreamp(12.0f);
eq.SetAmp(8.0f, 0);   // Band 0 = 60Hz

// Apply
player.SetEqualizer(eq);

// Disable
player.UnsetEqualizer();

// List presets
for (uint i = 0; i < Equalizer.PresetCount; i++)
    Console.WriteLine($"Preset {i}: {Equalizer.PresetName(i)}");
```

**Python:**
```python
import vlc

# Create from preset
eq = vlc.AudioEqualizer.from_preset(1)  # "Rock"

# Configure
eq.set_preamp(12.0)
eq.set_amp_at_index(8.0, 0)  # Boost 60Hz

# Apply to player
player.set_equalizer(eq)

# Disable
player.set_equalizer(None)
```

**Key points:**
- The player does NOT keep a reference to the equalizer — you can release/modify it after `set_equalizer()` and re-apply
- Changes apply immediately to the currently playing audio
- If set before playback, settings persist for subsequently played media
- The equalizer object is independent of the player — you can reuse one equalizer across multiple players

#### Custom Video Rendering (Callbacks)

For rendering video frames yourself (e.g., into a texture, off-screen buffer, or custom UI):

```c
// Lock callback: allocate/return buffer for VLC to decode into
void *lock(void *opaque, void **planes) {
    my_context *ctx = (my_context *)opaque;
    *planes = ctx->pixel_buffer;
    return NULL;  // picture identifier (passed to unlock/display)
}

// Unlock callback: called after decoding
void unlock(void *opaque, void *picture, void *const *planes) {
    // Optional: post-processing
}

// Display callback: frame is ready to show
void display(void *opaque, void *picture) {
    my_context *ctx = (my_context *)opaque;
    // Render ctx->pixel_buffer to screen/texture
}

libvlc_video_set_callbacks(mp, lock, unlock, display, my_context);
libvlc_video_set_format(mp, "RV32", width, height, width * 4);  // BGRA 32-bit
// Or use format callback for dynamic sizing:
// libvlc_video_set_format_callbacks(mp, setup_cb, cleanup_cb);
```

**Chroma formats:** `"RV32"` (BGRA), `"RV24"` (BGR), `"RV16"`, `"I420"` (YUV planar), `"NV12"`, `"UYVY"`, `"YUYV"`

**Performance note (LibVLC 3.x):** Video callbacks involve CPU copies — no GPU acceleration. Minimize resolution and prefer `I420` chroma over `RV32` to reduce copy overhead.

#### Custom Audio Rendering (Callbacks)

```c
void audio_play(void *data, const void *samples, unsigned count, int64_t pts) {
    // Render audio samples
}
void audio_pause(void *data, int64_t pts) { /* pause output */ }
void audio_resume(void *data, int64_t pts) { /* resume output */ }
void audio_flush(void *data, int64_t pts) { /* flush buffers */ }
void audio_drain(void *data) { /* drain remaining */ }

libvlc_audio_set_callbacks(mp, audio_play, audio_pause, audio_resume,
                           audio_flush, audio_drain, my_context);
libvlc_audio_set_format(mp, "S16N", 44100, 2);  // 16-bit signed, 44.1kHz, stereo
// Or: libvlc_audio_set_format_callbacks(mp, setup_cb, cleanup_cb);
```

**C# (LibVLCSharp) — Full audio callbacks with NAudio playback + file recording:**

This example uses `SetAudioFormatCallback` to negotiate audio format, then `SetAudioCallbacks` to route decoded PCM samples to both a speaker (via NAudio `WaveOutEvent`) and a WAV file writer:

```csharp
using var libVLC = new LibVLC(enableDebugLogs: true);
using var media = new Media(libVLC,
    new Uri("http://example.com/video.mp4"), ":no-video");
using var mediaPlayer = new MediaPlayer(media);

// Set up audio output
var waveFormat = new WaveFormat(8000, 16, 1);  // 8kHz, 16-bit, mono
var writer = new WaveFileWriter("sound.wav", waveFormat);
var waveProvider = new BufferedWaveProvider(waveFormat);
using var outputDevice = new WaveOutEvent();
outputDevice.Init(waveProvider);

// Negotiate format — libvlc calls this to agree on sample rate/channels
mediaPlayer.SetAudioFormatCallback(
    (ref IntPtr opaque, ref IntPtr format, ref uint rate, ref uint channels) =>
    {
        channels = (uint)waveFormat.Channels;
        rate = (uint)waveFormat.SampleRate;
        return 0;
    },
    (IntPtr opaque) => { /* cleanup */ });

// Route decoded audio samples
mediaPlayer.SetAudioCallbacks(
    (IntPtr data, IntPtr samples, uint count, long pts) =>
    {
        int bytes = (int)count * 2;  // 16-bit mono = 2 bytes per sample
        var buffer = new byte[bytes];
        Marshal.Copy(samples, buffer, 0, bytes);
        waveProvider.AddSamples(buffer, 0, bytes);  // Speaker output
        writer.Write(buffer, 0, bytes);              // File recording
    },
    (IntPtr data, long pts) => outputDevice.Pause(),   // pause
    (IntPtr data, long pts) => outputDevice.Play(),    // resume
    (IntPtr data, long pts) => { writer.Flush(); waveProvider.ClearBuffer(); },  // flush
    (IntPtr data) => writer.Flush());                  // drain

mediaPlayer.Play();
outputDevice.Play();
```

**Key points for audio callbacks:**
- Use `:no-video` media option when only audio is needed — avoids video decoding overhead
- `SetAudioFormatCallback` is called **before** playback — use it to negotiate sample rate and channels
- The `play` callback receives raw PCM samples as `IntPtr` — use `Marshal.Copy` to get managed byte arrays
- `count` is the number of **samples**, not bytes — multiply by bytes-per-sample (e.g., `count * 2` for 16-bit mono)
- All callbacks run on libvlc's audio thread — keep processing fast to avoid audio glitches

#### Subtitle / Media Slave

```c
// Add external subtitle file
libvlc_media_player_add_slave(mp, libvlc_media_slave_type_subtitle,
                               "file:///path/to/subs.srt", true);
// Add external audio track
libvlc_media_player_add_slave(mp, libvlc_media_slave_type_audio,
                               "file:///path/to/audio.mp3", true);
```

#### Chapters & Titles (DVD/Blu-ray)

| Function | Description |
|----------|-------------|
| `libvlc_media_player_get_chapter(mp)` / `set_chapter` | Current chapter |
| `libvlc_media_player_get_chapter_count(mp)` | Total chapters |
| `libvlc_media_player_get_title(mp)` / `set_title` | Current title |
| `libvlc_media_player_get_title_count(mp)` | Total titles |
| `libvlc_media_player_get_full_title_descriptions(mp, &descs)` | Detailed title info |
| `libvlc_media_player_get_full_chapter_descriptions(mp, title, &descs)` | Detailed chapter info |
| `libvlc_media_player_previous_chapter(mp)` / `next_chapter` | Chapter navigation |

#### 360° Video

```c
libvlc_video_update_viewpoint(mp, &(libvlc_video_viewpoint_t){
    .f_yaw   = 45.0,   // Horizontal rotation (-180 to 180)
    .f_pitch = -10.0,   // Vertical rotation (-90 to 90)
    .f_roll  = 0.0,     // Rotation around axis
    .f_field_of_view = 80.0  // FOV in degrees
}, false);  // false = absolute, true = relative
```

#### Media Player Role

```c
libvlc_media_player_set_role(mp, libvlc_role_Music);
// Roles: None, Music, Video, Communication, Game, Notification,
//        Animation, Production, Accessibility, Test
```

### 3.4 Media List (`libvlc_media_list_t`)

Thread-safe ordered collection of media items. **Must lock before read/write operations.**

```c
libvlc_media_list_t *ml = libvlc_media_list_new(inst);    // [3.x] takes instance
// libvlc_media_list_t *ml = libvlc_media_list_new();     // [4.x] no instance

libvlc_media_list_lock(ml);       // MUST lock before modifying
libvlc_media_list_add_media(ml, media1);
libvlc_media_list_add_media(ml, media2);
libvlc_media_list_insert_media(ml, media3, 0);  // Insert at index
int count = libvlc_media_list_count(ml);
libvlc_media_t *m = libvlc_media_list_item_at_index(ml, 0);  // Must release
libvlc_media_list_remove_index(ml, 0);
libvlc_media_list_unlock(ml);     // MUST unlock after

libvlc_media_list_release(ml);
```

### 3.5 Media List Player (`libvlc_media_list_player_t`)

Plays through a media list with configurable playback mode.

```c
libvlc_media_list_player_t *mlp = libvlc_media_list_player_new(inst);
libvlc_media_list_player_set_media_player(mlp, mp);
libvlc_media_list_player_set_media_list(mlp, ml);

// Playback modes
libvlc_media_list_player_set_playback_mode(mlp, libvlc_playback_mode_default);  // Sequential
libvlc_media_list_player_set_playback_mode(mlp, libvlc_playback_mode_loop);     // Repeat all
libvlc_media_list_player_set_playback_mode(mlp, libvlc_playback_mode_repeat);   // Repeat one

libvlc_media_list_player_play(mlp);
libvlc_media_list_player_next(mlp);
libvlc_media_list_player_previous(mlp);
libvlc_media_list_player_play_item_at_index(mlp, 2);
libvlc_media_list_player_play_item(mlp, specific_media);

libvlc_media_list_player_pause(mlp);
libvlc_media_list_player_stop(mlp);       // [3.x] synchronous
// libvlc_media_list_player_stop_async(mlp); // [4.x] asynchronous
libvlc_media_list_player_is_playing(mlp);  // [3.x] returns int, [4.x] returns bool
libvlc_media_list_player_get_state(mlp);

libvlc_media_list_player_release(mlp);
```

### 3.6 Events (`libvlc_event_t`)

#### Event Types

**MediaPlayer events (most common):**

| Event | Extra Data | Notes |
|-------|-----------|-------|
| `libvlc_MediaPlayerMediaChanged` | `new_media` | |
| `libvlc_MediaPlayerOpening` | — | |
| `libvlc_MediaPlayerBuffering` | `new_cache` (float, 0–100%) | |
| `libvlc_MediaPlayerPlaying` | — | |
| `libvlc_MediaPlayerPaused` | — | |
| `libvlc_MediaPlayerStopped` | — | |
| `libvlc_MediaPlayerStopping` | — | `[4.x]` Fired before `Stopped` when `stop_async()` begins |
| `libvlc_MediaPlayerForward` | — | |
| `libvlc_MediaPlayerBackward` | — | |
| `libvlc_MediaPlayerEndReached` | — | |
| `libvlc_MediaPlayerEncounteredError` | — | |
| `libvlc_MediaPlayerTimeChanged` | `new_time` (int64_t, ms) | |
| `libvlc_MediaPlayerPositionChanged` | `new_position` | `[3.x]` float. `[4.x]` double. |
| `libvlc_MediaPlayerSeekableChanged` | `new_seekable` | |
| `libvlc_MediaPlayerPausableChanged` | `new_pausable` | |
| `libvlc_MediaPlayerTitleChanged` | `new_title` (int) | |
| `libvlc_MediaPlayerSnapshotTaken` | `psz_filename` (char*) | |
| `libvlc_MediaPlayerLengthChanged` | `new_length` (int64_t) | |
| `libvlc_MediaPlayerVout` | `new_count` (int) | |
| `libvlc_MediaPlayerScrambledChanged` | `new_scrambled` (int) | |
| `libvlc_MediaPlayerESAdded` | `[3.x]` `i_type`, `i_id` (int). `[4.x]` `i_type`, `psz_id` (string). | |
| `libvlc_MediaPlayerESDeleted` | Same as ESAdded | |
| `libvlc_MediaPlayerESSelected` | `[3.x]` `i_type`, `i_id`. `[4.x]` `psz_unselected_id`, `psz_selected_id`. | |
| `libvlc_MediaPlayerESUpdated` | `i_type`, `psz_id` | `[4.x]` Track info changed |
| `libvlc_MediaPlayerProgramAdded` | `i_id`, `psz_name` | `[4.x]` MPEG-TS program |
| `libvlc_MediaPlayerProgramDeleted` | `i_id` | `[4.x]` |
| `libvlc_MediaPlayerProgramUpdated` | `i_id`, `psz_name` | `[4.x]` |
| `libvlc_MediaPlayerProgramSelected` | `i_unselected_id`, `i_selected_id` | `[4.x]` |
| `libvlc_MediaPlayerTitleListChanged` | — | `[4.x]` Title list updated |
| `libvlc_MediaPlayerTitleSelectionChanged` | `title`, `index` | `[4.x]` |
| `libvlc_MediaPlayerRecordChanged` | `recording` (bool), `psz_recorded_file_path` | `[4.x]` |
| `libvlc_MediaPlayerCorked` | — | |
| `libvlc_MediaPlayerUncorked` | — | |
| `libvlc_MediaPlayerMuted` | — | |
| `libvlc_MediaPlayerUnmuted` | — | |
| `libvlc_MediaPlayerAudioVolume` | `volume` (float) | |
| `libvlc_MediaPlayerAudioDevice` | `device` (char*) | |
| `libvlc_MediaPlayerChapterChanged` | `new_chapter` (int) | |

**Media events:**

| Event | Extra Data | Notes |
|-------|-----------|-------|
| `libvlc_MediaMetaChanged` | `meta_type` | |
| `libvlc_MediaSubItemAdded` | `new_child` (media) | |
| `libvlc_MediaDurationChanged` | `new_duration` (int64_t) | |
| `libvlc_MediaParsedChanged` | `new_status` (int) | |
| `libvlc_MediaFreed` | `md` (media) | `[3.x]` Removed in 4.x. |
| `libvlc_MediaStateChanged` | `new_state` | `[3.x]` Removed in 4.x. |
| `libvlc_MediaSubItemTreeAdded` | `item` (media) | |
| `libvlc_MediaThumbnailGenerated` | `p_thumbnail` (libvlc_picture_t*) | `[4.x]` From thumbnail request |
| `libvlc_MediaAttachedThumbnailsFound` | `p_thumbnail` (libvlc_picture_t*) | `[4.x]` Embedded artwork |

**MediaList events:** `ItemAdded` (`item`, `index`), `WillAddItem`, `ItemDeleted`, `WillDeleteItem`, `EndReached` `[4.x]`

**MediaDiscoverer events:** `Started`, `Ended`

**RendererDiscoverer events:** `ItemAdded` (`item`), `ItemDeleted` (`item`)

**`[3.x]` VLM events:** `MediaAdded`, `MediaRemoved`, `MediaChanged`, `MediaInstanceStarted`, `MediaInstanceStopped`, `MediaInstanceStatusInit/Opening/Playing/Pause/End/Error` — VLM is removed in 4.x.

### 3.7 Dialog API (`libvlc_dialog_cbs`)

Handle login prompts, questions, and progress for user interaction:

```c
// [3.x] Error callback is part of the struct
const libvlc_dialog_cbs cbs = {
    .pf_display_error    = on_error,     // (title, text)
    .pf_display_login    = on_login,     // (id, title, text, default_user, ask_store)
    .pf_display_question = on_question,  // (id, title, text, type, cancel, action1, action2)
    .pf_display_progress = on_progress,  // (id, title, text, indeterminate, position, cancel)
    .pf_cancel           = on_cancel,    // (id)
    .pf_update_progress  = on_update,    // (id, position, text)
};
libvlc_dialog_set_callbacks(inst, &cbs, my_data);
```

```c
// [4.x change] Error callback is registered separately
const libvlc_dialog_cbs cbs = {
    .pf_display_login    = on_login,
    .pf_display_question = on_question,
    .pf_display_progress = on_progress,
    .pf_cancel           = on_cancel,
    .pf_update_progress  = on_update,
};
libvlc_dialog_set_callbacks(inst, &cbs, my_data);
libvlc_dialog_set_error_callback(inst, on_error, my_data);  // [4.x] separate
```

```c
// Respond to dialog (same in both versions):
libvlc_dialog_post_login(id, username, password, store);
libvlc_dialog_post_action(id, action_number);  // 1 or 2
libvlc_dialog_dismiss(id);
```

### 3.8 Media Discoverer (`libvlc_media_discoverer_t`)

Discover network services (UPnP, Bonjour, SAP, etc.):

```c
// List available discoverers by category
libvlc_media_discoverer_description_t **descs;
size_t count = libvlc_media_discoverer_list_get(inst,
    libvlc_media_discoverer_devices,  // or _lan, _podcasts, _localdirs
    &descs);
// Each has: psz_name, psz_longname, i_cat

// Create and start
libvlc_media_discoverer_t *md = libvlc_media_discoverer_new(inst, descs[0]->psz_name);
libvlc_media_discoverer_start(md);

// Get discovered items
libvlc_media_list_t *ml = libvlc_media_discoverer_media_list(md);
// Listen for ItemAdded/ItemDeleted events on the media list

libvlc_media_discoverer_stop(md);
libvlc_media_discoverer_release(md);
libvlc_media_discoverer_description_list_release(descs, count);
```

**Categories:**
- `libvlc_media_discoverer_devices` — Audio/video devices (webcam, mic)
- `libvlc_media_discoverer_lan` — LAN services (UPnP, SMB shares)
- `libvlc_media_discoverer_podcasts` — Podcast directories
- `libvlc_media_discoverer_localdirs` — Local directories

### 3.9 Renderer Discoverer (`libvlc_renderer_discoverer_t`)

Find Chromecast, UPnP renderers:

```c
libvlc_renderer_discoverer_description_t **descs;
size_t count = libvlc_renderer_discoverer_list_get(inst, &descs);

libvlc_renderer_discoverer_t *rd = libvlc_renderer_discoverer_new(inst, descs[0]->psz_name);

// Listen for renderer items
libvlc_event_manager_t *em = libvlc_renderer_discoverer_event_manager(rd);
libvlc_event_attach(em, libvlc_RendererDiscovererItemAdded, on_renderer_found, ctx);

libvlc_renderer_discoverer_start(rd);

// When renderer found:
void on_renderer_found(const libvlc_event_t *e, void *data) {
    libvlc_renderer_item_t *item = e->u.renderer_discoverer_item_added.item;
    const char *name = libvlc_renderer_item_name(item);
    // Check: libvlc_renderer_item_flags(item) & LIBVLC_RENDERER_CAN_VIDEO
    // To cast: libvlc_media_player_set_renderer(mp, item);
    // To stop casting: libvlc_media_player_set_renderer(mp, NULL);
}

libvlc_renderer_discoverer_stop(rd);
libvlc_renderer_discoverer_release(rd);
```

**Renderer flags:** `LIBVLC_RENDERER_CAN_AUDIO` (0x0001), `LIBVLC_RENDERER_CAN_VIDEO` (0x0002)

### 3.10 VLM (Video LAN Manager) `[3.x]`

> **Note:** The VLM API is **removed in libvlc 4.x**. For server-side streaming in 4.x, use the sout (stream output) chain via `libvlc_media_add_option()` instead.

Server-side broadcast/VOD streaming management:

```c
// Add a broadcast
libvlc_vlm_add_broadcast(inst, "mystream",
    "file:///path/to/video.mp4",     // input
    "#standard{access=http,mux=ts,dst=:8080/stream}",  // output
    0, NULL,                          // extra options
    1,   // enabled
    0);  // no loop

libvlc_vlm_play_media(inst, "mystream");
libvlc_vlm_pause_media(inst, "mystream");
libvlc_vlm_stop_media(inst, "mystream");
libvlc_vlm_seek_media(inst, "mystream", 50.0);  // 50%

// VOD
libvlc_vlm_add_vod(inst, "myvod", "file:///path/to/video.mp4",
    0, NULL, 1, "ts");

// Query state
float pos = libvlc_vlm_get_media_instance_position(inst, "mystream", 0);
int time = libvlc_vlm_get_media_instance_time(inst, "mystream", 0);

// JSON info (debugging)
const char *info = libvlc_vlm_show_media(inst, "mystream");

libvlc_vlm_del_media(inst, "mystream");
libvlc_vlm_release(inst);
```

**VLM transcode with presets and progress tracking (from official DVD ripper sample):**

The VLM API can be used for transcoding with progress monitoring. Define sout transcode strings as presets and track position via polling.

```c
/* Transcode preset strings */
// MP4 high quality:
"#transcode{vcodec=h264,venc=x264{cfr=16},scale=1,acodec=mp4a,ab=160,"
"channels=2,samplerate=44100}:file{dst=/output.mp4}"

// MP4 low quality:
"#transcode{vcodec=h264,venc=x264{cfr=40},scale=1,acodec=mp4a,ab=96,"
"channels=2,samplerate=44100}:file{dst=/output.mp4}"

// OGG high quality (Theora + Vorbis):
"#transcode{vcodec=theo,venc=theora{quality=9},scale=1,acodec=vorb,ab=160,"
"channels=2,samplerate=44100}:file{dst=/output.ogg}"

// WebM high quality (VP8 + Vorbis):
"#transcode{vcodec=VP80,vb=2000,scale=1,acodec=vorb,ab=160,"
"channels=2,samplerate=44100}:file{dst=/output.webm}"

/* Start VLM broadcast and track progress */
libvlc_vlm_add_broadcast(inst, "transcode_job",
    "file:///input.mp4",  /* input */
    sout_string,          /* transcode preset from above */
    0, NULL, 1, 0);       /* enabled=1, loop=0 */
libvlc_vlm_play_media(inst, "transcode_job");

/* Monitor progress via VLM events */
libvlc_event_manager_t *em = libvlc_vlm_get_event_manager(inst);
libvlc_event_attach(em, libvlc_VlmMediaInstanceStatusEnd, on_done, NULL);
libvlc_event_attach(em, libvlc_VlmMediaInstanceStatusError, on_error, NULL);

/* Or poll position (0.0 to 1.0) for progress bars */
float pos = libvlc_vlm_get_media_instance_position(inst, "transcode_job", 0);
// pos < 0 means not started; 0.0-1.0 = progress; >= 1.0 = finished
```

### 3.11 Tracklist API (Player-side) `[4.x]`

In 4.x, track selection uses the new tracklist API instead of the `get_track`/`set_track`/`get_track_description` functions:

```c
// Get all audio tracks from the player
libvlc_media_tracklist_t *tl =
    libvlc_media_player_get_tracklist(mp, libvlc_track_audio, false);
    // selected=true to get only selected tracks

size_t count = libvlc_media_tracklist_count(tl);
for (size_t i = 0; i < count; i++) {
    libvlc_media_track_t *t = libvlc_media_tracklist_at(tl, i);
    printf("Track '%s': %s %s\n",
           t->psz_id,          // stable string identifier
           t->psz_name,        // human-readable name
           t->selected ? "(selected)" : "");
}
libvlc_media_tracklist_delete(tl);

// Select a track by reference
libvlc_media_player_select_track(mp, track);

// Select by string ID
libvlc_media_player_select_tracks_by_ids(mp, libvlc_track_audio, "audio/0,audio/1");

// Unselect all tracks of a type (e.g., disable all subtitles)
libvlc_media_player_unselect_track_type(mp, libvlc_track_text);

// Get the currently selected track of a type
libvlc_media_track_t *sel = libvlc_media_player_get_selected_track(mp, libvlc_track_video);
if (sel) {
    printf("Selected: %s\n", sel->psz_id);
    libvlc_media_track_release(sel);  // must release
}

// Get a specific track by ID
libvlc_media_track_t *t = libvlc_media_player_get_track_from_id(mp, "audio/1");
if (t) {
    // use t...
    libvlc_media_track_release(t);
}
```

### 3.12 Program API `[4.x]`

For MPEG-TS and multi-program streams, 4.x adds a dedicated program selection API:

```c
// Get the program list
libvlc_player_programlist_t *pl = libvlc_media_player_get_programlist(mp);
size_t count = libvlc_player_programlist_count(pl);
for (size_t i = 0; i < count; i++) {
    const libvlc_player_program_t *prog = libvlc_player_programlist_at(pl, i);
    printf("Program %d: '%s' %s\n",
           prog->i_group_id, prog->psz_name,
           prog->b_selected ? "(selected)" : "");
    // prog->b_scrambled — whether scrambled
}
libvlc_player_programlist_delete(pl);

// Select a program by ID
libvlc_media_player_select_program_id(mp, group_id);

// Get selected/specific program (must release with libvlc_player_program_delete)
libvlc_player_program_t *prog = libvlc_media_player_get_selected_program(mp);
// libvlc_player_program_t *prog = libvlc_media_player_get_program_from_id(mp, id);
if (prog) {
    printf("Selected program: %s\n", prog->psz_name);
    libvlc_player_program_delete(prog);
}
```

Listen for `libvlc_MediaPlayerProgramAdded/Deleted/Updated/Selected` events.

### 3.13 GPU Rendering Pipeline `[4.x]`

LibVLC 4.x introduces GPU-accelerated video output via `libvlc_video_set_output_callbacks()`. Instead of receiving CPU pixel buffers (the 3.x `vmem` approach), the application provides GPU resources directly.

**Supported engines:**

| Engine | Enum | Platform |
|--------|------|----------|
| OpenGL | `libvlc_video_engine_opengl` | Linux, macOS |
| OpenGL ES 2 | `libvlc_video_engine_gles2` | Android, embedded |
| Direct3D 11 | `libvlc_video_engine_d3d11` | Windows |
| Direct3D 9 | `libvlc_video_engine_d3d9` | Windows (legacy) |
| Android Native Window | `libvlc_video_engine_anw` | Android (via ANativeWindow) |
| Disable | `libvlc_video_engine_disable` | No video output |

```c
// Set up GPU rendering (D3D11 example)
bool setup(void **opaque, const libvlc_video_setup_device_cfg_t *cfg,
           libvlc_video_setup_device_info_t *out) {
    // cfg->hardware_decoding: true if hardware decoding is requested
    // Set up your D3D11 device, return context in *opaque
    out->d3d11.device_context = my_d3d11_context;
    return true;
}

void cleanup(void *opaque) { /* Release GPU resources */ }

bool update_output(void *opaque, const libvlc_video_render_cfg_t *cfg,
                   libvlc_video_output_cfg_t *out) {
    // cfg->width, cfg->height — requested size
    // cfg->colorspace, cfg->primaries, cfg->transfer — color info
    // out->dxgi_format, out->d3d11_format — set output format
    // out->orientation — set orientation
    return true;
}

void swap(void *opaque) { /* Present frame to display */ }

libvlc_video_set_output_callbacks(mp,
    libvlc_video_engine_d3d11,
    setup, cleanup, NULL /*window_cb*/,
    update_output, swap,
    NULL /*makeCurrent*/, NULL /*getProcAddress*/,
    NULL /*metadata*/, NULL /*select_plane*/,
    my_opaque);
```

**Key concepts:**
- `update_output` is called when video size/format changes — resize your swap chain here
- `swap` is called each time a frame is ready to display
- For OpenGL: provide `makeCurrent` and `getProcAddress` callbacks
- For Android: use the helper `libvlc_video_set_anw_callbacks()` instead
- HDR metadata available via `libvlc_video_frame_hdr10_metadata_t` in the metadata callback
- Color space info: `libvlc_video_color_space_t`, `libvlc_video_color_primaries_t`, `libvlc_video_transfer_func_t`

### 3.14 A-B Loop API `[4.x]`

```c
// Set A-B loop by time (both points at once)
libvlc_media_player_set_abloop_time(mp, a_time_ms, b_time_ms);

// Or by position (0.0–1.0)
libvlc_media_player_set_abloop_position(mp, 0.1, 0.5);

// Query current loop state
libvlc_time_t a_time, b_time;
double a_pos, b_pos;
libvlc_abloop_t state = libvlc_media_player_get_abloop(mp, &a_time, &a_pos, &b_time, &b_pos);
// state: libvlc_abloop_none, libvlc_abloop_a, libvlc_abloop_b

// Clear loop
libvlc_media_player_reset_abloop(mp);
```

### 3.15 Picture API `[4.x]`

The `libvlc_picture_t` type represents an image (thumbnail, artwork) with reference counting:

```c
// Received from MediaThumbnailGenerated event or thumbnail request
libvlc_picture_t *pic = event->u.media_thumbnail_generated.p_thumbnail;
libvlc_picture_retain(pic);  // hold beyond event scope

// Properties
unsigned w = libvlc_picture_get_width(pic);
unsigned h = libvlc_picture_get_height(pic);
libvlc_picture_type_t type = libvlc_picture_type(pic);
// Types: libvlc_picture_Argb, _Png, _Jpg, _WebP, _Rgba
libvlc_time_t time = libvlc_picture_get_time(pic);  // ms

// Get raw buffer
size_t buf_size;
const unsigned char *buf = libvlc_picture_get_buffer(pic, &buf_size);
// For Argb/Rgba types: stride = libvlc_picture_get_stride(pic)

// Save to file
libvlc_picture_save(pic, "/path/to/output.png");

libvlc_picture_release(pic);

// Picture list (e.g., from attached thumbnails)
size_t count = libvlc_picture_list_count(list);
libvlc_picture_t *p = libvlc_picture_list_at(list, 0);
libvlc_picture_list_destroy(list);
```

---

## §4. Language Binding Patterns

### 4.1 C# — LibVLCSharp (Official, Cross-platform)

> **Targets LibVLCSharp 3.x** (NuGet `LibVLCSharp` 3.x + `VideoLAN.LibVLC.*` 3.x) for the 3.x examples below. The master branch of LibVLCSharp targets libvlc 4.x with a different API surface (e.g., `MediaConfiguration`, new rendering APIs, async stop).

**Package:** `VideoLAN.LibVLC.Forms` (Xamarin), `LibVLCSharp` (core), `VideoLAN.LibVLC.Windows/Mac/...` (platform-specific)

**Type Mapping:**

| C Type | C# Type | Notes |
|--------|---------|-------|
| `libvlc_instance_t` | `LibVLC` | Constructor takes `params string[]` CLI args |
| `libvlc_media_player_t` | `MediaPlayer` | Created from `LibVLC` or `Media` |
| `libvlc_media_t` | `Media` | Created from `LibVLC` + URI/path/stream |
| `libvlc_media_list_t` | `MediaList` | Implements `IEnumerable` |
| `libvlc_event_manager_t` | C# events | `.Playing += handler` |
| `libvlc_renderer_item_t` | `RendererItem` | |
| `libvlc_equalizer_t` | `Equalizer` | |

**Initialization:**
```csharp
// Load native libvlc (does plugin scan — can be slow first time)
using var libVLC = new LibVLC(enableDebugLogs: true);
// Or with args:
using var libVLC = new LibVLC("--verbose=2", "--no-video-title-show");
```

**Basic Playback:**
```csharp
using var libVLC = new LibVLC();
using var media = new Media(libVLC, new Uri("https://example.com/video.mp4"));
using var player = new MediaPlayer(media);
player.Play();
```

**Events:**
```csharp
player.Playing += (sender, e) => Console.WriteLine("Playing!");
player.EndReached += (sender, e) => {
    // MUST offload — never call libvlc from callback thread
    ThreadPool.QueueUserWorkItem(_ => player.Play(nextMedia));
};
player.EncounteredError += (sender, e) => Console.WriteLine("Error!");
player.TimeChanged += (sender, e) => Console.WriteLine($"Time: {e.Time}ms");
player.Buffering += (sender, e) => Console.WriteLine($"Buffering: {e.Cache}%");
```

**Media Parsing:**
```csharp
using var media = new Media(libVLC, new Uri("file:///path/to/file.mp4"));
await media.Parse(MediaParseOptions.ParseLocal);
// media.Tracks, media.Meta(MetadataType.Title), media.Duration
```

**YouTube / m3u8 / Playlists (network-parsed media with sub-items):**

LibVLC can resolve playlist-like URLs (YouTube, HLS manifests) by parsing them over the network. The actual playable stream is exposed as the first sub-item:

```csharp
using var media = new Media(libVLC, new Uri("https://youtube.com/watch?v=..."));
await media.Parse(MediaParseOptions.ParseNetwork);
// The resolved stream URL is in SubItems — play the first one:
player.Play(media.SubItems.First());
```

This works for any URL where libvlc resolves the actual stream via network parsing (YouTube, Dailymotion, some m3u8 playlists, etc.). Always parse with `ParseNetwork` and check `SubItems` rather than playing the original URL directly.

**Custom Stream Input (imem):**
```csharp
using var media = new Media(libVLC, new StreamMediaInput(myStream));
player.Play(new MediaPlayer(media));
```

**Platform VideoView:**

| Platform | Control | Embedding |
|----------|---------|-----------|
| WPF | `VideoView` (wraps `WindowsFormsHost`) | XAML: `<vlc:VideoView />` |
| WinForms | `VideoView` | Direct control |
| UWP | `VideoView` (SwapChainPanel) | Requires `"--aout=winstore"` |
| macOS/iOS | `VideoView` (NSView/UIView) | `player.SetNSObject(view.Handle)` |
| Android | `VideoView` (SurfaceView) | `player.SetAndroidContext(...)` |
| GTK | `VideoView` | DrawingArea |
| Avalonia | `VideoView` | NativeControlHost |

**IDisposable — CRITICAL:** All main types (`LibVLC`, `MediaPlayer`, `Media`, `MediaList`) implement `IDisposable`. Always `using` or `.Dispose()`. Events are native callbacks — **unsubscribe before disposal** to prevent both managed and native memory leaks.

### 4.2 Python — python-vlc

**Package:** `pip install python-vlc`

**Type Mapping:**

| C Type | Python Type | Notes |
|--------|-------------|-------|
| `libvlc_instance_t` | `vlc.Instance` | Constructor takes string or list of args |
| `libvlc_media_player_t` | `vlc.MediaPlayer` | Auto-creates default Instance if not provided |
| `libvlc_media_t` | `vlc.Media` | |
| `libvlc_media_list_t` | `vlc.MediaList` | |
| `libvlc_event_manager_t` | `vlc.EventManager` | |

**Quickstart (Implicit Instance):**
```python
import vlc

player = vlc.MediaPlayer("file:///path/to/video.mp4")
player.play()

# Wait for playback
import time
time.sleep(10)
```

**Explicit Instance (Recommended):**
```python
import vlc

instance = vlc.Instance('--no-audio', '--verbose=2')
player = instance.media_player_new()

media = instance.media_new('/path/to/file.mp4')
player.set_media(media)

# Embed in window (Linux/X11)
player.set_xwindow(window_id)
# Windows: player.set_hwnd(hwnd)
# macOS: player.set_nsobject(nsview_ptr)

player.play()
```

**Events:**
```python
import vlc
import queue

cmd_queue = queue.Queue()

def on_end(event):
    # DO NOT call player methods here — deadlock!
    cmd_queue.put('ended')

def on_position(event):
    cmd_queue.put(('pos', event.u.new_position))

player = vlc.MediaPlayer("file.mp4")
em = player.event_manager()
em.event_attach(vlc.EventType.MediaPlayerEndReached, on_end)
em.event_attach(vlc.EventType.MediaPlayerPositionChanged, on_position)

player.play()

# Process events in main thread
while True:
    try:
        msg = cmd_queue.get(timeout=0.1)
        if msg == 'ended':
            break
    except queue.Empty:
        pass
```

**Media Options:**
```python
media = instance.media_new('file.mp4', 'network-caching=1000')
# Or:
media = instance.media_new('file.mp4')
media.add_option(':sout=#transcode{vcodec=h264}:std{access=file,dst=out.mp4}')
```

**Custom Callbacks (in-memory stream):**
```python
import vlc
import ctypes

@vlc.CallbackDecorators.MediaOpenCb
def open_cb(opaque, data_pointer, size_pointer):
    size_pointer.value = 2**64 - 1
    return 0

@vlc.CallbackDecorators.MediaReadCb
def read_cb(opaque, buffer, length):
    data = get_next_chunk()
    buf = ctypes.cast(buffer, ctypes.POINTER(ctypes.c_char * len(data)))
    for i, b in enumerate(data):
        buf.contents[i] = ctypes.c_char(b)
    return len(data)

@vlc.CallbackDecorators.MediaCloseCb
def close_cb(opaque):
    pass

media = instance.media_new_callbacks(open_cb, read_cb, None, close_cb, None)
```

**Python Gotchas:**
1. **Keep references alive** — if Python GC collects a wrapper, the C pointer becomes invalid. Always assign to variables, not inline.
2. **Callbacks run on libvlc thread** — not Python main thread. Use `queue.Queue` to communicate.
3. **String encoding** — auto UTF-8 conversion. Non-ASCII paths work.
4. **`event_manager()` reference** — keep the EventManager reference alive, or callbacks stop working.

### 4.3 Java/Kotlin — vlcj

> **Targets vlcj 4.x** (which wraps libvlc 3.x — note: vlcj version numbers differ from libvlc version numbers).

**Package:** `uk.co.caprica:vlcj:4.x` (Maven Central)

**Type Mapping:**

| C Type | vlcj Type | Notes |
|--------|-----------|-------|
| `libvlc_instance_t` | `MediaPlayerFactory` | Auto-discovers libvlc |
| `libvlc_media_player_t` | `MediaPlayer` / `EmbeddedMediaPlayer` | |
| `libvlc_media_t` | `Media` | |
| `libvlc_event_manager_t` | `MediaPlayerEventAdapter` / listeners | |

**Initialization (vlcj 4.x — auto-discovery):**
```java
MediaPlayerFactory factory = new MediaPlayerFactory();
// Or with args:
MediaPlayerFactory factory = new MediaPlayerFactory("--verbose=2", "--no-video-title-show");
```

**Basic Playback (Swing):**
```java
EmbeddedMediaPlayerComponent component = new EmbeddedMediaPlayerComponent();
frame.setContentPane(component);
frame.setVisible(true);

component.mediaPlayer().media().play("/path/to/video.mp4");
```

**Fluent API (module pattern):**
```java
MediaPlayer mp = component.mediaPlayer();
mp.controls().play();
mp.controls().pause();
mp.controls().stop();
mp.controls().setPosition(0.5f);
mp.audio().setVolume(80);
mp.video().setAspectRatio("16:9");
mp.media().play(mrl, ":network-caching=1000");
long time = mp.status().time();
```

**Events:**
```java
mp.events().addMediaPlayerEventListener(new MediaPlayerEventAdapter() {
    @Override
    public void playing(MediaPlayer mediaPlayer) {
        // On native thread — marshal to EDT for UI updates
        SwingUtilities.invokeLater(() -> statusLabel.setText("Playing"));
    }

    @Override
    public void finished(MediaPlayer mediaPlayer) {
        // NEVER call libvlc directly — use submit()
        mediaPlayer.submit(() -> mediaPlayer.media().play(nextMrl));
    }

    @Override
    public void error(MediaPlayer mediaPlayer) {
        SwingUtilities.invokeLater(() ->
            JOptionPane.showMessageDialog(frame, "Playback error"));
    }
});
```

**Direct/Callback Rendering (for JavaFX, OpenGL):**
```java
CallbackMediaPlayerComponent component = new CallbackMediaPlayerComponent();
// Renders via BufferedImage — suitable for JavaFX ImageView
```

**Cleanup — CRITICAL:**
```java
// Must release native resources explicitly
component.release();
factory.release();
```

**vlcj Gotchas:**
1. **GC crashes** — keep hard references to all vlcj objects. Local variables go out of scope → native thread outlives Java object → JVM crash.
2. **macOS Java 7+** — no heavyweight AWT. Use JavaFX or `CallbackMediaPlayerComponent`.
3. **`play()` is async** — returns immediately. Success/failure reported via events.
4. **Thread safety** — events fire on native callback thread. Use `submit()` for libvlc calls, `SwingUtilities.invokeLater()` for UI.

### 4.4 Go — libvlc-go

**Package:** `github.com/adrg/libvlc-go/v3`

**Type Mapping:**

| C Type | Go Type | Notes |
|--------|---------|-------|
| `libvlc_instance_t` | module-level (via `vlc.Init()`) | Global singleton |
| `libvlc_media_player_t` | `vlc.Player` | |
| `libvlc_media_t` | `vlc.Media` | |
| `libvlc_event_manager_t` | `vlc.EventManager` | |

**Usage:**
```go
package main

import (
    "log"
    vlc "github.com/adrg/libvlc-go/v3"
)

func main() {
    // Initialize (global, call once)
    if err := vlc.Init("--quiet"); err != nil {
        log.Fatal(err)
    }
    defer vlc.Release()

    // Create player
    player, err := vlc.NewPlayer()
    if err != nil {
        log.Fatal(err)
    }
    defer func() { player.Stop(); player.Release() }()

    // Load media
    media, err := player.LoadMediaFromPath("/path/to/file.mp4")
    if err != nil {
        log.Fatal(err)
    }
    defer media.Release()

    // Events
    manager, err := player.EventManager()
    if err != nil {
        log.Fatal(err)
    }

    eventID, err := manager.Attach(vlc.MediaPlayerEndReached, func(event vlc.Event, userData interface{}) {
        log.Println("Playback ended")
    }, nil)
    if err != nil {
        log.Fatal(err)
    }
    defer manager.Detach(eventID)

    // Play
    if err := player.Play(); err != nil {
        log.Fatal(err)
    }

    // Block main goroutine
    select {}
}
```

**Go Gotchas:**
1. **CGo overhead** — each libvlc call crosses the CGo boundary. Minimize calls in hot paths.
2. **Global init** — `vlc.Init()` must be called once; `vlc.Release()` at shutdown.
3. **Error returns** — Go-idiomatic `(result, error)` pattern. Always check errors.
4. **Event callbacks** — fire on libvlc thread via CGo. Safe to use goroutines for follow-up work.

### 4.5 C++ — libvlcpp (Header-only)

**Type Mapping:**

| C Type | C++ Type | Notes |
|--------|----------|-------|
| `libvlc_instance_t` | `VLC::Instance` | RAII, shared_ptr-based |
| `libvlc_media_player_t` | `VLC::MediaPlayer` | |
| `libvlc_media_t` | `VLC::Media` | |
| `libvlc_media_list_t` | `VLC::MediaList` | |
| `libvlc_event_manager_t` | `VLC::EventManager` | |

**Usage:**
```cpp
#include <vlcpp/vlc.hpp>

auto instance = VLC::Instance(0, nullptr);
auto media = VLC::Media(instance, "/path/to/file.mp4", VLC::Media::FromPath);
auto player = VLC::MediaPlayer(media);

// Events (lambda-based)
player.eventManager().onPlaying([&]() {
    std::cout << "Playing!" << std::endl;
});

player.eventManager().onEndReached([&]() {
    // Still must not call libvlc directly — use async dispatch
    std::async(std::launch::async, [&]() { player.play(); });
});

player.play();
```

**C++ Features:**
- **RAII** — automatic cleanup via destructors (no manual retain/release)
- **Shared ownership** — internal `std::shared_ptr` wrapping
- **Lambda events** — `eventManager().onXxx(lambda)`
- **Type-safe** — wraps all C enums and types

### 4.6 Other Language Bindings

**VB.NET (LibVLCSharp):**

LibVLCSharp works with any .NET language, including Visual Basic:

```vb
Imports LibVLCSharp.Shared

Module Program
    Sub Main(args As String())
        Core.Initialize()
        Using libVLC = New LibVLC()
            Dim video = New Media(libVLC, New Uri("http://example.com/video.mp4"))
            Using mp = New MediaPlayer(video)
                video.Dispose()
                mp.Play()
                Console.ReadKey()
            End Using
        End Using
    End Sub
End Module
```

**PHP (via PeachPie — experimental):**

LibVLCSharp can be used from PHP through the [PeachPie](https://www.peachpie.io/) PHP-to-.NET compiler:

```php
<?php
use LibVLCSharp\Shared\Core;
use LibVLCSharp\Shared\LibVLC;
use LibVLCSharp\Shared\MediaPlayer;
use LibVLCSharp\Shared\Media;

Core::Initialize();
$libVLC = new LibVLC();
$mediaPlayer = new MediaPlayer($libVLC);
$media = new Media($libVLC, "http://example.com/video.mp4", 1);
$mediaPlayer->Play($media);
```

These demonstrate that LibVLCSharp is not limited to C# — any .NET-compatible language can use it with the same API surface.

### 4.7 Binding Cross-Reference

**"Play a file" across all languages:**

| Language | Code |
|----------|------|
| C | `m = libvlc_media_new_path(inst, path); libvlc_media_player_set_media(mp, m); libvlc_media_player_play(mp);` |
| C# | `player.Play(new Media(libVLC, path, FromType.FromPath));` |
| Python | `player = vlc.MediaPlayer(path); player.play()` |
| Java | `component.mediaPlayer().media().play(path);` |
| Go | `media, _ := player.LoadMediaFromPath(path); player.Play()` |
| C++ | `auto m = VLC::Media(inst, path, VLC::Media::FromPath); player.setMedia(m); player.play();` |

---

## §5. Common Workflows

### 5.1 Play a Local File

```c
// [3.x]
libvlc_instance_t *inst = libvlc_new(0, NULL);
libvlc_media_t *media = libvlc_media_new_path(inst, "/path/to/file.mp4");
libvlc_media_player_t *mp = libvlc_media_player_new_from_media(media);
libvlc_media_release(media);
libvlc_media_player_play(mp);
// ... wait or handle events ...
libvlc_media_player_stop(mp);
libvlc_media_player_release(mp);
libvlc_release(inst);
```

```c
// [4.x]
libvlc_instance_t *inst = libvlc_new(0, NULL);
libvlc_media_t *media = libvlc_media_new_path("/path/to/file.mp4");        // no inst
libvlc_media_player_t *mp = libvlc_media_player_new_from_media(inst, media); // inst first
libvlc_media_release(media);
libvlc_media_player_play(mp);
// ... wait or handle events ...
libvlc_media_player_stop_async(mp);  // async
libvlc_media_player_release(mp);
libvlc_release(inst);
```

### 5.2 Play a Network Stream

```c
// [3.x]
libvlc_media_t *media = libvlc_media_new_location(inst, "https://example.com/stream.m3u8");
// [4.x]
// libvlc_media_t *media = libvlc_media_new_location("https://example.com/stream.m3u8");
libvlc_media_add_option(media, ":network-caching=1000");
// Same as local file from here
```

### 5.3 Get Media Metadata

```c
// [3.x]
libvlc_media_t *media = libvlc_media_new_path(inst, path);
libvlc_media_parse_with_options(media, libvlc_media_parse_local | libvlc_media_fetch_local, 5000);
// Wait for parsing (event or poll):
while (libvlc_media_get_parsed_status(media) != libvlc_media_parsed_status_done)
    usleep(100000);

char *title = libvlc_media_get_meta(media, libvlc_meta_Title);
char *artist = libvlc_media_get_meta(media, libvlc_meta_Artist);
int64_t duration = libvlc_media_get_duration(media);  // ms

libvlc_media_track_t **tracks;
unsigned n = libvlc_media_tracks_get(media, &tracks);
// ... inspect tracks ...
libvlc_media_tracks_release(tracks, n);

if (title) libvlc_free(title);
if (artist) libvlc_free(artist);
libvlc_media_release(media);
```

```c
// [4.x]
libvlc_media_t *media = libvlc_media_new_path(path);         // no inst
libvlc_media_parse_request(inst, media,                       // inst required here
    libvlc_media_parse_local | libvlc_media_fetch_local, 5000);
while (libvlc_media_get_parsed_status(media) != libvlc_media_parsed_status_done)
    usleep(100000);

char *title = libvlc_media_get_meta(media, libvlc_meta_Title);
int64_t duration = libvlc_media_get_duration(media);

// Use tracklist API instead of tracks_get
libvlc_media_tracklist_t *tl = libvlc_media_get_tracklist(media, libvlc_track_video);
for (size_t i = 0; i < libvlc_media_tracklist_count(tl); i++) {
    libvlc_media_track_t *t = libvlc_media_tracklist_at(tl, i);
    printf("Video: %dx%d codec=%s\n", t->video->i_width, t->video->i_height,
           libvlc_media_get_codec_description(t->i_type, t->i_codec));
}
libvlc_media_tracklist_delete(tl);

if (title) libvlc_free(title);
libvlc_media_release(media);
```

### 5.4 Extract Thumbnail / Screenshot

**Method 1: Event-based snapshot (recommended, from official `vlc-thumb.c`):**

Seek to 30% position, wait for the seek to complete via events, then take a snapshot. Uses pthread synchronization with a timeout to avoid hanging on broken files.

```c
#include <vlc/vlc.h>
#include <pthread.h>
#include <time.h>

#define THUMBNAIL_POSITION  0.30f   /* 30% into the video */
#define THUMBNAIL_TIMEOUT   5       /* seconds */

static pthread_mutex_t lock = PTHREAD_MUTEX_INITIALIZER;
static pthread_cond_t  wait_cond;
static bool done;

static void callback(const libvlc_event_t *ev, void *param) {
    (void)param;
    pthread_mutex_lock(&lock);
    switch (ev->type) {
    case libvlc_MediaPlayerPositionChanged:
        if (ev->u.media_player_position_changed.new_position
                < THUMBNAIL_POSITION * 0.9f)
            break;  /* not there yet */
        /* fall through */
    case libvlc_MediaPlayerSnapshotTaken:
        done = true;
        pthread_cond_signal(&wait_cond);
        break;
    default:
        break;
    }
    pthread_mutex_unlock(&lock);
}

static int wait_with_timeout(const char *error_msg) {
    struct timespec ts;
    clock_gettime(CLOCK_MONOTONIC, &ts);
    ts.tv_sec += THUMBNAIL_TIMEOUT;

    pthread_mutex_lock(&lock);
    int ret = done ? 0 : pthread_cond_timedwait(&wait_cond, &lock, &ts);
    pthread_mutex_unlock(&lock);
    if (ret) fprintf(stderr, "%s (timeout)\n", error_msg);
    return ret;
}

int make_thumbnail(const char *input, const char *output_png, int width) {
    static const char *args[] = {
        "--intf", "dummy", "--vout", "dummy",
        "--no-audio", "--no-video-title-show",
        "--no-stats", "--no-sub-autodetect-file",
        "--no-snapshot-preview"
    };
    libvlc_instance_t *vlc = libvlc_new(sizeof(args)/sizeof(*args), args);
    libvlc_media_t *m = libvlc_media_new_path(vlc, input);
    libvlc_media_player_t *mp = libvlc_media_player_new_from_media(m);

    /* Initialize condition variable with monotonic clock */
    pthread_condattr_t attr;
    pthread_condattr_init(&attr);
    pthread_condattr_setclock(&attr, CLOCK_MONOTONIC);
    pthread_cond_init(&wait_cond, &attr);
    pthread_condattr_destroy(&attr);

    libvlc_media_player_play(mp);

    /* Step 1: Seek to position, wait via event */
    libvlc_event_manager_t *em = libvlc_media_player_event_manager(mp);
    libvlc_event_attach(em, libvlc_MediaPlayerPositionChanged, callback, NULL);
    done = false;
    libvlc_media_player_set_position(mp, THUMBNAIL_POSITION);
    int err = wait_with_timeout("Seek failed");
    libvlc_event_detach(em, libvlc_MediaPlayerPositionChanged, callback, NULL);

    if (!err) {
        /* Step 2: Take snapshot, wait for completion */
        libvlc_event_attach(em, libvlc_MediaPlayerSnapshotTaken, callback, NULL);
        done = false;
        libvlc_video_take_snapshot(mp, 0, output_png, width, 0);
        err = wait_with_timeout("Snapshot failed");
        libvlc_event_detach(em, libvlc_MediaPlayerSnapshotTaken, callback, NULL);
    }

    libvlc_media_player_stop(mp);
    libvlc_media_player_release(mp);
    libvlc_media_release(m);
    libvlc_release(vlc);
    pthread_cond_destroy(&wait_cond);
    return err;
}
```

**Key points:**
- Use `--vout=dummy` and `--no-audio` to suppress video/audio output (headless)
- `--no-snapshot-preview` prevents blending the snapshot into the dummy vout
- Always attach/detach events in pairs, and use timeouts to avoid hanging
- `PositionChanged` fires continuously during seeking; wait until within 90% of target before proceeding
- The output filename **must** end in `.png` (VLC uses the extension to detect format)

**Method 2: Video callbacks (headless, custom processing):**
```c
// Set up video callbacks (see §3.3) with desired resolution
// In display callback, save the first frame, then stop
libvlc_video_set_callbacks(mp, lock, unlock, display, ctx);
libvlc_video_set_format(mp, "RV32", 320, 240, 320 * 4);
libvlc_media_player_play(mp);
```

### 5.5 Build a Playlist

```c
libvlc_media_list_t *ml = libvlc_media_list_new(inst);
libvlc_media_list_player_t *mlp = libvlc_media_list_player_new(inst);
libvlc_media_player_t *mp = libvlc_media_player_new(inst);

libvlc_media_list_player_set_media_player(mlp, mp);

libvlc_media_list_lock(ml);
for (int i = 0; i < file_count; i++) {
    libvlc_media_t *m = libvlc_media_new_path(inst, files[i]);
    libvlc_media_list_add_media(ml, m);
    libvlc_media_release(m);
}
libvlc_media_list_unlock(ml);

libvlc_media_list_player_set_media_list(mlp, ml);
libvlc_media_list_player_set_playback_mode(mlp, libvlc_playback_mode_loop);
libvlc_media_list_player_play(mlp);
```

### 5.6 Cast to Chromecast

```c
// 1. Discover renderers
libvlc_renderer_discoverer_t *rd = libvlc_renderer_discoverer_new(inst, "microdns_renderer");
libvlc_event_manager_t *em = libvlc_renderer_discoverer_event_manager(rd);
libvlc_event_attach(em, libvlc_RendererDiscovererItemAdded, on_renderer, ctx);
libvlc_renderer_discoverer_start(rd);

// 2. In callback, save the renderer item
void on_renderer(const libvlc_event_t *e, void *data) {
    libvlc_renderer_item_t *item = e->u.renderer_discoverer_item_added.item;
    libvlc_renderer_item_hold(item);  // Retain
    // Store item for later use
}

// 3. Set renderer on player
libvlc_media_player_set_renderer(mp, chromecast_item);
libvlc_media_player_play(mp);

// 4. Stop casting
libvlc_media_player_set_renderer(mp, NULL);
```

**Chromecast-specific options:**
```c
// On the media:
libvlc_media_add_option(media, ":sout-chromecast-conversion-quality=2");
// Quality: 0=low, 1=medium, 2=high
```

### 5.7 Transcode and Save to File

```c
libvlc_media_t *media = libvlc_media_new_path(inst, "/input.avi");
libvlc_media_add_option(media,
    ":sout=#transcode{vcodec=h264,vb=800,acodec=mpga,ab=128,channels=2}"
    ":std{access=file,mux=mp4,dst=/output.mp4}");
libvlc_media_add_option(media, ":no-sout-all");  // Only stream the first track
libvlc_media_add_option(media, ":sout-keep");

libvlc_media_player_t *mp = libvlc_media_player_new_from_media(media);
libvlc_media_release(media);
libvlc_media_player_play(mp);
// Wait for EndReached event
```

**C# (LibVLCSharp) — Record an HLS stream to file:**
```csharp
using var libvlc = new LibVLC();
using var mediaPlayer = new MediaPlayer(libvlc);

libvlc.Log += (sender, e) => Console.WriteLine($"[{e.Level}] {e.Module}:{e.Message}");
mediaPlayer.EndReached += (sender, e) =>
    Console.WriteLine("Recording complete: " + destination);

var destination = Path.Combine(Directory.GetCurrentDirectory(), "record.ts");
using var media = new Media(libvlc,
    new Uri("http://example.com/stream.m3u8"),
    ":sout=#file{dst=" + destination + "}",
    ":sout-keep");

mediaPlayer.Play(media);
// Playback continues until the stream ends — EndReached fires when done
```

**Key points:**
- Use `#file{dst=...}` for passthrough recording (no transcoding) — preserves original codecs
- Use `#transcode{...}:std{access=file,...}` when codec conversion is needed
- `:sout-keep` keeps the sout chain alive across media changes
- `:no-sout-all` limits streaming to the first track of each type (avoids duplicate tracks)

### 5.8 Stream Over HTTP

```c
libvlc_media_t *media = libvlc_media_new_path(inst, "/input.mp4");
libvlc_media_add_option(media,
    ":sout=#transcode{vcodec=h264,acodec=mpga}:http{mux=ts,dst=:8080/stream}");

libvlc_media_player_t *mp = libvlc_media_player_new_from_media(media);
libvlc_media_release(media);
libvlc_media_player_play(mp);
// Stream available at http://localhost:8080/stream
```

### 5.9 Record/Capture from Camera

```c
// Linux (Video4Linux)
libvlc_media_t *media = libvlc_media_new_location(inst, "v4l2:///dev/video0");
libvlc_media_add_option(media, ":v4l2-width=640");
libvlc_media_add_option(media, ":v4l2-height=480");

// To save: add sout option
libvlc_media_add_option(media,
    ":sout=#transcode{vcodec=h264}:std{access=file,mux=mp4,dst=capture.mp4}");
```

### 5.10 Record the Screen

Capture the entire screen to a video file using the `screen://` access module. Works on Windows, macOS, and Linux.

**C:**
```c
libvlc_media_t *media = libvlc_media_new_location(inst, "screen://");
libvlc_media_add_option(media, ":screen-fps=24");
libvlc_media_add_option(media,
    ":sout=#transcode{vcodec=h264,vb=0,scale=0,acodec=mp4a,ab=128,"
    "channels=2,samplerate=44100}:file{dst=record.mp4}");
libvlc_media_add_option(media, ":sout-keep");

libvlc_media_player_t *mp = libvlc_media_player_new_from_media(media);
libvlc_media_release(media);
libvlc_media_player_play(mp);  // Start recording

// ... record for desired duration ...

libvlc_media_player_stop(mp);  // Stop recording and finalize file
libvlc_media_player_release(mp);
```

**C# (LibVLCSharp):**
```csharp
using var libvlc = new LibVLC();
using var mediaPlayer = new MediaPlayer(libvlc);
using var media = new Media(libvlc, "screen://", FromType.FromLocation);

media.AddOption(":screen-fps=24");
media.AddOption(":sout=#transcode{vcodec=h264,vb=0,scale=0,acodec=mp4a," +
    "ab=128,channels=2,samplerate=44100}:file{dst=record.mp4}");
media.AddOption(":sout-keep");

mediaPlayer.Play(media);       // Start recording
await Task.Delay(5000);        // Record for 5 seconds
mediaPlayer.Stop();            // Stop and save
```

**Key points:**
- `screen://` is a pseudo-MRL — it captures the entire primary display
- `vb=0,scale=0` in transcode means auto-bitrate and original resolution
- `:screen-fps=24` controls capture frame rate (higher = smoother but larger files)
- The file is only finalized when `Stop()` is called — ensure clean shutdown
- On Linux, requires X11 (Wayland support varies); on macOS, requires screen recording permission

### 5.11 Browse NAS / UPnP Shares

```c
// 1. Get UPnP media discoverer
libvlc_media_discoverer_t *md = libvlc_media_discoverer_new(inst, "upnp");
libvlc_media_discoverer_start(md);

// 2. Get discovered media list
libvlc_media_list_t *ml = libvlc_media_discoverer_media_list(md);

// 3. Each item is a directory or media
libvlc_media_list_lock(ml);
int count = libvlc_media_list_count(ml);
for (int i = 0; i < count; i++) {
    libvlc_media_t *m = libvlc_media_list_item_at_index(ml, i);
    libvlc_media_type_t type = libvlc_media_get_type(m);
    if (type == libvlc_media_type_directory) {
        // Browse sub-items: parse, then check subitems
        libvlc_media_parse_with_options(m, libvlc_media_parse_network, 5000);
        libvlc_media_list_t *sub = libvlc_media_subitems(m);
        // ... recurse ...
    }
    libvlc_media_release(m);
}
libvlc_media_list_unlock(ml);
```

**C# (LibVLCSharp) — Local Network Browser with MediaDiscoverer:**

Discover LAN services (UPnP, SMB shares) and browse directories. This pattern is used in media browser applications:

```csharp
var libVLC = new LibVLC("--verbose=2");
var mediaDiscoverers = new List<MediaDiscoverer>();

// Find all LAN-type discoverers and start them
foreach (var md in libVLC.MediaDiscoverers(MediaDiscovererCategory.Lan))
{
    var discoverer = new MediaDiscoverer(libVLC, md.Name);

    // Listen for discovered items (e.g., UPnP servers, SMB shares)
    discoverer.MediaList.ItemAdded += (sender, e) =>
        Console.WriteLine($"Found: {e.Media.Meta(MetadataType.Title)}");
    discoverer.MediaList.ItemDeleted += (sender, e) =>
        Console.WriteLine($"Lost: {e.Media.Meta(MetadataType.Title)}");

    mediaDiscoverers.Add(discoverer);
}

// Start discovery
foreach (var md in mediaDiscoverers)
    md.Start();

// Browse into a discovered directory
async Task BrowseDirectory(Media directoryMedia)
{
    // Parse to discover sub-items
    directoryMedia.SubItems.ItemAdded += (sender, e) =>
        Console.WriteLine($"  Sub-item: {e.Media.Meta(MetadataType.Title)}");

    await directoryMedia.Parse(MediaParseOptions.ParseNetwork);
    // Sub-items are now accessible via directoryMedia.SubItems
}
```

### 5.12 Select Audio, Video, and Subtitle Tracks

Track selection must happen **after** playback starts (tracks are discovered during demuxing). Wait for the `MediaPlayerPlaying` event or poll until tracks are available.

**C — Enumerate and select tracks `[3.x]`:**
```c
// [3.x] Wait until playing (tracks aren't available before playback starts)
// Then enumerate audio tracks:
libvlc_track_description_t *tracks = libvlc_audio_get_track_description(mp);
for (libvlc_track_description_t *t = tracks; t != NULL; t = t->p_next) {
    printf("Audio track %d: %s\n", t->i_id, t->psz_name);
}
libvlc_track_description_list_release(tracks);

// Select audio track by ID (i_id from description):
libvlc_audio_set_track(mp, track_id);

// Disable audio: set track to -1
libvlc_audio_set_track(mp, -1);

// Video tracks (same pattern):
libvlc_track_description_t *vtracks = libvlc_video_get_track_description(mp);
libvlc_video_set_track(mp, video_track_id);
libvlc_track_description_list_release(vtracks);

// Subtitle tracks:
libvlc_track_description_t *stracks = libvlc_video_get_spu_description(mp);
libvlc_video_set_spu(mp, subtitle_track_id);
libvlc_track_description_list_release(stracks);

// Disable subtitles:
libvlc_video_set_spu(mp, -1);

// Add external subtitle file at runtime:
libvlc_media_player_add_slave(mp, libvlc_media_slave_type_subtitle,
    "file:///path/to/subtitles.srt", true);

// Add external audio track at runtime:
libvlc_media_player_add_slave(mp, libvlc_media_slave_type_audio,
    "file:///path/to/audio.aac", true);

// Adjust subtitle/audio sync:
libvlc_video_set_spu_delay(mp, 500000);   // +500ms (microseconds)
libvlc_audio_set_delay(mp, -200000);       // -200ms
```

**C# (LibVLCSharp):**
```csharp
player.Playing += (s, e) =>
{
    // Audio tracks (0 = disable, 1+ = track index)
    foreach (var track in player.AudioTrackDescription)
        Console.WriteLine($"Audio {track.Id}: {track.Name}");
    player.SetAudioTrack(trackId);

    // Subtitle tracks (SPU)
    foreach (var track in player.SpuDescription)
        Console.WriteLine($"Sub {track.Id}: {track.Name}");
    player.SetSpu(trackId);
    player.SetSpu(-1);  // Disable subtitles

    // Add external subtitle
    player.AddSlave(MediaSlaveType.Subtitle, "file:///path/to/subs.srt", true);

    // Subtitle delay
    player.SetSpuDelay(500000);  // +500ms in microseconds
};
```

**Python:**
```python
import vlc, time

player = vlc.MediaPlayer("video.mkv")
player.play()
time.sleep(2)  # Wait for tracks to become available

# Audio tracks
for t in player.audio_get_track_description():
    print(f"Audio {t[0]}: {t[1]}")
player.audio_set_track(track_id)

# Subtitle tracks
for t in player.video_get_spu_description():
    print(f"Sub {t[0]}: {t[1]}")
player.video_set_spu(track_id)
player.video_set_spu(-1)  # Disable

# External subtitle
player.add_slave(vlc.MediaSlaveType.subtitle, "file:///path/to/subs.srt", True)
```

**`[4.x]` Track selection with tracklist API (C):**
```c
// Get all audio tracks
libvlc_media_tracklist_t *tl =
    libvlc_media_player_get_tracklist(mp, libvlc_track_audio, false);
for (size_t i = 0; i < libvlc_media_tracklist_count(tl); i++) {
    libvlc_media_track_t *t = libvlc_media_tracklist_at(tl, i);
    printf("Audio '%s': %s %s\n", t->psz_id, t->psz_name,
           t->selected ? "(selected)" : "");
}

// Select a specific track
libvlc_media_player_select_track(mp, libvlc_media_tracklist_at(tl, 1));
libvlc_media_tracklist_delete(tl);

// Disable all subtitles
libvlc_media_player_unselect_track_type(mp, libvlc_track_text);

// Select by string ID
libvlc_media_player_select_tracks_by_ids(mp, libvlc_track_audio, "audio/0");

// Add external subtitle (same in both versions)
libvlc_media_player_add_slave(mp, libvlc_media_slave_type_subtitle,
    "file:///path/to/subs.srt", true);
```

**Key points:**
- `[3.x]` Track IDs come from the `i_id` field of `libvlc_track_description_t`, NOT sequential indices
- `[3.x]` Track ID `-1` typically means "disable" (no audio / no subtitle)
- `[4.x]` Track IDs are strings (`psz_id`), e.g., `"audio/0"`, `"video/0"`, `"spu/0"`
- `[4.x]` Use `unselect_track_type()` to disable all tracks of a type
- `add_slave()` can add external subtitles or audio tracks **during playback** — the `select` parameter (`true`) auto-selects the new track
- Subtitle and audio delays are in **microseconds** and reset when media changes

### 5.13 Video Mosaic (Multiple Players)

Play multiple video streams simultaneously using separate `MediaPlayer` instances sharing a single `LibVLC` instance. Common for CCTV/surveillance dashboards or multi-camera views.

**C:**
```c
// Single instance, multiple players
libvlc_instance_t *inst = libvlc_new(0, NULL);

libvlc_media_player_t *players[4];
const char *urls[] = {
    "rtsp://camera1/stream", "rtsp://camera2/stream",
    "rtsp://camera3/stream", "rtsp://camera4/stream"
};

for (int i = 0; i < 4; i++) {
    players[i] = libvlc_media_player_new(inst);
    libvlc_media_player_set_hwnd(players[i], window_handles[i]);  // One window per player
    libvlc_media_t *m = libvlc_media_new_location(inst, urls[i]);
    libvlc_media_player_set_media(players[i], m);
    libvlc_media_release(m);
    libvlc_media_player_play(players[i]);
}

// Cleanup: stop and release each player, then release instance
```

**C# (LibVLCSharp / Xamarin.Forms) — RTSP Mosaic:**
```csharp
const string VideoUrl = "rtsp://camera/stream";
var libvlc = new LibVLC();

// Create separate MediaPlayer for each VideoView in the layout
VideoView0.MediaPlayer = new MediaPlayer(libvlc);
using (var media = new Media(libvlc, new Uri(VideoUrl)))
    VideoView0.MediaPlayer.Play(media);

VideoView1.MediaPlayer = new MediaPlayer(libvlc);
using (var media = new Media(libvlc, new Uri(VideoUrl)))
    VideoView1.MediaPlayer.Play(media);

// Repeat for VideoView2, VideoView3, etc.
```

**Key points:**
- **Always share a single `LibVLC` instance** — each `MediaPlayer` has its own decoder pipeline but shares plugin infrastructure
- Each `MediaPlayer` needs its own video output window/surface — never share a window between players
- For RTSP streams, set `:network-caching=1000` to buffer against network jitter
- On mobile, consider CPU/GPU limits — 4+ simultaneous HD streams may drop frames

### 5.14 Mobile Foreground/Background Lifecycle (Android)

On Android, the native video surface is released when the app goes to background. You must save playback state, tear down the `VideoView`, and recreate it when returning to foreground.

**C# (LibVLCSharp.Forms / Xamarin.Forms):**
```csharp
LibVLC _libVLC;
MediaPlayer _mediaPlayer;
float _position;

// When app goes to background (OnPause):
MessagingCenter.Subscribe<string>(this, "OnPause", app =>
{
    _mediaPlayer.Pause();
    _position = _mediaPlayer.Position;  // Save position (0.0–1.0)
    _mediaPlayer.Stop();
    MainGrid.Children.Clear();          // Remove VideoView (releases native surface)
});

// When app returns to foreground (OnRestart):
MessagingCenter.Subscribe<string>(this, "OnRestart", app =>
{
    var videoView = new VideoView {
        HorizontalOptions = LayoutOptions.FillAndExpand,
        VerticalOptions = LayoutOptions.FillAndExpand
    };
    MainGrid.Children.Add(videoView);   // Create fresh VideoView

    videoView.MediaPlayer = _mediaPlayer;
    _mediaPlayer.Position = _position;   // Restore position
    _position = 0;
    _mediaPlayer.Play();
});
```

**Key points:**
- On Android, the native libvlc video surface is destroyed when the app is paused/stopped — this is a platform behavior, not a bug
- **Save** `Position` (or `Time`) before `Stop()`, and **remove** the `VideoView` from the layout
- On resume, create a **new** `VideoView` and add it to the layout — reattach the existing `MediaPlayer`
- The `MediaPlayer` object itself survives backgrounding — only the view surface needs recreation
- iOS does not have this issue — `UIView` survives background transitions
- For MAUI/.NET 8+, use `Application.Current.Windows[0].Activated` / `Deactivated` instead of `MessagingCenter`

### 5.15 Gesture-Based Playback Control

Map touch/pan gestures to seeking and volume control. Horizontal swipes control time position, vertical swipes control volume.

**C# (Xamarin.Forms — PanGestureRecognizer):**
```csharp
long _finalTime;
int _finalVolume;
bool _timeChanged, _volumeChanged;

void OnGesture(PanUpdatedEventArgs e)
{
    switch (e.StatusType)
    {
        case GestureStatus.Running:
            if (Math.Abs(e.TotalX) > Math.Abs(e.TotalY))
            {
                // Horizontal swipe → seek
                var timeDiff = Convert.ToInt64(e.TotalX * 1000);  // ms
                _finalTime = MediaPlayer.Time + timeDiff;
                _timeChanged = true;
            }
            else
            {
                // Vertical swipe → volume (up = louder)
                var volume = (int)(MediaPlayer.Volume + e.TotalY * -1);
                _finalVolume = Math.Clamp(volume, 0, 200);
                _volumeChanged = true;
            }
            break;

        case GestureStatus.Completed:
            if (_timeChanged)
                MediaPlayer.Time = _finalTime;
            if (_volumeChanged)
                MediaPlayer.Volume = _finalVolume;
            _timeChanged = _volumeChanged = false;
            break;
    }
}
```

**Key points:**
- Apply time/volume changes on `GestureStatus.Completed`, not `Running` — avoids excessive libvlc calls during the drag
- Volume range is 0–200 (100 = normal, >100 = amplification)
- `Time` is in milliseconds — multiply gesture distance by a scaling factor for natural feel
- This pattern works for 360° video too — map gestures to `UpdateViewpoint()` yaw/pitch instead

### 5.16 Hardware-Accelerated Playback (EnableHardwareDecoding)

Enable platform-specific hardware decoding for better performance and lower CPU usage.

**C:**
```c
const char *args[] = {"--avcodec-hw=any"};  // auto-select best HW decoder
libvlc_instance_t *inst = libvlc_new(1, args);
// Hardware decoding options: "any", "none", "d3d11va" (Win), "vaapi" (Linux),
// "videotoolbox" (macOS/iOS), "mediacodec" (Android)
```

**C# (LibVLCSharp):**
```csharp
var media = new Media(LibVLC,
    new Uri("http://example.com/video.mp4"));
var mediaPlayer = new MediaPlayer(media) { EnableHardwareDecoding = true };
media.Dispose();
mediaPlayer.Play();
```

**Key points:**
- `EnableHardwareDecoding = true` maps to `--avcodec-hw=any` in libvlc
- Hardware decoding reduces CPU usage significantly for H.264/H.265 content
- Falls back to software decoding automatically if hardware decoder is unavailable
- On Android, uses MediaCodec; on iOS/macOS, uses VideoToolbox; on Windows, uses D3D11VA or DXVA2
- If you see green/corrupt frames, try disabling hardware decoding as a diagnostic step (see §8.2)

### 5.17 Audio-Only Playback (Music Player)

Build a music player by disabling video output. Reduces resource usage and works headless.

**C# (LibVLCSharp) — Audio service with event-driven UI updates:**
```csharp
var libVLC = new LibVLC();
var mediaPlayer = new MediaPlayer(libVLC);

// Create media with video disabled
using var media = new Media(libVLC,
    new Uri("https://example.com/song.mp4"), ":no-video");
mediaPlayer.Media = media;

// Subscribe to playback events for UI updates
mediaPlayer.TimeChanged += (s, e) => UpdateTimeDisplay(e.Time);
mediaPlayer.PositionChanged += (s, e) => UpdateSeekBar(e.Position);
mediaPlayer.LengthChanged += (s, e) => UpdateDuration(e.Length);
mediaPlayer.EndReached += (s, e) => OnTrackFinished();
mediaPlayer.Playing += (s, e) => ShowPlayingState();
mediaPlayer.Paused += (s, e) => ShowPausedState();

mediaPlayer.Play();

// Seeking: offset by milliseconds
mediaPlayer.Time += 5000;   // Forward 5 seconds
mediaPlayer.Time -= 5000;   // Rewind 5 seconds
```

**Key points:**
- `:no-video` prevents video decoding entirely — not just hiding the output
- `TimeChanged` and `PositionChanged` fire frequently during playback — use them for scrubber/progress UI
- `LengthChanged` fires once the duration is known (may not be immediate for streams)
- Remember: never call libvlc from event callbacks — offload to UI thread

---

## §6. Platform Integration

### 6.1 Windows

**WPF:**
```csharp
// LibVLCSharp.WPF — uses WindowsFormsHost (airspace limitation)
<vlc:VideoView x:Name="VideoView" />

// Code-behind:
VideoView.MediaPlayer = new MediaPlayer(libVLC);
VideoView.MediaPlayer.Play(media);
```

**WinForms:**
```csharp
// Direct handle access
var videoView = new VideoView();
videoView.MediaPlayer = new MediaPlayer(libVLC);
videoView.MediaPlayer.Play(media);
```

**UWP:**
```csharp
// Requires SwapChainPanel + special options
using var libVLC = new LibVLC("--aout=winstore");
```

**Win32 (C) — Full player with drag-and-drop and aspect ratio control:**

Based on the official VLC sample (`doc/libvlc/win_player.c`). Key points: use `WS_CLIPCHILDREN` on the parent window to prevent GDI from painting over the video surface, and use `DragAcceptFiles` for drag-and-drop media loading.

```c
#include <windows.h>
#include <vlc/vlc.h>

struct vlc_context {
    libvlc_instance_t     *p_libvlc;
    libvlc_media_player_t *p_mediaplayer;
};

static LRESULT CALLBACK WindowProc(HWND hWnd, UINT message,
                                   WPARAM wParam, LPARAM lParam)
{
    if (message == WM_CREATE) {
        CREATESTRUCT *c = (CREATESTRUCT *)lParam;
        SetWindowLongPtr(hWnd, GWLP_USERDATA, (LONG_PTR)c->lpCreateParams);
        return 0;
    }

    LONG_PTR p_user_data = GetWindowLongPtr(hWnd, GWLP_USERDATA);
    if (p_user_data == 0)
        return DefWindowProc(hWnd, message, wParam, lParam);
    struct vlc_context *ctx = (struct vlc_context *)p_user_data;

    switch (message) {
        case WM_DESTROY:
            PostQuitMessage(0);
            return 0;

        case WM_DROPFILES: {
            HDROP hDrop = (HDROP)wParam;
            char file_path[MAX_PATH];
            libvlc_media_player_stop(ctx->p_mediaplayer);

            if (DragQueryFile(hDrop, 0, file_path, sizeof(file_path))) {
                libvlc_media_t *p_media = libvlc_media_new_path(
                    ctx->p_libvlc, file_path);
                libvlc_media_t *p_old = libvlc_media_player_get_media(
                    ctx->p_mediaplayer);
                libvlc_media_player_set_media(ctx->p_mediaplayer, p_media);
                libvlc_media_release(p_old);
                libvlc_media_player_play(ctx->p_mediaplayer);
            }
            DragFinish(hDrop);
            return 0;
        }

        case WM_KEYDOWN:
            if (tolower(MapVirtualKey((UINT)wParam, 2)) == 's')
                libvlc_media_player_stop(ctx->p_mediaplayer);
            break;
    }
    return DefWindowProc(hWnd, message, wParam, lParam);
}

int WINAPI WinMain(HINSTANCE hInstance, HINSTANCE hPrevInstance,
                   LPSTR lpCmdLine, int nCmdShow)
{
    struct vlc_context Context;
    Context.p_libvlc = libvlc_new(0, NULL);

    libvlc_media_t *p_media = libvlc_media_new_path(
        Context.p_libvlc, lpCmdLine);
    Context.p_mediaplayer = libvlc_media_player_new_from_media(p_media);

    WNDCLASSEX wc = {0};
    wc.cbSize = sizeof(WNDCLASSEX);
    wc.style = CS_HREDRAW | CS_VREDRAW;
    wc.lpfnWndProc = WindowProc;
    wc.hInstance = hInstance;
    wc.hCursor = LoadCursor(NULL, IDC_ARROW);
    wc.lpszClassName = "VLCPlayerClass";
    RegisterClassEx(&wc);

    /* WS_CLIPCHILDREN is REQUIRED — prevents GDI from overpainting the video */
    HWND hWnd = CreateWindowEx(0, "VLCPlayerClass", "libvlc Demo",
        WS_OVERLAPPEDWINDOW | WS_CLIPCHILDREN,
        CW_USEDEFAULT, CW_USEDEFAULT, 1500, 900,
        NULL, NULL, hInstance, &Context);

    DragAcceptFiles(hWnd, TRUE);           /* Enable drag-and-drop */
    libvlc_media_player_set_hwnd(Context.p_mediaplayer, hWnd);
    ShowWindow(hWnd, nCmdShow);
    libvlc_media_player_play(Context.p_mediaplayer);

    MSG msg;
    while (GetMessage(&msg, NULL, 0, 0)) {
        TranslateMessage(&msg);
        DispatchMessage(&msg);
    }

    libvlc_media_player_stop(Context.p_mediaplayer);
    libvlc_media_release(libvlc_media_player_get_media(Context.p_mediaplayer));
    libvlc_media_player_release(Context.p_mediaplayer);
    libvlc_release(Context.p_libvlc);
    return (int)msg.wParam;
}
```

**Win32 with external D3D11 SwapChain (advanced, for UWP/custom rendering):**

Pass a pre-created D3D11 device context and swap chain to libvlc via CLI args. The app owns the swap chain and signals size changes through private data GUIDs. Based on the official `d3d11_swapr.cpp` sample.

```c
// Key setup (after D3D11CreateDeviceAndSwapChain):
// 1. Enable multithread protection on the D3D11 device
ID3D10Multithread *pMultithread;
d3device->QueryInterface(&IID_ID3D10Multithread, (void **)&pMultithread);
pMultithread->SetMultithreadProtected(TRUE);
pMultithread->Release();

// 2. Share the context mutex via private data
HANDLE d3dctx_mutex = CreateMutexEx(NULL, NULL, 0, SYNCHRONIZE);
d3dctx->SetPrivateData(GUID_CONTEXT_MUTEX, sizeof(d3dctx_mutex), &d3dctx_mutex);

// 3. Set initial swapchain dimensions via private data
uint32_t w = width, h = height;
swapchain->SetPrivateData(GUID_SWAPCHAIN_WIDTH,  sizeof(w), &w);
swapchain->SetPrivateData(GUID_SWAPCHAIN_HEIGHT, sizeof(h), &h);

// 4. Pass pointers to libvlc as CLI args
char ctx_arg[64], swap_arg[64];
sprintf(ctx_arg, "--winrt-d3dcontext=0x%llx", (intptr_t)d3dctx);
sprintf(swap_arg, "--winrt-swapchain=0x%llx", (intptr_t)swapchain);
const char *params[] = { ctx_arg, swap_arg };
libvlc_instance_t *vlc = libvlc_new(2, params);

// 5. On WM_SIZE, update the private data (libvlc reads it to resize output):
swapchain->SetPrivateData(GUID_SWAPCHAIN_WIDTH,  sizeof(new_w), &new_w);
swapchain->SetPrivateData(GUID_SWAPCHAIN_HEIGHT, sizeof(new_h), &new_h);
// DON'T use libvlc_media_player_set_hwnd() with external swapchain
```

### 6.2 macOS / iOS / tvOS

**macOS AppKit (Objective-C) — Full player:**

Based on the official `appkit_player.m` sample. Uses ARC and `__bridge` casting.

```objc
#import <Cocoa/Cocoa.h>
#import <vlc/vlc.h>

@interface AppDelegate : NSObject <NSApplicationDelegate> {
    libvlc_instance_t *instance;
    libvlc_media_player_t *player;
    libvlc_media_t *media;
}
@property NSWindow *window;
@property NSView *view;
@end

@implementation AppDelegate
- (void)applicationDidFinishLaunching:(NSNotification *)aNotification {
    NSWindowStyleMask mask = NSWindowStyleMaskTitled |
        NSWindowStyleMaskMiniaturizable | NSWindowStyleMaskResizable |
        NSWindowStyleMaskClosable;
    _window = [[NSWindow alloc]
        initWithContentRect:NSMakeRect(300, 300, 800, 600)
                  styleMask:mask
                    backing:NSBackingStoreBuffered
                      defer:NO];
    [_window setTitle:@"LibVLC AppKit Player"];
    [_window makeKeyAndOrderFront:nil];

    _view = [[NSView alloc] initWithFrame:NSMakeRect(0, 0, 800, 600)];
    [_window setContentView:_view];

    const char *const vlc_args[] = { "-vv" };
    instance = libvlc_new(1, vlc_args);
    player = libvlc_media_player_new(instance);

    NSString *location = [[NSProcessInfo processInfo] arguments][1];
    media = libvlc_media_new_location(instance, [location UTF8String]);
    libvlc_media_player_set_media(player, media);

    /* __bridge cast required under ARC */
    libvlc_media_player_set_nsobject(player, (__bridge void *)_view);
    libvlc_media_player_play(player);
}
@end

int main(int argc, char *argv[]) {
    AppDelegate *delegate = [[AppDelegate alloc] init];
    [NSApplication sharedApplication];
    [NSApp setActivationPolicy:NSApplicationActivationPolicyRegular];
    [NSApp setDelegate:delegate];
    [NSApp activateIgnoringOtherApps:YES];
    return NSApplicationMain(argc, (const char **)argv);
}
```

**`[4.x]` VLCDrawable protocol (macOS/iOS):**

In libvlc 4.x, the NSView/UIView passed to `set_nsobject()` can optionally implement the `VLCDrawable` protocol, which provides:
- Resize notifications when the video surface changes
- PictureInPicture (PiP) support on supported platforms
- The view manages its own layer hosting for GPU rendering

**LibVLCSharp:**
```csharp
// VideoView is NSView/UIView based
<vlc:VideoView x:Name="VideoView" />
```

### 6.3 Linux

**GTK+ (C) — Full player with file chooser:**

Based on the official `gtk_player.c` sample. The video output must be set after the drawing area widget is realized (i.e., has a native X11 window).

```c
// Build: gcc -o gtk_player gtk_player.c `pkg-config --libs --cflags gtk+-2.0 libvlc`

#include <stdlib.h>
#include <gtk/gtk.h>
#include <gdk/gdkx.h>
#include <vlc/vlc.h>

libvlc_media_player_t *media_player;
libvlc_instance_t *vlc_inst;

/* Set the X11 window ID after the widget has a native window */
void on_realize(GtkWidget *widget, gpointer data) {
    libvlc_media_player_set_xwindow(media_player,
        GDK_WINDOW_XID(gtk_widget_get_window(widget)));
}

void on_open(GtkWidget *widget, gpointer data) {
    GtkWidget *dialog = gtk_file_chooser_dialog_new("Choose Media",
        data, GTK_FILE_CHOOSER_ACTION_OPEN,
        GTK_STOCK_CANCEL, GTK_RESPONSE_CANCEL,
        GTK_STOCK_OPEN, GTK_RESPONSE_ACCEPT, NULL);
    if (gtk_dialog_run(GTK_DIALOG(dialog)) == GTK_RESPONSE_ACCEPT) {
        char *uri = gtk_file_chooser_get_uri(GTK_FILE_CHOOSER(dialog));
        libvlc_media_t *media = libvlc_media_new_location(vlc_inst, uri);
        libvlc_media_player_set_media(media_player, media);
        libvlc_media_player_play(media_player);
        libvlc_media_release(media);
        g_free(uri);
    }
    gtk_widget_destroy(dialog);
}

int main(int argc, char *argv[]) {
    gtk_init(&argc, &argv);

    GtkWidget *window = gtk_window_new(GTK_WINDOW_TOPLEVEL);
    gtk_window_set_default_size(GTK_WINDOW(window), 800, 600);
    g_signal_connect(window, "destroy", G_CALLBACK(gtk_main_quit), NULL);

    GtkWidget *vbox = gtk_vbox_new(FALSE, 0);
    gtk_container_add(GTK_CONTAINER(window), vbox);

    /* Video drawing area — connect "realize" to set X11 window ID */
    GtkWidget *player_widget = gtk_drawing_area_new();
    gtk_box_pack_start(GTK_BOX(vbox), player_widget, TRUE, TRUE, 0);

    vlc_inst = libvlc_new(0, NULL);
    media_player = libvlc_media_player_new(vlc_inst);

    g_signal_connect(player_widget, "realize", G_CALLBACK(on_realize), NULL);

    gtk_widget_show_all(window);
    gtk_main();

    libvlc_media_player_release(media_player);
    libvlc_release(vlc_inst);
    return 0;
}
```

**Key GTK pattern:** The X11 window ID (`GDK_WINDOW_XID`) is only available after the widget is realized. Always connect the `"realize"` signal and set the window ID there, never before `gtk_widget_show_all()`.

### 6.3b Qt (C++)

**Qt player with cross-platform video embedding:**

Based on the official `QtPlayer` sample. Uses platform-conditional APIs for video embedding and a `QTimer` for polling playback state.

```cpp
// Build: qmake && make (requires libvlc and Qt5/6 development packages)
#include <QMainWindow>
#include <QSlider>
#include <QPushButton>
#include <QTimer>
#include <QFileDialog>
#include <vlc/vlc.h>

#ifdef Q_OS_WIN
#include <windows.h>
#endif

class VLCPlayer : public QMainWindow {
    Q_OBJECT
public:
    VLCPlayer() {
        vlcInstance = libvlc_new(0, NULL);
        vlcPlayer = NULL;

        videoWidget = new QWidget(this);
        videoWidget->setAutoFillBackground(true);
        QPalette plt = palette();
        plt.setColor(QPalette::Window, Qt::black);
        videoWidget->setPalette(plt);

        slider = new QSlider(Qt::Horizontal);
        slider->setMaximum(1000);
        connect(slider, &QSlider::sliderMoved, this, &VLCPlayer::seek);

        /* Poll playback position every 100ms */
        QTimer *timer = new QTimer(this);
        connect(timer, &QTimer::timeout, this, &VLCPlayer::updateUI);
        timer->start(100);
        // ... layout setup ...
    }

    ~VLCPlayer() {
        if (vlcPlayer) {
            libvlc_media_player_stop(vlcPlayer);
            libvlc_media_player_release(vlcPlayer);
        }
        if (vlcInstance) libvlc_release(vlcInstance);
    }

    void openFile() {
        QString file = QFileDialog::getOpenFileName(this, "Open Media");
        if (file.isEmpty()) return;

        if (vlcPlayer && libvlc_media_player_is_playing(vlcPlayer))
            stop();

        libvlc_media_t *media = libvlc_media_new_path(vlcInstance,
            file.toUtf8().constData());
        vlcPlayer = libvlc_media_player_new_from_media(media);
        libvlc_media_release(media);

        /* Platform-specific video embedding */
#if defined(Q_OS_MAC)
        libvlc_media_player_set_nsobject(vlcPlayer,
            (void *)videoWidget->winId());
#elif defined(Q_OS_UNIX)
        libvlc_media_player_set_xwindow(vlcPlayer, videoWidget->winId());
#elif defined(Q_OS_WIN)
        /* WS_CLIPCHILDREN required on Windows */
        HWND hwnd = (HWND)videoWidget->winId();
        LONG style = GetWindowLong(hwnd, GWL_STYLE);
        if (!(style & WS_CLIPCHILDREN))
            SetWindowLong(hwnd, GWL_STYLE, style | WS_CLIPCHILDREN);
        libvlc_media_player_set_hwnd(vlcPlayer, hwnd);
#endif
        libvlc_media_player_play(vlcPlayer);
    }

private slots:
    void updateUI() {
        if (!vlcPlayer) return;
        float pos = libvlc_media_player_get_position(vlcPlayer);
        slider->setValue((int)(pos * 1000.0));
        if (libvlc_media_player_get_state(vlcPlayer) == libvlc_Ended)
            stop();
    }

    void seek(int pos) {
        if (vlcPlayer)
            libvlc_media_player_set_position(vlcPlayer, (float)pos / 1000.0);
    }

    void stop() {
        if (vlcPlayer) {
            libvlc_media_player_stop(vlcPlayer);
            libvlc_media_player_release(vlcPlayer);
            vlcPlayer = NULL;
            slider->setValue(0);
        }
    }

private:
    libvlc_instance_t *vlcInstance;
    libvlc_media_player_t *vlcPlayer;
    QWidget *videoWidget;
    QSlider *slider;
};
```

**Key Qt patterns:**
- Use `QTimer` for polling playback state (position, ended) rather than libvlc events, to stay on the Qt event loop
- `videoWidget->winId()` returns the native window handle on all platforms
- On Windows, add `WS_CLIPCHILDREN` to the video widget's window style before setting the HWND

### 6.4 Android

```c
libvlc_media_player_set_android_context(mp, awindow);
```

**LibVLCSharp.Android:**
```csharp
// VideoView wraps SurfaceView
<vlc:VideoView android:id="@+id/videoView" />
```

**vlcj (Android via libvlcjni):**
- Uses `org.videolan.libvlc` from JitPack
- `IVLCVout` interface for surface management

### 6.5 Framework Comparison

| Framework | Binding | Video Surface | GPU Accel |
|-----------|---------|---------------|-----------|
| WPF | LibVLCSharp.WPF | WindowsFormsHost | Yes (D3D) |
| WinForms | LibVLCSharp.WinForms | Direct Handle | Yes (D3D) |
| UWP | LibVLCSharp.UWP | SwapChainPanel | Yes (D3D11) |
| Xamarin.iOS | LibVLCSharp | UIView | Yes (OpenGL) |
| Xamarin.Android | LibVLCSharp | SurfaceView | Yes (MediaCodec) |
| Swing | vlcj | AWT Canvas | Yes (platform) |
| JavaFX | vlcj | PixelBuffer callback | CPU copy |
| GTK | C/Python | DrawingArea | Yes (platform) |
| Qt | C++ | QWidget (winId) | Yes (platform) |
| Avalonia | LibVLCSharp.Avalonia | NativeControlHost | Yes (platform) |

### 6.6 Using MediaPlayerElement (Plug-and-Play UI Control)

`MediaPlayerElement` is a high-level control in LibVLCSharp.Forms that provides a ready-made video player UI with transport controls (play/pause, seek bar, volume, track selection, Chromecast). It replaces the need to build playback UI from scratch.

**Xamarin.Forms / MAUI XAML:**
```xml
<vlc:MediaPlayerElement
    EnableRendererDiscovery="True"
    LibVLC="{Binding LibVLC}"
    MediaPlayer="{Binding MediaPlayer}" />
```

**ViewModel:**
```csharp
public class MainViewModel : INotifyPropertyChanged
{
    public LibVLC LibVLC { get; private set; }
    public MediaPlayer MediaPlayer { get; private set; }

    public void OnAppearing()
    {
        Core.Initialize();
        LibVLC = new LibVLC(enableDebugLogs: true);

        var media = new Media(LibVLC,
            new Uri("http://example.com/video.mp4"));
        MediaPlayer = new MediaPlayer(media) { EnableHardwareDecoding = true };
        media.Dispose();
        MediaPlayer.Play();
    }

    public void OnDisappearing()
    {
        MediaPlayer.Dispose();
        LibVLC.Dispose();
    }
}
```

**Customization** — hide/show controls, change colors, toggle features:
```xml
<vlc:MediaPlayerElement LibVLC="{Binding LibVLC}" MediaPlayer="{Binding MediaPlayer}">
    <vlc:MediaPlayerElement.PlaybackControls>
        <vlc:PlaybackControls
            MainColor="Red"
            IsAspectRatioButtonVisible="False"
            IsAudioTracksSelectionButtonVisible="False"
            IsClosedCaptionsSelectionButtonVisible="False"
            KeepScreenOn="True"
            ShowAndHideAutomatically="True" />
    </vlc:MediaPlayerElement.PlaybackControls>
</vlc:MediaPlayerElement>
```

**Available customization properties:**
- `MainColor`, `ButtonColor`, `Foreground` — theme colors
- `IsPlayPauseButtonVisible`, `IsStopButtonVisible`, `IsSeekBarVisible`, `IsSeekEnabled` — transport controls
- `IsRewindButtonVisible`, `IsSeekButtonVisible` — skip forward/back buttons
- `IsAudioTracksSelectionButtonVisible`, `IsClosedCaptionsSelectionButtonVisible` — track pickers
- `IsCastButtonVisible` — Chromecast button (requires `EnableRendererDiscovery="True"`)
- `IsAspectRatioButtonVisible` — aspect ratio toggle
- `KeepScreenOn` — prevent screen dimming during playback
- `ShowAndHideAutomatically` — auto-hide controls after inactivity

### 6.7 Avalonia Desktop Integration

LibVLCSharp.Avalonia provides a `VideoView` control using Avalonia's `NativeControlHost`. This is suitable for cross-platform desktop apps on Windows, macOS, and Linux.

```csharp
// Avalonia ViewModel — proper Dispose pattern
public class MainWindowViewModel : IDisposable
{
    private readonly LibVLC _libVlc = new();
    public MediaPlayer MediaPlayer { get; }

    public MainWindowViewModel()
    {
        MediaPlayer = new MediaPlayer(_libVlc);
    }

    public void Play()
    {
        if (Design.IsDesignMode) return;  // Skip in XAML preview
        using var media = new Media(_libVlc,
            new Uri("http://example.com/video.mp4"));
        MediaPlayer.Play(media);
    }

    public void Dispose()
    {
        MediaPlayer.Stop();
        MediaPlayer.Dispose();
        _libVlc.Dispose();
    }
}
```

**Key points:**
- Check `Design.IsDesignMode` to avoid libvlc calls during XAML previewer rendering
- Implement `IDisposable` to properly clean up native resources
- `VideoView` in Avalonia wraps `NativeControlHost` — ensure `AllowsTransparency` is not set on the window (native video surfaces don't support transparency on all platforms)

---

## §7. Streaming & Transcoding

### 7.1 Sout Chain Syntax

The stream output chain uses the `:sout=` option with `#module{params}:module{params}` syntax:

```
:sout=#transcode{<params>}:standard{<params>}
:sout=#transcode{<params>}:duplicate{dst=display,dst=standard{<params>}}
```

**Common sout modules:**

| Module | Purpose | Key Parameters |
|--------|---------|---------------|
| `transcode` | Convert codec | `vcodec`, `vb` (bitrate), `acodec`, `ab`, `channels`, `width`, `height`, `fps`, `scale` |
| `standard`/`std` | Output destination | `access` (file/http/udp), `mux` (ts/mp4/ogg/webm), `dst` (path/url) |
| `duplicate` | Split stream | `dst=display` (show locally), `dst=standard{...}` |
| `rtp` | RTP streaming | `dst`, `port`, `mux` |
| `http` | HTTP streaming | `dst`, `mux` |

### 7.2 Common Sout Recipes

**Save to file:**
```
:sout=#transcode{vcodec=h264,vb=2000,acodec=mp4a,ab=192}:std{access=file,mux=mp4,dst=/output.mp4}
```

**HTTP live stream:**
```
:sout=#transcode{vcodec=h264,acodec=mpga,ab=128}:http{mux=ts,dst=:8080/stream}
```

**UDP multicast:**
```
:sout=#transcode{vcodec=h264}:rtp{mux=ts,dst=239.0.0.1,port=1234}
```

**Display locally AND save:**
```
:sout=#transcode{vcodec=h264}:duplicate{dst=display,dst=std{access=file,mux=mp4,dst=out.mp4}}
```

**Audio only (extract audio):**
```
:sout=#transcode{acodec=mp3,ab=192}:std{access=file,mux=raw,dst=output.mp3}
:no-video
```

### 7.3 Video Codecs

| Codec ID | Codec | Notes |
|----------|-------|-------|
| `h264` | H.264/AVC | Most compatible |
| `h265` | H.265/HEVC | Better compression |
| `mp4v` | MPEG-4 Part 2 | Legacy |
| `VP80` | VP8 | WebM |
| `VP90` | VP9 | WebM, better |
| `theo` | Theora | Ogg |
| `none` | No video | Strip video track |

### 7.4 Audio Codecs

| Codec ID | Codec | Notes |
|----------|-------|-------|
| `mpga` | MP3 | Universal |
| `mp4a` | AAC | Better quality |
| `vorb` | Vorbis | Ogg |
| `opus` | Opus | Best for voice |
| `flac` | FLAC | Lossless |
| `none` | No audio | Strip audio track |

### 7.5 Container Formats (Mux)

| Mux | Format | Typical Use |
|-----|--------|-------------|
| `ts` | MPEG-TS | Streaming (HTTP, UDP) |
| `mp4` | MP4/MOV | File output |
| `ogg` | Ogg | Vorbis/Theora |
| `webm` | WebM | VP8/VP9 + Opus |
| `avi` | AVI | Legacy |
| `raw` | Raw | Single codec output |
| `asf` | ASF/WMV | Windows |

---

## §8. Troubleshooting & Gotchas

### 8.1 Critical Pitfalls (Will Bite You)

#### Deadlock from Event Callbacks
**Symptom:** Application freezes/hangs during playback event.
**Cause:** Calling any libvlc function from within a libvlc event callback.
**Fix:** Offload work to another thread. See §2.2 for per-language patterns.

#### Stop() Freezing
**Symptom:** `libvlc_media_player_stop()` blocks for seconds (especially RTSP streams in LibVLC 3).
**Fix:** Call `stop()` from a background thread:
```csharp
// C#
ThreadPool.QueueUserWorkItem(_ => player.Stop());
```
```java
// Java
mediaPlayer.submit(() -> mediaPlayer.controls().stop());
```

#### Memory Leaks from Event Handlers
**Symptom:** Growing memory usage over time.
**Cause (C#):** LibVLCSharp events are native callbacks. Failing to unsubscribe causes both managed and native memory leaks.
**Fix:** Always unsubscribe event handlers before disposing objects:
```csharp
player.Playing -= OnPlaying;
player.Dispose();
```

#### Multiple LibVLC Instances
**Symptom:** Crashes, undefined behavior, plugin conflicts.
**Cause:** Creating more than one `libvlc_instance_t`.
**Fix:** Create exactly ONE instance, share it across all players.

#### GC Collecting Active Players
**Symptom:** Random crashes, especially in Java/Python.
**Cause:** Player object goes out of scope while native thread still runs.
**Fix:** Keep strong references to all libvlc objects as class fields, not local variables.

#### Untrusted Input to `media_add_option` (Security)
**Symptom:** Media exfiltration, arbitrary file writes, or unexpected streaming behavior.
**Cause:** `libvlc_media_add_option()` treats the option as **trusted** — it can set `sout` chains, write files, open network streams, etc. If the option string comes from user input (e.g., a URL parameter, config file, or UI text field), an attacker can inject `:sout=#transcode{...}:std{access=file,dst=/etc/passwd}` or redirect media to a remote server.
**Fix:** Use `libvlc_media_add_option_flag()` with the **untrusted** flag (value `0x0`, the default when no flags are set) for any user-provided input. Only use `libvlc_media_option_trusted` (`0x2`) for options your application controls:
```c
// SAFE — user-provided options are untrusted (default flag = 0)
libvlc_media_add_option_flag(media, user_input, 0);

// TRUSTED — only for app-controlled options
libvlc_media_add_option_flag(media, ":network-caching=1000", libvlc_media_option_trusted);

// DANGEROUS — libvlc_media_add_option() always trusts the input
// Never pass user/network input to this function:
libvlc_media_add_option(media, user_input);  // ⚠️ DO NOT DO THIS
```
**Binding equivalents:**
- **C#** (LibVLCSharp): `media.AddOption(":option")` — trusted by default. Validate/sanitize user input before passing.
- **Python**: `media.add_option(":option")` — same caveat.
- **vlcj**: `media().play(mrl, ":option")` — trusted. Sanitize.

### 8.2 Common Issues

#### Green/Corrupt Video Frames
**Cause:** GPU driver issue or incompatible hardware decoding.
**Fix:**
1. Update GPU drivers
2. Disable hardware decoding: `--avcodec-hw=none`
3. Try different video output: `--vout=x11` (Linux), `--vout=directdraw` (Windows)

#### No Audio Output
**Cause:** Audio output device not configured or unavailable.
**Fix:**
1. List available outputs: `libvlc_audio_output_list_get()`
2. Set explicitly: `libvlc_audio_output_set(mp, "alsa")` (or `"directsound"`, `"coreaudio"`)
3. Check volume: `libvlc_audio_set_volume(mp, 100)`

#### Chromecast Not Found
**Cause:** VPN blocking mDNS discovery, or network isolation.
**Fix:**
1. Disconnect VPN
2. Ensure device is on same subnet
3. Check firewall (mDNS uses port 5353/UDP)

#### YouTube URLs Not Playing
**Cause:** YouTube requires network parsing to resolve actual stream URL.
**Fix:**
```c
libvlc_media_parse_with_options(media, libvlc_media_parse_network, 10000);
// Then play first sub-item:
libvlc_media_list_t *subs = libvlc_media_subitems(media);
libvlc_media_list_lock(subs);
libvlc_media_t *actual = libvlc_media_list_item_at_index(subs, 0);
libvlc_media_list_unlock(subs);
libvlc_media_player_set_media(mp, actual);
libvlc_media_player_play(mp);
```

#### Slow Startup / Plugin Scan
**Cause:** LibVLC scans all plugins on first `libvlc_new()`.
**Fix:**
1. Pre-generate `plugins.dat` cache file (or use `--reset-plugins-cache` once, then rely on cache)
2. Initialize LibVLC early (splash screen, app startup)
3. Reuse the instance — don't destroy and recreate
4. Use `--no-plugins-scan` to skip directory scanning and load only from cache (if cache exists)
5. See §2.7 for full plugin discovery details and `VLC_PLUGIN_PATH` usage

#### Video Callbacks Performance (LibVLC 3.x)
**Cause:** CPU-based pixel copying with no GPU acceleration.
**Fix:**
1. Use smallest necessary resolution
2. Use `I420` chroma (smaller than `RV32`)
3. Process frames asynchronously — don't block the lock/unlock callbacks

#### Subtitle Encoding Issues
**Fix:** Set encoding option:
```c
// Instance level:
"--subsdec-encoding=Windows-1252"
// Or per-media:
":subsdec-encoding=UTF-8"
```

### 8.3 Debugging Methodology

1. **Enable verbose logging:**
   ```c
   const char *args[] = {"--verbose=2"};
   libvlc_instance_t *inst = libvlc_new(1, args);
   ```
   Or set log callback to capture programmatically (see §2.5).

2. **Build minimal reproduction:** Isolate the issue in the smallest possible code.

3. **Check the logs:** Look for `[error]` and `[warning]` lines. Common indicators:
   - `no suitable decoder` — missing codec plugin
   - `connection refused` — network issue
   - `main decoder error` — corrupt media or unsupported format

4. **Regression test:** Does it work in official VLC app? Does it work with a different file? Different platform?

5. **Version check:** `libvlc_get_version()` — verify you're running expected version.

---

## §9. CLI Options Quick Reference

### Instance-Level (`--option=value` in constructor)

| Option | Description | Default |
|--------|-------------|---------|
| `--verbose=N` | Log verbosity: 0=errors, 1=warnings, 2=debug | 0 |
| `--no-video-title-show` | Don't show media title on video | off |
| `--no-video` | Disable video output entirely | off |
| `--no-audio` | Disable audio output entirely | off |
| `--avcodec-hw=MODE` | Hardware decoding: `any`, `none`, `d3d11va`, `vaapi`, `videotoolbox` | `any` |
| `--network-caching=MS` | Network stream buffer in ms | 1000 |
| `--file-caching=MS` | File stream buffer in ms | 300 |
| `--live-caching=MS` | Live stream buffer in ms | 300 |
| `--vout=MODULE` | Video output: `x11`, `gl`, `directdraw`, `d3d11`, `caca` | auto |
| `--aout=MODULE` | Audio output: `pulse`, `alsa`, `directsound`, `coreaudio`, `winstore` | auto |
| `--freetype-rel-fontsize=N` | Subtitle font size (relative) | 16 |
| `--freetype-color=N` | Subtitle color (decimal, e.g., 16711680 = red) | 16777215 |
| `--subsdec-encoding=ENC` | Subtitle encoding: `UTF-8`, `Windows-1252`, etc. | auto |
| `--hrtf-file=PATH` | 3D audio HRTF file path | — |
| `--no-plugins-cache` | Disable plugin cache, force re-scan every startup | cache enabled |
| `--no-plugins-scan` | Don't scan plugin dirs, load from cache only | scan enabled |
| `--reset-plugins-cache` | Rebuild `plugins.dat` cache on next startup | off |

### Media-Level (`:option=value` via `media_add_option`)

| Option | Description |
|--------|-------------|
| `:no-audio` | Disable audio for this media |
| `:no-video` | Disable video for this media |
| `:network-caching=MS` | Override network caching |
| `:start-time=SEC` | Start playback at N seconds |
| `:stop-time=SEC` | Stop playback at N seconds |
| `:run-time=SEC` | Play for N seconds |
| `:sub-file=PATH` | External subtitle file path |
| `:sub-language=LANG` | Preferred subtitle language (e.g., `"eng"`, `"none"`) |
| `:sout=CHAIN` | Stream output chain (see §7) |
| `:sout-keep` | Keep sout instance across media changes |
| `:no-sout-all` | Only stream first track of each type |
| `:sout-chromecast-conversion-quality=N` | Chromecast quality: 0=low, 1=medium, 2=high |
| `:input-repeat=N` | Repeat input N times (0=play once) |

**Format difference:** Instance options use `--double-dash`, Media options use `:colon-prefix`.

---

## §10. Deprecated API — Do NOT Use

The following functions are deprecated. Always suggest their modern replacements:

| Deprecated | Replacement |
|-----------|-------------|
| `libvlc_media_parse()` | `libvlc_media_parse_with_options()` |
| `libvlc_media_parse_async()` | `libvlc_media_parse_with_options()` |
| `libvlc_media_is_parsed()` | `libvlc_media_get_parsed_status()` |
| `libvlc_media_get_tracks_info()` | `libvlc_media_tracks_get()` |
| `libvlc_media_player_get_fps()` | `libvlc_media_tracks_get()` (get FPS from video track info) |
| `libvlc_video_get_height()` | `libvlc_video_get_size()` |
| `libvlc_video_get_width()` | `libvlc_video_get_size()` |
| `libvlc_video_set_subtitle_file()` | `libvlc_media_player_add_slave()` |
| `libvlc_track_description_release()` | `libvlc_track_description_list_release()` |
| `libvlc_media_player_set_agl()` | `libvlc_media_player_set_nsobject()` |
| `libvlc_media_discoverer_new_from_name()` | `libvlc_media_discoverer_new()` + `_start()` |
| `libvlc_media_discoverer_localized_name()` | `libvlc_media_discoverer_list_get()` |
| `libvlc_wait()` | `libvlc_set_exit_handler()` |
| `libvlc_log_open/close/count/clear/get_iterator/iterator_*()` | `libvlc_log_set()` with callback |
| `libvlc_playlist_play()` | `libvlc_media_list` + `libvlc_media_list_player` |
| `libvlc_audio_output_device_count/longname/id()` | `libvlc_audio_output_device_list_get()` |
| `libvlc_toggle_teletext()` | `libvlc_video_set_teletext()` |

---

## §11. Available Language Bindings

| Language | Binding | Package/Repo |
|----------|---------|--------------|
| C | libvlc (native) | `#include <vlc/vlc.h>` |
| C++ | libvlcpp | Header-only, part of VLC ecosystem |
| C# / .NET | LibVLCSharp | NuGet: `LibVLCSharp` + `VideoLAN.LibVLC.*` |
| Python | python-vlc | PyPI: `python-vlc` |
| Java (Desktop) | vlcj | Maven: `uk.co.caprica:vlcj:4.x` |
| Java (Android) | libvlcjni | JitPack / VLC Android SDK |
| Kotlin | vlcj / libvlcjni | Same as Java |
| Objective-C / Swift | VLCKit | CocoaPods: `MobileVLCKit` / `TVVLCKit` |
| Go | libvlc-go | `github.com/adrg/libvlc-go/v3` |
| Rust | vlc-rs | `crates.io/crates/vlc-rs` |
| Dart/Flutter | dart_vlc (desktop), flutter_vlc_player (mobile) | pub.dev |
| Node.js | webchimera.js | npm: `webchimera.js` |

---

## §12. Quick Decision Guide

**"Which binding should I use?"**

| Platform | Recommended Binding |
|----------|-------------------|
| Windows desktop (.NET) | LibVLCSharp |
| macOS/iOS/tvOS (Swift) | VLCKit |
| Android (Kotlin/Java) | libvlcjni + LibVLCSharp.Android |
| Cross-platform .NET (MAUI, Avalonia) | LibVLCSharp |
| Desktop Java/Kotlin | vlcj 4.x |
| Python scripting | python-vlc |
| Go application | libvlc-go |
| C/C++ application | libvlc / libvlcpp |
| Rust application | vlc-rs |
| Electron/Web | webchimera.js or LibVLC WASM (experimental) |

**"How should I render video?"**

| Need | Approach |
|------|----------|
| Embedded in native window | `set_hwnd`/`set_xwindow`/`set_nsobject` |
| Custom rendering / texture | `[3.x]` Video callbacks (lock/unlock/display). `[4.x]` GPU output callbacks (`set_output_callbacks`). |
| Headless (no display) | `--no-video` or video callbacks to `/dev/null`. `[4.x]` `libvlc_video_engine_disable`. |
| Off-screen thumbnail | `[3.x]` Video callbacks, capture first frame. `[4.x]` Use `libvlc_media_thumbnail_request_by_time()`. |
| Multiple simultaneous videos | Multiple MediaPlayers, one LibVLC instance |

**"How do I handle the end of playback?"**

All bindings: Listen for `EndReached` / `MediaPlayerEndReached` event. **Always** offload the next action to a different thread — never call libvlc from the callback.

---

## §13. Migration Guide: libVLC 3.x → 4.x

Quick reference for porting 3.x code to 4.x. See inline `[4.x]` / `[4.x change]` markers throughout this document for details.

### Function Signature Changes

| 3.x | 4.x | Notes |
|-----|-----|-------|
| `libvlc_media_new_path(inst, path)` | `libvlc_media_new_path(path)` | All `_new_*` media creators drop `inst` |
| `libvlc_media_new_location(inst, mrl)` | `libvlc_media_new_location(mrl)` | |
| `libvlc_media_new_fd(inst, fd)` | `libvlc_media_new_fd(fd)` | |
| `libvlc_media_new_callbacks(inst, open, read, seek, close, opaque)` | `libvlc_media_new_callbacks(open, read, seek, close, opaque)` | |
| `libvlc_media_new_as_node(inst, name)` | `libvlc_media_new_as_node(name)` | |
| `libvlc_media_list_new(inst)` | `libvlc_media_list_new()` | |
| `libvlc_media_player_new_from_media(media)` | `libvlc_media_player_new_from_media(inst, media)` | Swapped: inst added |
| `libvlc_media_player_stop(mp)` | `libvlc_media_player_stop_async(mp)` | Async, returns int |
| `libvlc_media_list_player_stop(mlp)` | `libvlc_media_list_player_stop_async(mlp)` | Async |
| `libvlc_media_player_set_time(mp, t)` | `libvlc_media_player_set_time(mp, t, fast)` | Added `b_fast` |
| `libvlc_media_player_set_position(mp, p)` | `libvlc_media_player_set_position(mp, p, fast)` | `p` is `double`, added `b_fast` |
| `libvlc_media_player_get_position(mp)` | Same | Returns `double` (was `float`) |
| `libvlc_media_parse_with_options(m, f, t)` | `libvlc_media_parse_request(inst, m, f, t)` | Inst added, returns int |
| `libvlc_media_save_meta(media)` | `libvlc_media_save_meta(inst, media)` | Inst added |
| `libvlc_video_set_deinterlace(mp, mode)` | `libvlc_video_set_deinterlace(mp, state, mode)` | State: -1/0/1 |
| `libvlc_audio_output_device_set(mp, mod, id)` | `libvlc_audio_output_device_set(mp, id)` | Module param removed |
| `libvlc_video_set_crop_geometry(mp, geo)` | `libvlc_video_set_crop_ratio(mp, n, d)` | String → structured |

### Removed APIs (no 4.x equivalent)

| 3.x API | Alternative in 4.x |
|---------|-------------------|
| `libvlc_vlm_*()` (entire VLM API) | Use sout chains via `libvlc_media_add_option()` |
| `libvlc_add_intf(inst, name)` | No equivalent |
| `libvlc_set_exit_handler(inst, cb, op)` | No equivalent |
| `libvlc_media_tracks_get/release()` | `libvlc_media_get_tracklist()` + `_delete()` |
| `libvlc_audio_get_track_description()` | `libvlc_media_player_get_tracklist(mp, audio, false)` |
| `libvlc_video_get_track_description()` | `libvlc_media_player_get_tracklist(mp, video, false)` |
| `libvlc_video_get_spu_description()` | `libvlc_media_player_get_tracklist(mp, text, false)` |
| `libvlc_audio/video_set_track(mp, id)` | `libvlc_media_player_select_track(mp, track)` |
| `libvlc_video_set_spu(mp, id)` | `libvlc_media_player_select_track(mp, track)` |
| `libvlc_audio_get/set_channel()` | `libvlc_audio_get/set_stereomode()` |
| Event: `libvlc_MediaFreed` | No equivalent (use release directly) |
| Event: `libvlc_MediaStateChanged` | No equivalent (use player state events) |

### New APIs (4.x only)

| API | Purpose | See §  |
|-----|---------|--------|
| Tracklist API | String-ID track selection | §3.11 |
| Program API | MPEG-TS program selection | §3.12 |
| GPU rendering (`set_output_callbacks`) | D3D11/OpenGL/GLES2 video output | §3.13 |
| A-B Loop | Loop between two points | §3.14 |
| Picture API | Image type for thumbnails/art | §3.15 |
| Thumbnail Request | Async thumbnail generation | §3.2 Media |
| Watch Time | Precise time interpolation for UI | §2.2 |
| Concurrency (lock/wait/signal) | Built-in sync primitives | §2.2 |
| Recording | `media_player_record()` | §3.3 |
| Display Fit Mode | Contain/cover/fit display modes | §3.3 Video |
| Audio Mix Mode | Force stereo/5.1/7.1/binaural | §3.3 Audio |
| Meta Extra | Custom key-value metadata | §3.2 Media |
| Jump Time | Relative seeking | §3.3 Playback |
| `parse_stop()` | Cancel parsing | §3.2 Media |

### Type Changes

| What | 3.x | 4.x |
|------|-----|-----|
| `get_position()` return | `float` | `double` |
| `is_playing()` return | `int` | `bool` |
| `is_seekable()` return | `int` | `bool` |
| `can_pause()` return | `int` | `bool` |
| `is_running()` (discoverer) | `int` | `bool` |
| ES event track ID | `int i_id` | `const char *psz_id` |
| Position changed event | `float new_position` | `double new_position` |
| Parse flags | Values: 0x00–0x08 | Values: 0x01–0x20 (renumbered) |
