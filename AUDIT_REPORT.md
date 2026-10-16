# THE LAST BELL — 2.0 Upgrade · Phase 0 Audit Report

**Status:** Phase 0 complete — **awaiting review before any implementation.** No gameplay code was changed in this phase (only the protective branch and this report were added).
**Date:** 2026-10-16
**Branch:** `feature/last-bell-2.0` (created from `main`; `main` itself is untouched)
**Baseline commit:** `87a4acec9063d01a0155583fd6c0884e160ceaf5` — "Release 2.0 (code 20): Save & Continue — checkpoint resumes, CONTINUE menu row, Part10 tooling"
**Engine:** Unity 2022.3.62f3 revision `96770f904ca7`, Built-in Render Pipeline, Android, IL2CPP, ARM64

---

## 1. Executive summary

- The repository is **healthy and release-grade**: 0 compiler errors, **0 missing-script references** (`m_Script: {fileID: 0}` count = 0 across all scenes/prefabs), **0 broken asset GUID references** (all 12,444 GUID references resolved; the 10 initially flagged were Unity package components, verified against `Library/PackageCache`), and no duplicate managers, AudioListeners or EventSystems in any scene.
- A working "Release 2.0 (code 20)" already exists on `main`: Save & Continue (checkpoint resumes, CONTINUE menu row) with 17/17 edit-mode and 37/37 runtime save tests passing, plus three signed AABs with verification records in `Builds/`.
- Phase 0 audited the project with **10 parallel read-only agents** + direct Unity-editor verification + deterministic filesystem scans. This report catalogs verified baseline, defects, risks and a phase plan. Key findings:
  1. **INTERNET permission is present in every built AAB** (0.2-v2, API36, 0.4-v4) and in the current Gradle pipeline output, while the game contains **zero networking code**. This contradicts the spirit of the "fully offline" promise and needs a Phase 5 manifest fix + rebuild verification.
  2. Several **authored-but-unwired audio features**: the ambient cue layer is inert (`ambientClips: []` in MainLevel), the monster entry sting and door/pickup sounds have no clips assigned. Atmosphere content (footsteps variation, door sounds, silence zones) does not exist yet.
  3. **Enemy fairness gaps**: teleport destinations are unvalidated (can land inside geometry/behind locked gates/outside bounds), the safe-place retreat jumps without a visibility check, and danger can kill from the player's blind side while trigger colliders never block line-of-sight.
  4. **UI polish issues introduced by the 6-row menu**: CONTINUE loses its bottom 7 units to row overlap and QUIT extends 13 units below the 20:9 safe band; no `Screen.safeArea` handling exists anywhere (notch risk); settings lack reset-to-defaults and a reduce-flicker/shake option.
  5. **Input robustness**: `ControlButton` dual-path handling can fire actions twice (flashlight toggles through the pause overlay); the look pad sums up to 10 fingers; the pause button overlaps the look-pad drag zone.
  6. **Documentation drift**: the README graphics-tier table and pause-button corner contradict the code; ambient cadence is documented as active but is inert; "two local `file:` packages" is actually one.
- The 2.0 Save & Continue work is regression-tested and re-runnable (`Tools/Part10 ▸ ...` menu items). No automated NUnit test suite exists; the editor menu tooling (41 items) is the de-facto test harness.
- **No blockers to the main gameplay loop were found.** Proposed next step: Phase 1 (stability + preservation) as scoped in §12–13.

---

## 2. Repository & protection state

| Item | Value |
|---|---|
| Git history | 7 commits total; `dfc241c` (baseline port) → … → `f7918aa` (0.4) → `87a4ace` (2.0 Save & Continue) |
| Branches | `main` = `origin/main` = `87a4ace`; **new:** `feature/last-bell-2.0` = `87a4ace` |
| Tags | none |
| Working tree | clean except intentionally untracked `Packages/com.bezi.sidekick/` (editor tooling; embedded, not in `Packages/manifest.json`) |
| Original hash recorded | `87a4acec9063d01a0155583fd6c0884e160ceaf5` |

**Backup confirmation:** branching was safe (clean tree), so a separate archive copy was not required. The original baseline is protected three ways: `main` locally, `origin/main` on GitHub, and this audit branch that starts from the identical commit. No destructive commands (reset/clean/force-push) were used at any point.

---

## 3. Verified project configuration

Sources: `ProjectSettings/*.asset` (line-cited) + live editor query at audit time.

| Setting | Verified value | Evidence |
|---|---|---|
| Unity version | 2022.3.62f3 (`96770f904ca7`) | `ProjectSettings/ProjectVersion.txt:1`; live query |
| Product / company | "Last Bell" / "HAMZA POWERPLAYER" | live query |
| Render pipeline | Built-in RP (no SRP asset assigned, all quality tiers) | `GraphicsSettings.asset:45`; live query `null` |
| Build target / scenes | Android; level0..4 = Menu, MainLevel, Bedroom, GameWin, GameOver (all enabled) | `EditorBuildSettings.asset:8-22`; live query |
| Application ID | `com.hamzapowerplayer.lastbell` | `ProjectSettings.asset:166`; AAB records |
| Version | `bundleVersion 2.0`, `AndroidBundleVersionCode 20` | `ProjectSettings.asset:144,173`; live query |
| Min / target SDK | 24 / 36 | `ProjectSettings.asset:174-175`; live query; 0.4 AAB record |
| Architectures | ARM64 only | `ProjectSettings.asset:266` (value 2); live query; 0.4 record |
| Scripting backend | IL2CPP | `ProjectSettings.asset:741-742`; live query |
| Orientation | Landscape (LandscapeRight default; auto-rotate flags on) | live query; `ProjectSettings.asset:11,61-65` |
| Active input handling | **Both** (legacy + Input System) — known Android build warning, intentional | `ProjectSettings.asset:847` |
| Quality tiers | Very Low, Low, Medium, High, Very High, Ultra; Android default = Medium; editor session currently Ultra | `QualitySettings.asset`, live query |
| Keystore | `C:/MOBILE-Keys/lastbell.keystore`, alias `lastbell`, custom keystore ON; password not stored in repo (editor session only) | `ProjectSettings.asset:270-271,279`; keystore file exists |
| App icons | **0 of 6 Android icon slots assigned** (AABs currently ship Unity default icon resources) | live query; `LastBellAppIcon.jpg` exists in `Assets/Main/UI/Art` |
| Internet access | `forceInternetPermission = false` yet INTERNET permission is emitted (see §7) | live query; `ProjectSettings.asset:181` |

**Baseline Android buildability: YES.** The toolchain has produced three signed AABs (see §9); a fresh build requires the keystore password to be re-entered in the editor session (by design, not stored).

---

## 4. Scene & runtime wiring

Per-scene component inventory (scripts resolved via `.meta` GUIDs; package components verified in `Library/PackageCache`):

| Scene | GameObjects | AudioListener | GameManager | Monster | SpawnItem | EventSystem | InputSystemUIInputModule | Notes |
|---|---|---|---|---|---|---|---|---|
| Menu (level0) | 38 | 1 | – | – | – | 1 | 1 | MainMenuUI ×1; legacy `MenuAction` canvas still present (disabled/hidden) |
| MainLevel (level1) | 194 | 1 | 1 | 1 | **18** | 1 | 1 | ProgressionGate ×2, ExitTrigger ×1, PauseMenuUI ×1 |
| Bedroom (level2) | 13 | 1 | – | – | – | 1 | 1 | end sequence |
| GameWin (level3) | 10 | 1 | – | – | – | 1 | 1 | |
| GameOver (level4) | 11 | 1 | – | – | – | 1 | 1 | |
| SampleScene | 2 | 1 | – | – | – | 0 | 0 | **not in build** — leftover |

Flags (all verified clean): no scene has duplicate AudioListener/GameManager/EventSystem; `StandaloneInputModule` count in build scenes = **0** (removed by `87a4ace`; it survives only in the unused `Assets/ThirdAssets/UJoystick/Example/Scene`); `TouchInputModule`/`StandaloneInputModule` only appear in that example scene. Remaining cosmetic leftover: a `BaseInput` (uGUI, inert) component on EventSystem objects in several scenes (e.g. `Bedroom.unity:1955`) — 5 references total.

Lighting settings: `MenuSettings`, `BedroomSettings`, `GameWinSettings`, `GameOverSettings` `.lighting` assets referenced by their scenes; MainLevel embeds its lighting settings inline (`MainLevel.unity:100`).

---

## 5. Systems audit

### 5.1 Enemy / AI (`Monster.cs` 284 LOC, `DetectMonster.cs` 43 LOC)

**Current behaviour (verified numbers):**
- Inactive until the first key is collected (`Monster.cs:122`).
- Escalation per keys held: distance ray 12 → 14.5 → 17 m; facing factor 20 → 24 → 26; teleport interval 10 → 8 → 6 s (`Monster.cs:96-104`). During the escape act: +3 m distance, ×1.5 facing, −2 s interval (min 3.5 s).
- Teleport: random XZ within ±30 units of the player, ≥8 m away, Y = player.y − 2.24, up to 24 bounded re-rolls preferring a 60°-in-front cone; fallback picks the largest-angle candidate (`Monster.cs:165-196`). Scene overrides: `spawnRate=30`, `lifeDuration=7`, `distanceRayMax=30` (`MainLevel.unity:27770-27815`).
- Danger meter: ramps to a ceiling of 10; death threshold 6.9; danger window ≈7 s (`Monster.cs:57-59`); LOS check is a single throttled forward ray every 0.12 s (`Monster.cs:131-136`).
- Death → `LoadScene("GameOver")` (`Monster.cs:260-267`); safe-place retreat = fixed-point jump with 3 s re-arm (`Monster.cs:105-125,239`).

**Fairness risks (Phase 2 targets):**
- Teleport destinations are unvalidated — no floor/NavMesh/bounds test; the entity can land inside geometry, outside the level, or behind locked gates (`Monster.cs:171-174`).
- Safe-place retreat ignores visibility/distance — can appear in view (`Monster.cs:105-124`).
- Trigger colliders never block LOS (`QueryTriggerInteraction.Ignore`, `Monster.cs:135,216`); danger uses the monster's forward, so it can kill from the player's blind side — needs a design decision (intent vs fairness).
- No locomotion/pathing: embedded placements only resolve via the teleport timer.
- Flashlight counterplay: the trigger object has no Rigidbody (grep found none) — UNVERIFIED whether the flashlight mechanic actually functions; needs a play-mode test.

**Performance:** benign. One throttled raycast (~8.3/s), ≤24 Linecasts per teleport (max once per ≥3.5 s), no allocations/`GetComponent`/`Find` in `Update`; early-out before the first key.

### 5.2 Audio & atmosphere

- Volume: per-source runtime application (`AudioVolumeController.cs:98`), master = `AudioListener.volume`. The `AudioMixer` path is **dead code**: `GameMixer.mixer` has zero exposed parameters (`GameMixer.mixer:51`); no source is routed to mixer groups; `GameSettings.ApplyVolumes(AudioMixer)` has no callers. `AudioSource.PlayClipAtPoint` one-shots are untracked by the SFX slider (`AudioVolumeController.cs:10`).
- Ambient scheduler exists (12–35 s cadence, first cue 8–20 s, `AmbientAudioManager.cs:23-32`) but the shipped scene has `ambientClips: []` (`MainLevel.unity:25816`) → **the cue layer never plays**. Legacy `randomSounds` (90–240 s, self-destruct) still runs.
- **Unassigned audio hooks**: monster entry sting sources = 0 on all 4 instances (e.g. `MainLevel.unity:5599-5600`); `door_clip: 0` (`MainLevel.unity:8634`); pickup clips = 0 (`MainLevel.unity:36082-36083`) though `PlayPickup` is called (`GameManager.cs:512,529`); `ProgressionGate` is silent by design.
- Risks: no ambient repeat-prevention; duck fallback ignores role gain and can stick (`StairsAmbience.cs:63,80`); flashlight click `Play()` unguarded (`Flashlight.cs:91-93`); footstep is a single clip with fixed pitch (`Controller.cs:72,91`).
- Inventory: 9 audio files, 12.36 MB total (heaviest: ambient bed 7.37 MB, atmosphere 1.91 MB). Footstep variation: **does not exist**. Door sounds in gameplay: **do not exist**. Intentional-silence zones: **do not exist**. FlickerLight components in MainLevel: **7**.
- `OpeningCutscene`/`EndingCutscene` stop their sources properly (checked).

### 5.3 UI / UX

- Two scaling conventions coexist: menus use 800×600 match-**width** (`MainMenuUI.cs:116-119`, `PauseMenuUI.cs:130-133`), while Bedroom/GameWin/GameOver use ConstantPixelSize (`GameOver.unity:569` etc.). Usable band on 20:9 ≈ ±180 units.
- **6-row menu issues (introduced with CONTINUE):** with a save, `rowFirstY=102`, `rowPitch=53` (`MainMenuUI.cs:138-139`) — rows overlap 7 units (later rows win raycast depth, so CONTINUE loses its bottom 7 units of tap area) and QUIT's rect bottom sits 13 units below the ±180 safe band (≈37.6 dp tappable). Needs an on-device check and a layout fix.
- **No `Screen.safeArea` handling anywhere** — notch/cutout overlap risk for HUD and menus.
- Touch targets: menu rows 48 dp; HUD pause 57.6 dp; interact/open-door ≈53 dp; flashlight ≈53×48 dp — all ≥48 dp except **settings steppers 30.4×24 dp** and BACK 30.4 dp height (below the 48 dp guideline).
- Settings coverage: master/ambient/SFX volumes ✅, look sensitivity ×0.4–×2.0 ✅, graphics tier Low/Medium/High ✅; **missing:** reduce-flicker/camera-shake option, reset-to-defaults.
- Other observations: `DeathImage` is invisible (alpha 0) but `raycastTarget: 1` — possible tap blocker after death (UNVERIFIED, check in Phase 1); HUD pause button (top-right) sits inside the look-pad drag zone and may rotate the camera while tapping (UNVERIFIED); procedural menu sprites are cheap (~7.4k px, cached, one-time).

### 5.4 Input pipeline

- EnhancedTouch (`EnhancedTouchInitializer.cs:8-13`, forces 60 fps) → `ControlStick` (fixed, left thumb, dead zone 0.17) → `ControlPad` (right-half drag look, dead zone 0.1) → `Controller` reads fields directly. Movement `SimpleMove`, camera lerp ×20.
- Duplicate-handling risks: `ControlButton` has both pointer-event and EnhancedTouch paths — double-tap/action firing possible (`ControlButton.cs:57-75` vs `:103-154`); it also lacks a pause check, so the **flashlight can toggle through the pause overlay** (Controller only resets stick/pad, `Controller.cs:42-46`). The look pad sums **all** claimed fingers (up to 10) into one delta (`ControlPad.cs:14-16,45-67`). `RectangleContainsScreenPoint` is called without a camera (`ControlPad.cs:31`). No occlusion filter — the pad overlaps `Button_Interact` and half of `Button_Flash`.
- Back/pause handling is centralized enough (Escape handled in PauseManager/PauseMenuUI/MainMenuUI; cutscenes have their own skips), but there are 6+ key-handling sites to keep aligned.
- Legacy mouse fallbacks are correctly gated by `!Application.isMobilePlatform` (`GameManager.cs:211`, `ExitTrigger.cs:61`).

---

## 6. Asset integrity & weight

**Integrity (deterministic scans, evidence-cited):**
- 12,444 GUID references scanned across scenes/prefabs/assets/materials/animators; 701 local meta GUIDs; initial flag list = 10 GUIDs, **all verified as Unity package components** in `Library/PackageCache` (2D InputSystemUIInputModule, EventSystem, BaseInput, StandaloneInputModule, TouchInputModule, Button, GraphicRaycaster, Image, Text, CanvasScaler). **Broken references: none.**
- `m_Script: {fileID: 0}` (missing script) count: **0**.
- Leftovers noted: `SampleScene` (not in build), UJoystick example scene/scripts (third-party sample, not in build), inert `BaseInput` components (see §4). Cleanup candidates only — not touched in Phase 0.

**Weight (mobile-relevant):**
- Totals: textures 234 files / 86.6 MB; audio 9 files / 12.4 MB; models 110 / 9.8 MB; prefabs 118 / 0.8 MB; scenes 10 / 2.7 MB.
- Top offenders: monster textures `NORMALMJ.png` 16.05 MB, `RGBMJ.png` 13.12 MB, `NORMALHair.png` 4.53 MB, `RGBHair.png` 2.45 MB (all 2048, normal-map RGB), `FloorTilesDeffuse.png` 6.66 MB, ambient bed MP3 7.37 MB, monster FBX 5.42 MB.
- Import findings: all sampled audio = Vorbis compressed, 44.1 kHz, preload-on; sampled textures ≤2048 (no 4096+ anywhere); only the menu backdrop has an Android platform override (1024, format 50).
- AAB size baseline: **39.0 MB** (0.4-v4, `40,875,400` bytes).

---

## 7. Android, offline & privacy

- **Networking sweep: clean.** Zero hits for `UnityWebRequest|WebRequest|Socket|System.Net|HttpClient` in runtime scripts; no ad/analytics SDKs anywhere in Assets/Packages/ProjectSettings; Unity cloud services all disabled (`UnityConnectSettings` entries `m_Enabled: 0`; `cloudEnabled: 0`). Persistence = PlayerPrefs only.
- **INTERNET permission is present in every built AAB** (verified by inspecting `base/manifest/AndroidManifest.xml` inside `LastBell-0.2-v2.aab`, `LastBell-API36.aab`, `LastBell-0.4-v4.aab` — encoded protobuf contains exactly one `uses-permission` entry: `android.permission.INTERNET` — and the current `Library/Bee/.../unityLibrary/src/main/AndroidManifest.xml` has `<uses-permission android:name="android.permission.INTERNET" />`). Player Settings show `forceInternetPermission = false`. Unity 2022.3 has no "Deny" option; removal requires a custom main manifest (`tools:node="remove"`). The project keeps `Assets/Plugins/Android/AndroidManifest.xml.DISABLED` (declares no permissions; not compiled). **The app makes no network calls, but the permission declaration must be removed and rebuilt/verified — Phase 5 item.**
- Editor-only flags to clean up in passing: `submitAnalytics: 1` (`ProjectSettings.asset:91`, editor CRT toggle; runtime analytics absent).
- Packages: 42 dependencies; one non-registry entry `com.coplaydev.unity-mcp` = `file:C:/unity-mcp-main/MCPForUnity` (dev bridge, absolute local path — blocks CI and clean-clone builds until replaced/removed for release builds); `com.bezi.sidekick` is embedded-untracked (editor tooling). All registry deps are standard Unity packages.
- Android lifecycle/back-button: handled via legacy Escape path (works under "Both" input handling); pause/resume audio and timeScale handled by `PauseManager`.

---

## 8. Documentation vs implementation (verification of README claims)

Claims 1–12 and 14: **VERIFIED** against code/config, including: 18 spawn spots; 6 pages (clueIndex 0–5); 3 key stages; 30 s escape timer (`MainLevel.unity:36077`); danger ceiling 10 / threshold 6.9 / life 7 s; teleport ±30 u / ≥8 m / 24 re-rolls; flashlight battery drain off; look sensitivity 0.15°/px, ×0.4–×2.0; fixed left stick + right-half drag look; CONTINUE (n/3 KEYS) / NEW RUN menu behaviour; `Save.State/Version/Keys/Pages/Gates/Layout` semantics; minSdk 24 / target 36 / ARM64 / IL2CPP; 0.4 AAB record exists with "jar verified".

**Mismatches found (doc bugs to fix in Phase 1):**
1. **Graphics tier table** (`README.md:387-393, 677-678`) does not match `GameSettings.cs:61-87` (Low: hard shadows/15 m vs code Disabled/10 m/1 light; High=Medium vs code All/30/4). Runtime code is authoritative for devices.
2. **Pause button corner**: README says top-left (`README.md:202`); it is top-right (`PauseMenuUI.cs:156-159`).
3. "Two local `file:` packages" (`README.md:547`) — actually one (`com.coplaydev.unity-mcp`); the other is embedded.
4. Ambient cue layer described as active (§5) but inert in the shipped scene (`MainLevel.unity:25816`).
5. Cosmetic: `RUN! N s` vs actual `RUN! 30s` (`GameManager.cs:243`).

---

## 9. Baseline metrics & artifacts

| Artifact (Builds/) | Version | Size | Verdict |
|---|---|---|---|
| `LastBell-0.2-v2.aab` (+ symbols 62.6 MB) | 0.2 / code 2 | 39,799,077 B | jar verified; 0 errors / 2 warnings |
| `LastBell-API36.aab` (+ symbols 62.9 MB) | 0.2 / code 2 (API36 SDK test build) | 40,873,291 B | no verification record on file |
| `LastBell-0.4-v4.aab` (+ symbols 62.9 MB) | 0.4 / code 4 | 40,875,400 B | jar verified; 0 errors / 7 warnings; SHA-256 `CA48…6804` |
| `LastBell.aab` (pre-fix, Oct 1) | — | 42,794,947 B | record too thin; carried INTERNET, minSdk 22 |

- Editor console at audit time: **clean of game errors/warnings**; single entry = MCP bridge WebSocket notice. Editor: edit mode, `MainLevel` active, not compiling.
- 2.0 Save & Continue test evidence (from the shipped work): `Tools/Part10 ▸ Verify Save Round-Trip (edit mode)` 17/17; `Tools/Part10 ▸ Runtime Save Continue Test (while playing)` 37/37 (fresh-run checkpoints, key/page/gate checkpoints, revive reload restore, app-restart CONTINUE path, stale-save guard).
- **Not measurable in this environment (honest gaps):** device FPS/memory/thermal, real loading times, low-memory behaviour, play-store bundle install size — all require the user's device (Phase 5/6). Editor-side profiling would not be representative.
- Historical build warnings (0.4): (1) Active Input Handling = Both (intentional), (2) >1 per-pixel light on mobile (tier caps exist at runtime), (3) EventSystem dual-module warning — **root cause removed in `87a4ace`** (StandaloneInputModule no longer in build scenes); re-verify at the next build.
- Automated tests: **none (no NUnit/`[Test]` anywhere)**. The editor menu suite (`Tools/...`, 41 items, 11 files) is the working test harness; `Tools/Part10` covers save/continue best.

---

## 10. Verified working-feature baseline (preserve list)

Menus (main with CONTINUE/NEW RUN/SETTINGS/CREDITS/LICENCES/QUIT, pause, settings persistence, licences); school exploration in MainLevel; player movement + touch controls (fixed stick, right-half look, sprint ring); flashlight toggle; interaction prompts; 3-stage key collection + 2 progression gates; 6 story pages + page viewer; monster encounters with escalation, danger meter and safe place; death → GameOver → revive (restores checkpoint) ; escape act (30 s) → win → Bedroom ending; Save & Continue checkpoint system (new in 2.0); fully offline runtime; Android release configuration (IL2CPP/ARM64/AAB); signed-build pipeline with verification records.

---

## 11. Risks & unresolved issues (prioritized)

| # | Priority | Issue | Where | Phase |
|---|---|---|---|---|
| 1 | High | INTERNET permission in all shipped AABs despite zero networking | §7 | 5 |
| 2 | High | Enemy teleports unvalidated (inside geometry/out of bounds/behind gates); safe-place jump visible | `Monster.cs:105-124,171-174` | 2 |
| 3 | High | Ambient cue layer inert; sting/door/pickup clips unassigned — atmosphere below authored intent | §5.2 | 2 |
| 4 | Med-High | 6-row menu overlap (CONTINUE −7 u tap area; QUIT 13 u below safe band); no `Screen.safeArea` support anywhere | `MainMenuUI.cs:138-150`; §5.3 | 4 |
| 5 | Med-High | Flashlight can toggle through pause overlay (ControlButton lacks pause guard); double-fire risk | `ControlButton.cs:57-154`, `Controller.cs:42-46` | 1 |
| 6 | Med | DeathImage invisible but `raycastTarget:1` — possible tap blocker (UNVERIFIED) | `MainLevel.unity:17228-17247` | 1 |
| 7 | Med | Pause button inside look-pad drag zone (camera drift on tap, UNVERIFIED) | `PauseMenuUI.cs:157-159`, `ControlPad.cs:31` | 1 |
| 8 | Med | Doc drift: graphics tiers, pause corner, file:-packages count, ambient claim | §8 | 1 |
| 9 | Med | Large assets (26 MB monster textures, 7.4 MB ambient MP3) vs 39 MB AAB | §6 | 5 |
| 10 | Med | No automated tests; file:-package blocks CI/reproducible builds | §9, §7 | 1/5 |
| 11 | Low | Settings steppers 30×24 dp (<48 dp); no reset-defaults; no reduce-flicker/shake | §5.3 | 4 |
| 12 | Low | Leftovers: SampleScene, UJoystick example scene, inert BaseInput components, dead mixer code/dead methods | §4, §5.2 | cleanup |
| 13 | Low | Missing play-mode verifications: flashlight counterplay, DeathImage tap-block, pause overlap | §5 | 1/6 |

No blockers found for the main gameplay loop.

---

## 12. Proposed 2.0 changes (mapped to the program phases)

**Phase 1 — Stability & preservation (no rewrites):** fix risk #5–#8 and #13 verifications; remove dead methods only after confirming; keep all serialized field names; re-run `Tools/Part10` + smoke tests; add regression checks for menu rows, pause/back, pickups and save/continue; write `CHANGELOG_2.0.md`.

**Phase 2 — Atmosphere & enemy intelligence:** wire the authored audio (ambient clips list, entry sting, pickup/door sounds); remove ambient-repeat collision; add footstep variation; honest flicker control hook (prepare for reduce-flicker option); enemy: keep the existing architecture but add explicit states around it (idle → investigate → chase → attack → return) with LOS-validated teleports (floor raycast + bounds + gate-awareness), visibility check for the safe-place jump, and capped fairness rules; document the state diagram; playable test encounter.

**Phase 3 — Content expansion (needs scope decision):** candidate additions per the master prompt (a small number of memorable rooms, optional notes/puzzle, secret-ending feasibility) with save-layout compatibility (the layout codec already discards stale layouts safely on count mismatch, `RunSave.cs`); original ending preserved; every objective gets interaction + success feedback; verify nav/lighting/collision.

**Phase 4 — Visual identity & UI:** safe-area wrapper; fix the 6-row menu layout (spacing/raycast); larger steppers; reset-to-defaults; reduce-flicker/camera-shake option; unify scaler conventions across scenes; HUD polish; assign app icons; keep Built-in RP (no pipeline change).

**Phase 5 — Android performance & offline guarantees:** root-cause + remove INTERNET permission and verify in a rebuilt AAB; texture/audio import passes based on §6; quality-preset tuning tied to measured device results; lifecycle checks (back button, interruptions); reproducible build instructions; document `file:` package removal path for release builds.

**Phase 6 — QA & release readiness:** full loop + endings QA with the user's device evidence; extend automation (wrap `Tools/Part10` checks into EditMode tests where feasible); produce `RELEASE_CHECKLIST.md`, `TEST_REPORT.md`, `CHANGELOG_2.0.md`; final signed AAB + verification record (keystore password entered by the user); remaining-known-issues list.

---

## 13. Phase-by-phase plan & gates

| Phase | Entry condition | Exit evidence | Gate |
|---|---|---|---|
| 1 Stability | This report approved | Changed files listed + tests re-run + `CHANGELOG_2.0.md` | **STOP for review** |
| 2 Atmosphere/AI | Phase 1 approved | AI state diagram + wired audio + playable test encounter | **STOP for review** |
| 3 Content | Phase 2 approved + scope decision | Level plan + objective checklist verified in-editor | **STOP for review** |
| 4 UI/Visual | Phase 3 approved | Verified menu/HUD frames per aspect ratio (in-game captures) | **STOP for review** |
| 5 Android/Perf | Phase 4 approved | Rebuilt AAB (permission check) + device measurements (user) + docs | **STOP for review** |
| 6 QA/Release | Phase 5 approved | Signed AAB + verification record + release docs; on-device checklist | release |

Working method per phase (per the master prompt): state goal + files → inspect dependencies → smallest complete change → review → run tests and record real results → summarize changes/risks/not-tested → continue. No fabrication; anything not verified is marked.

## 14. Decisions needed from review

1. **Approve Phase 1 start** (stability fixes as scoped in §12/13).
2. **Phase 3 content scope** — the master prompt adds content expansion; confirm go/no-go and limits (recommendation: 1–2 strong additions, secret ending only if feasible within the existing trigger architecture).
3. **Version strategy** — `main` already ships as "2.0 (code 20)". Recommendation: keep `2.0/20` during development; at the next signed build decide `2.0 (code 21)` (re-release) vs `2.1` (if you want to distinguish from what's already out).
4. **INTERNET permission removal** (Phase 5) — approve the manifest change + required rebuild verification.
5. **Build & device access** — keystore password entry and on-device testing remain user-side (Phases 5–6).

## 15. Phase 0 limitations (not verified — stated honestly)

- No device measurements (FPS/memory/thermal/load times) — no device access.
- Play-mode behaviours flagged UNVERIFIED: flashlight counterplay, DeathImage tap blocking, pause-overlap camera drift, ControlButton double-fire repro. All are Phase 1 checks.
- Root cause of Unity emitting INTERNET was not conclusively established (Unity 2022.3 manifest-merge behaviour); the fix will be developed and verified during Phase 5.
- No build was produced in Phase 0 (by agreement; the 2.0 AAB build was deferred by the user).

---

*Method: 10 parallel read-only audit agents (scenes/wiring, scripts/dependencies, asset weight, enemy AI, audio/atmosphere, UI/UX, input, Android/offline, docs-vs-code, history/QA tooling) + direct Unity-editor queries via the MCP bridge + deterministic filesystem scans (GUID integrity, AAB manifest inspection, git pickaxe). All findings are evidence-cited; UNVERIFIED items are marked as such.*
