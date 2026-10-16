# THE LAST BELL 2.0 — Test Report

All results below were produced **in-editor via the live Unity bridge** (`127.0.0.1:8080`) on
Unity 2022.3.62f3, Built-in RP. The **signed 2.0 production AAB** was built and verified from
this seat on 2026-10-16 (rows 18–22). The remaining UNVERIFIED items are physical-device
checks, listed at the end.

---

## PASS — executed & verified

| # | Test | Evidence |
|---|---|---|
| 1 | Repository/scene integrity scan (Phase 0) | `AUDIT_REPORT.md` committed (`b34c625`); no casual deletions found |
| 2 | Touch controls wiring | `Tools/NeKONTROLLER ▸ Verify Touch Controls` — **PASS ×3** |
| 3 | Save round-trip (edit mode) | `Tools/Part10 ▸ Verify Save Round-Trip` — **17/17 PASS**, re-run after Phase 2 still 17/17 |
| 4 | Monster initial state | MainLevel play: `state = Dormant` before first key |
| 5 | Teleport destination validation | 25 scripted teleports: **valid = 25 / invalid = 0 / moved = 25** |
| 6 | Rigged invalid safe place refused | Mid-air destination injected: `landedAtRigged = False`, final landing valid |
| 7 | Authored safe place honored | Normal retreat to the authored hidden spot still works |
| 8 | Six-page collection | Six page picks → `RunSave.PagesCollected = 6` |
| 9 | Secret ending flag (live) | In `GameWin` instance: `secretTruth = True`, `gameOverEnding = False`; after `Start` the flag is consumed (`-1`) |
| 10 | 6-row menu layout (live play) | Rows CONTINUE→QUIT at y = 104…−151, pitch 51, height 51; `rowOverlap = False`; span +129.5 → −176.5 (inside ±180); screenshot `Screenshots/lastbell_menu_6row_2.0.png` |
| 11 | Settings panel (live play) | `FlashValue` (20,−74), `TierValue` (20,−112), `SettingsBack` (−105,−152), `SettingsReset` (100,−152) all present |
| 12 | Reduce-flashing toggle (live play) | `False → True` (label `Reduced`) `→ False` (label `Normal`) |
| 13 | SafeAreaClamp (live, Device Simulator Pixel 5 landscape) | `safeArea` x=0 … 2204 of 2340 (136 px right punch-hole); authored (−48,−48) → **unchanged** (its 140 px margin covers 136 px); `padding=40` → **(−86.50, −48)** exactly = inset 46.5 u + 40; restore → (−48, −48) |
| 14 | MainLevel smoke after all changes | `AmbientAudioManager`, `GameManager`, `FL1_AmbientAudio`, monster all present; `state = Dormant`; console errors `[]` |
| 15 | Android icons | All **6** slots = `LastBellAppIcon` (readable + uncompressed, 2048×2048) — was 0/6 |
| 16 | Custom main manifest | XML well-formed; exactly 1 `uses-permission` node: `android.permission.INTERNET` with `tools:node="remove"` |
| 17 | Editor left clean | MainLevel open, not dirty, not playing; run save cleared; `Save.Truth` absent; `opt_reduceFlashing = 0`; console errors `[]` |
| 18 | **2.0 production AAB** | `BuildReport.result = Succeeded`, errors 0 — `Builds/LastBell-2.0-code20.aab` (39.0 MB, SHA-256 `2F205919…93C010C7`); symbols zip archived |
| 19 | **INTERNET absent in the shipped bundle** | aapt2 decode of the bundle's binary manifest: **no `uses-permission` nodes at all** |
| 20 | **Launcher entry in the shipped bundle** | `com.unity3d.player.UnityPlayerActivity` present + `MAIN`/`LAUNCHER` intent + `exported=true` (this is the check that caught the first build missing its launcher entirely) |
| 21 | **Bundle signature** | `jarsigner -verify -certs` → **jar verified** (self-signed upload key, no timestamp — normal) |
| 22 | **Bundle identity & payload** | versionCode 20 · versionName 2.0 · minSdk 24 / targetSdk 36 · arm64-v8a only · level0..level4 · record `Builds/aab_verification_2.0-code20.txt` |

Notes on test 13: the Device Simulator models the Pixel 5 punch-hole in landscape; the clamp
was verified for both the no-op case (authored margin already sufficient) and the engage case
(exact expected offset). The first implementation had a scale-source bug (pixel width instead
of `scaleFactor`) — this live test caught it; the fixed version is what passed above.

## UNVERIFIED — physical-device checks (user-side)

The signed-build items from earlier revisions of this report (2.0 AAB build; INTERNET removal in
the merged manifest) are now **verified** — see rows 18–22.

| Item | Why it is blocked |
|---|---|
| App icon on a real launcher (mask shape, quality) | Device-side visual (the icon IS in the bundle — 6 mipmap densities, verified) |
| Safe area on a real notched/punch-hole phone | Only the Simulator was available here |
| FPS / memory / thermal / loading times | Device measurements; README §12.9 remains the outstanding performance evidence |
| Audio output (levels, clipping) | Clips wired by design from existing assets; nobody has auditioned the 2.0 mix on hardware |
| Full end-to-end device run | Pre-existing outstanding item (README §12.8) |

## Build incidents — 2026-10-16

1. **First production build shipped without a launcher entry.** A custom main manifest replaces
   Unity's launcher template; Unity only logs
   `LauncherManifestDiag.txt: Unable to find Unity activity in manifest` and still reports build
   success, but `UnityPlayerActivity` was absent — the app would install and never launch. Caught
   by decoding the AAB's binary manifest with aapt2, not by the build result. Fixed by declaring
   the activity in the custom manifest (element-for-element from the previous builds); the final
   bundle passes the launcher checks (row 20).
2. **Stuck "scripts have compile errors" state.** Duplicate queued build callbacks re-fired
   after a completed build and started reentrant build attempts; Unity's build system hit an
   internal error and left `scriptCompilationFailed = true`, so every further build failed
   instantly with "Error building Player because scripts have compile errors in the editor" —
   despite the sources compiling cleanly. No source files were affected; cleared with a forced
   script recompile, after which the build succeeded (row 18).
3. **Builds/ folder cleanup.** The loose files in `Builds/` (older bundles, their records, logs)
   were removed externally during the incident window. The 2.0 bundle + symbols were
   re-materialized by the following build and are now mirrored in `Builds/archive/` so a repeat
   cleanup cannot take the current artifacts.

## Known accepted gaps

1. **Pickup SFX** (`keyPickup_clip`, `pagePickup_clip`) left **unassigned** — no suitable clip
   exists in the project's licensed audio set; assigning an unfitting one was judged worse than
   silence. Revisit when a fitting free SFX is found.
2. **Adaptive launcher icon** not authored (square source; the launcher masks it).
3. **Extra lore-note props** deferred (six-page story system already carries the beat).
4. **Text scaling** (accessibility) deferred; reduced-flashing shipped in 2.0.
