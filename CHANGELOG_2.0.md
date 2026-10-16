# THE LAST BELL 2.0 — Changelog

Branch `feature/last-bell-2.0` · version stays **2.0** (version code 20) · 2026-10.

Commits: Phase 0 audit `b34c625` · Phases 1–2 `e766ea7` · Phases 3–6 (this commit).

---

## Phase 0 — Audit (committed `b34c625`)

- Full repository/scene/asset audit written to `AUDIT_REPORT.md` (integrity scans, build-config
  verification, mobile-risk inventory, UI overflow measurements, manifest/INTERNET origin
  trace). No gameplay changes.

## Phase 1 — Input & pause stability

- **Pause button**: kept at **top-right** (documented decision — moving it was rejected; docs
  fixed instead, see README §Controls and the `PauseMenuUI` header comment).
- `ControlButton`: pause-press guard + duplicate-press dedupe + `OnPointerDown` guard.
- `ControlPad`: `IsOverUI` occlusion guard — touches that start on interactive UI no longer
  drive look-drag (so the `II` pause button is not draggable-into).
- `DeathImage` in `MainLevel`: `raycastTarget` turned off (invisible full-screen blocker).
- Verified: `Tools/NeKONTROLLER ▸ Verify Touch Controls` PASS ×3; `Tools/Part10 ▸ Verify Save
  Round-Trip` 17/17 PASS (re-run after Phase 2, still 17/17).

## Phase 2 — Atmosphere, audio wiring, enemy fairness

Audio (reuses **existing** assets only; no new files):

- Corridor ambience bed: `FL1_AmbientAudio.ambientClips` = [atmosphere-09, freesound-ambience-01,
  impact-hit] (the README's ambient-layer claim is now true).
- Stairs tension: 4× `FL2_StairsCue.entrySting` = impact-hit.
- Exit door cue: `ExitTrigger[MainDoorTrigger].door_clip` = impact-hit, 1.0 s delay.
- Gate unlock cue: `ProgressionGate.openClip` = impact-hit ×2, played on player unlock only
  (not `OpenInstant`), at `0.9 × GameSettings.SfxVolume`.
- Footsteps: per-burst pitch jitter (0.94–1.06) in `Controller.cs`.
- **Pickup SFX intentionally left empty** — no suitable clip in the existing asset set;
  documented gap (see `TEST_REPORT.md`).

Enemy fairness (`Monster.cs`):

- `IsValidDestination()` — teleport candidates validated by floor raycast + clearance sphere.
- Safe-place retreat now checks distance/hidden/validity, falls back to a validated hidden spot.
- Derived `BehaviorState` enum + ladder (see `AI_STATE_DIAGRAM.md`).
- Emergent asymmetric awareness (sight gates attack, hearing keeps pressure) documented as
  intentional.
- Runtime-tested: 25/25 teleports valid + moved; rigged mid-air safe place correctly refused.

## Phase 3 — Secret ending (six-page "Truth" variant)

- `ExitTrigger.GameWin`: writes `Save.Truth` (1 when all **6** story pages were collected)
  **before** `EndRun()` so the write persists through run-clear.
- `EndingCutscene`: consumes the flag once; plays an extra ending beat —
  *"THE LOG IS COMPLETE / Six pages. One name. Yours."* — after the standard ending.
- Verified live: six page picks → `PagesCollected=6`; cutscene instance read `secretTruth=True`;
  flag consumed (absent afterwards). No save-schema change.
- Deferred: additional lore-note props (the six-page system already carries the narrative;
  new props would need new art/text authoring for marginal value).

## Phase 4 — UI / accessibility / polish

- **Six-row main menu fixed**: rows now pitch 51 u with 51 u tap rects (`rowFirstY=104`) —
  the old 53/60 layout let adjacent tap rects overlap by 7 u and pushed QUIT to −193 u,
  off the ±180 u landscape band. Verified live: zero overlap, span +129.5 → −176.5.
  (`LastBellMenuStyle.rowHeight` added; `MainMenuUI` uses it.)
- **Reduced-flashing option** (`GameSettings.ReduceFlashing`, key `opt_reduceFlashing`):
  new FLASHING row in **both** settings panels; `FlickerLight` skips hard tube dropouts and
  softens the swing; the menu title no longer dips.
- **RESET DEFAULTS** button in both settings panels (`GameSettings.ResetToDefaults()`).
- **Safe-area handling**: new `SafeAreaClamp` component (edge-anchored UI is pushed inside
  verified safe areas; no-op otherwise), applied to the HUD pause button. Verified live against
  the Device Simulator (Pixel 5 landscape): 136 px punch-hole inset detected, default no-op,
  `padding=40` → exact expected offset, restore → exact authored value. (The first version had
  a scale-source bug; the live test caught it and the fix is verified.)
- **Android app icon**: `LastBellAppIcon.jpg` (2048×2048, readable + uncompressed) assigned to
  **all 6** Player Settings icon slots (was 0/6). Launcher visual check is device-side.
- README corrections: pause button top-right, `RUN! Ns` label, graphics-tier table matched to
  actual `GameSettings` values, settings table (new rows), `file:` package counts.

## Phase 5 — Offline release hardening

- Custom main manifest `Assets/Plugins/Android/AndroidManifest.xml`: mirrors Unity's launcher
  template and adds `<uses-permission android:name="android.permission.INTERNET"
  tools:node="remove"/>`. INTERNET originates in Unity's **unityLibrary** manifest; the launcher
  manifest outranks it in the merger, so the directive wins. XML validated after write.
- Superseded `AndroidManifest.xml.DISABLED` removed (it was the wrong shape — it redeclared the
  activity without `android:exported`).
- Post-build verification (INTERNET grep) documented in `RELEASE_CHECKLIST.md` — **requires a
  build, which is user-side (keystore password).**

## Phase 6 — Documentation & evidence

- `CHANGELOG_2.0.md` (this file), `AI_STATE_DIAGRAM.md`, `RELEASE_CHECKLIST.md`,
  `TEST_REPORT.md`, README status/roadmap refresh, proof screenshot
  `Screenshots/lastbell_menu_6row_2.0.png`.

## Not changed (by policy)

- Version stays 2.0/20. No render-pipeline, input-era, or package-version changes.
- `Packages/com.bezi.sidekick/` untouched. Working serializer fields preserved.
- No asset/scene/reference deletions beyond the superseded disabled manifest (git-tracked).
