# THE LAST BELL 2.0 — Test Report

All results below were produced **in-editor via the live Unity bridge** (`127.0.0.1:8080`) on
Unity 2022.3.62f3, Built-in RP. Everything that requires a **signed build or a physical
device** is listed as UNVERIFIED at the end — it could not be produced from this seat.

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

Notes on test 13: the Device Simulator models the Pixel 5 punch-hole in landscape; the clamp
was verified for both the no-op case (authored margin already sufficient) and the engage case
(exact expected offset). The first implementation had a scale-source bug (pixel width instead
of `scaleFactor`) — this live test caught it; the fixed version is what passed above.

## UNVERIFIED — requires a signed build or a physical device (user-side)

| Item | Why it is blocked |
|---|---|
| 2.0 signed AAB build | Keystore password is not stored (by policy); user enters it at build time |
| INTERNET actually removed from the merged manifest | Only provable by dumping a built bundle (`bundletool`/`aapt2`) — command in `RELEASE_CHECKLIST.md` §2 |
| App icon on a real launcher (mask shape, quality) | Device-side visual |
| Safe area on a real notched/punch-hole phone | Only the Simulator was available here |
| FPS / memory / thermal / loading times | Device measurements; README §12.9 remains the outstanding performance evidence |
| Audio output (levels, clipping) | Clips wired by design from existing assets; nobody has auditioned the 2.0 mix on hardware |
| Full end-to-end device run | Pre-existing outstanding item (README §12.8) |

## Known accepted gaps

1. **Pickup SFX** (`keyPickup_clip`, `pagePickup_clip`) left **unassigned** — no suitable clip
   exists in the project's licensed audio set; assigning an unfitting one was judged worse than
   silence. Revisit when a fitting free SFX is found.
2. **Adaptive launcher icon** not authored (square source; the launcher masks it).
3. **Extra lore-note props** deferred (six-page story system already carries the beat).
4. **Text scaling** (accessibility) deferred; reduced-flashing shipped in 2.0.
