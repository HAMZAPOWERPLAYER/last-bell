# Last Bell

A first-person mobile horror game set in a school at night. Find three keys, avoid the entity,
and get out. Made for Android.

This is a port/derivative of **High School Horror Android** by
[yurifarion](https://github.com/yurifarion/High-School-Horror-Android-Unity3D) (CC-BY-4.0),
reworked for mobile with new touch controls, lighting, settings and menus. See
[Credits & licences](#credits--licences).

---

## Quick facts

| | |
|---|---|
| Engine | Unity **2022.3.62f3 LTS** |
| Render pipeline | **Built-in RP** (URP is intentionally *not* used — see [Known limitations](#known-limitations)) |
| Package ID | `com.hamzapowerplayer.lastbell` |
| Product name | Last Bell |
| Company | HAMZA POWERPLAYER |
| Version | `0.2` (bundleVersion) · AndroidBundleVersionCode `2` |
| Orientation | Landscape |
| Active build platform | `Android` — release AAB builds with **IL2CPP / ARM64** (see [Building for Android](#building-for-android)) |
| Input | Unity Input System **and** legacy Input Manager (`Active Input Handling = Both`) |
| Connectivity | Fully offline — no login, no accounts, no ads, no analytics |

---

## Objective

**Primary mission — escape.** Explore the school, find **3 keys**, then reach the main exit door.
The exit prompt only appears once you hold all three. Escaping loads `Bedroom` → `GameWin`.

**Collectibles — 6 pages.** Six readable notes are scattered through the level carrying
environmental lore. They are optional to escape but are the game's storytelling.

**Failure.** If the entity catches you in view for too long you die and the game loads `GameOver`.
Death does **not** clear your keys; restarting via `MenuAction.revive()` clears
`PlayerPrefs "Keys"` and reloads `MainLevel`.

**The entity is inert until you pick up your first key.** Before then nothing stalks you.

### Key / page placement

Placement is randomised every run. `GameManager.initSpawns()` finds all **18** `SpawnItem` spots and
rolls each one: 6 become Pages, 3 become Keys, the rest stay empty. A spot can only hold one item.

---

## Controls (mobile)

| Action | Input |
|---|---|
| Move | Left thumbstick (fixed, bottom-left). **Analog** — partial deflection is partial speed. |
| Sprint | Push the stick to its outer ring (`FullInput`). |
| Look | **Touch-drag anywhere on the right half of the screen.** Not a joystick — drag delta drives the camera. |
| Flashlight | On-screen button — plays a click SFX. |
| Interact / pick up | On-screen button, appears when in range of a key or page. |
| Pause | HUD button (top-left) or the **Android back button**. |

**Look sensitivity** baseline is `0.15° per pixel of drag`. Adjustable in Settings from ×0.4 to ×2.0.

**The Android back button opens the pause menu; it no longer quits the game.**

---

## Game systems

### The entity — `Monster.cs`

The entity **never walks**. There is no NavMesh agent and no pathfinding: it **teleports** around
the player and rotates to face them.

- **Teleports** to a random point within ±30 units of the player, on the player's own floor.
- **Avoids being seen**: destination search is bounded (24 attempts) and prefers spots outside a
  60° view cone or occluded by level geometry, rejecting anything within 8 m of the player.
- **Sight** is a single forward ray to the player, plus a **flashlight trigger volume**
  (`DetectMonster` on `FlashLightTrigger`) — shining the light on it is noticed.
- **Safe place**: while not seen and not in danger it returns to `SafePlaceSpawn` on a timer.

### Danger meter

Not a 0–1 value. It ramps toward `dangerCeiling` (10) over `lifeDuration` (**7 s** in the scene)
while you are both near and in view, and decays when you break line of sight.

- Death at `dangerDeathThreshold` = **6.9** → roughly a **4.83 second reaction window**.
- All of these are serialized fields on the `Monster` component, tunable without code changes.

### Flashlight — `Flashlight.cs`

- Toggles with a click sound effect.
- **Infinite battery by default** (`batteryDrainEnabled = false`). A full drain system exists and is
  correct, but is deliberately **off**: the flashlight is the player's only light source and there
  are no battery pickups, so a drain would make the game unwinnable.
- `Addbattery(seconds)` is implemented and re-ignites a dead light, ready for pickups if the drain is
  ever enabled. The battery UI shows a percentage when draining, `--` when unlimited.

### Audio

| Layer | Source |
|---|---|
| Ambient bed | `Ambient_SchoolNight_Bed` — 193 s seamless loop, Vorbis mono q0.45 (~1.1 MB in memory) |
| Random cues | `AmbientAudioManager` — creaks / distant sounds on a 12–35 s random interval |
| Footsteps | Play while **moving and grounded** (they do not loop while standing still) |
| One-shots | Flashlight click, horror scream, `back sound` |
| Stairs | 4 trigger volumes duck the ambient bed while you are on the stairwell |

Volume is split **Master / Ambient / SFX**, persisted and applied at runtime by
`AudioVolumeController` (per-source scaling — no AudioMixer groups required).

### Lighting

Floor 1 was originally **completely unlit** (ambient colour only). Fixtures were added:

| Space | Mood |
|---|---|
| Floor 1 corridor | 3 × sickly-green fixtures, **flickering** with dropouts — the tension space |
| Classroom_1_1 | Warm, single steady fixture — the calm room |
| Classroom_1_2 | Cooler, twin fixtures, **one failing tube** — the "wrong" room |
| Restroom | Cold, dim, sterile |
| Floor 2 corridor | Darker and greener than Floor 1, with heavier flicker dropouts |
| Classroom_2_1 | Clinical lab white, 3 steady fixtures — distinct from both Floor 1 classrooms |

`FlickerLight` drives the flicker (Perlin noise + randomised dropouts).

---

## Level layout

**Floor 1** — `FirstFloorCorridor`, `Classroom_1_1`, `Classroom_1_2`, `Restroom`
(plus `Library`, `Lab`, the shared `Main Structure` shell and `Stair`)

**Floor 2** — `SecondFloorCorridor`, `Classroom_2_1`

**Scenes** (in build order)

| Index | Scene | Purpose |
|---|---|---|
| 0 | `Menu` | Main menu |
| 1 | `MainLevel` | The school — both floors |
| 2 | `Bedroom` | Post-escape scene |
| 3 | `GameWin` | Win screen |
| 4 | `GameOver` | Death screen |

---

## Menus & settings

**Main menu** — PLAY · SETTINGS · CREDITS · LICENCES · QUIT

There is **no Continue button**: the game has no save system, and Play always starts a fresh run.

**Pause menu** — RESUME · SETTINGS · CREDITS · MAIN MENU · QUIT
Pausing freezes `Time.timeScale`, mutes audio, stops the entity's coroutines, and blocks player
input so the view cannot drift behind the panel. Resuming re-arms the entity's safe place.

**Style.** Both menus are the LAST BELL text-list style: a wide-spaced thin title top-left with a
subtitle, left-aligned uppercase items with **no boxes or panels**, a translucent slanted
(parallelogram) highlight bar that slides in behind the pressed item, tiled grain and a slow
vignette pulse over a near-black background, and a right-hand art image that fades into darkness
toward the left. All of it lives in **one** reusable component, `LastBellMenuStyle`, animates only
`RectTransform` / `CanvasGroup.alpha` / `Graphic` colour, uses **unscaled time** (so the pause menu
animates while `Time.timeScale = 0`) and generates every sprite procedurally — no new packages and
no new art assets beyond the placeholder below.

**Art slot.** `backdropArt` is a single serialized `Sprite` field on both `MainMenuUI` and
`PauseMenuUI` — drop your own image in to replace the placeholder corridor screenshot
(`Assets/Main/UI/Art/MenuBackdrop_Corridor.png`, captured from this game's own first-floor
corridor). The pause menu renders it at `backdropArtAlpha` (default 0.55) so the frozen world still
hints through.

**Font.** Menu text uses Unity's built-in font unless `displayFont` is set on the menu component.
There is a reserved **TYPE** entry on the Licences screen: any free SIL OFL display font added
later must be credited there.

**Settings** (shared by both menus, single source of truth in `GameSettings`)

| Setting | Range / values |
|---|---|
| Look sensitivity | ×0.4 – ×2.0 around the 0.15°/px baseline |
| Master / Ambient / SFX volume | 0–100 % |
| Graphics | Low / Medium / High |

**Graphics tiers** — this is the low-end safety net:

| Tier | Per-pixel lights | Shadows | Shadow distance |
|---|---|---|---|
| **Low** | **1** | **Hard only** | 15 |
| Medium | 4 | Soft, medium res | 30 |
| High | 4 | Soft, medium res | 30 |

All settings persist via `PlayerPrefs` (`opt_lookSensitivity`, `opt_volumeMaster`,
`opt_volumeAmbient`, `opt_volumeSfx`, `opt_graphicsTier`).

---

## Project structure

```
Assets/
  Main/
    Scenes/     Menu, MainLevel, Bedroom, GameWin, GameOver
    scripts/    gameplay + systems (see below)
    Audio/      ambient bed, footsteps, click, scream
    Material/   authored materials, incl. Floor2/ texture materials
    Prefab/     Key, Page, monster prefabs
    UI/         HUD sprites + generated control sprites
    UI/Art/     menu backdrop placeholder (game screenshot, ASTC 6x6 on Android)
  ThirdAssets/
    school/     school props + building shell (FBX, embedded materials)
    FloorTile/  floor material set
    Toon Furniture/, Horror Monster/, lineLight/
    NeKONTROLLER/  upstream touch-control art (sprites)
    CC0/        poly haven textures (bricks, wood floor, wood)
  Editor/       tooling menu items (see below)
```

**Notable scripts**

| Script | Role |
|---|---|
| `Controller.cs` | Player movement + camera. Reads `ControlStick` / `ControlPad`. |
| `Monster.cs` | Entity AI: teleport, sight, danger meter, death. |
| `DetectMonster.cs` | Flashlight trigger volume → `isOnFlashLightTrigger`. |
| `Flashlight.cs` | Light toggle, battery, click SFX. |
| `GameManager.cs` | Randomised key/page placement, pickup flow, key counter, exit. |
| `KeyTrigger.cs` / `PageTrigger.cs` / `ExitTrigger.cs` | World triggers. |
| `SpawnItem.cs` | Instantiates the key/page prefab at a spawn spot. |
| `PauseManager.cs` | Pause state, freeze, mute, return-to-menu, quit. |
| `PauseMenuUI.cs` / `MainMenuUI.cs` | Code-built menu UI in the LAST BELL text-list style. |
| `LastBellMenuStyle.cs` | Menu presentation: procedural sprites + unscaled coroutine animation (shared by both menus). |
| `GameSettings.cs` | Settings values, persistence, graphics tier mapping. |
| `AudioVolumeController.cs` | Master/Ambient/SFX application. |
| `AmbientAudioManager.cs` | Random ambient cue scheduler. |
| `FlickerLight.cs` / `StairsAmbience.cs` / `MobileControlsLayout.cs` | Lighting, stairs cue, adaptive control sizing. |

**Editor tooling** (menu items, not shipped in builds)

| Menu | Purpose |
|---|---|
| `Tools/NeKONTROLLER/*` | Rebuild touch UI, verify wiring, runtime smoke test, screenshot |
| `Tools/Floor1/*` | Audit, texture presets, lighting pass, content placement |
| `Tools/Floor2/*` | Audit, texture presets, lighting, stairs cue, floor revert |
| `Tools/Part7/*` | Pause manager install, pause runtime test, pause channel test, audio mixer, report |
| `Tools/Part8/*` | Package identity |
| `Tools/Part9/*` | Menu backdrop capture + assign, runtime menu animation test, Game View device sizes |
| `Tools/Audio/*` | Ambient bed compression |

---

## Building for Android

1. **The active platform is `Android`.** Unity **2022.3.62f3** with the Android module (SDK 34.0.0,
   NDK r23b) is installed. If the editor cannot find a JDK, point
   *Preferences ▸ External Tools ▸ JDK* at a JDK 11 install — the module's bundled `OpenJDK` folder
   is not present on this machine.
2. Package ID is `com.hamzapowerplayer.lastbell`.
3. **Release config:** scripting backend **IL2CPP**, architecture **ARM64** (required by Play),
   **Build App Bundle (Google Play)** ticked, **Release** build type, minSdk 22 / targetSdk auto (36).
4. **Signing:** custom keystore `C:/MOBILE-Keys/lastbell.keystore`, alias `lastbell`. The keystore
   file lives outside the repo and the password is **not** committed anywhere — supply it per build
   (locally in Player Settings ▸ Publishing, or via the CI secrets in [CLOUD_BUILD.md](CLOUD_BUILD.md)).
5. **Version:** currently `0.2` (bundleVersion) / `2` (AndroidBundleVersionCode). Increment the code
   for every upload.
6. **Texture compression**: albedo maps use **ASTC 6×6**, normal maps **ASTC 4×4**, data maps
   (roughness / AO / displacement) **ASTC 6×6 at ≤512 px**, with `wrapMode = Repeat` for tiling.
7. Build an **AAB** for Play Store upload. A release AAB (`Builds/LastBell-0.2-v2.aab`, 38 MB,
   ARM64, IL2CPP, `jar verified`) and its verification record
   (`Builds/aab_verification_0.2-v2.txt`) were produced this way.

**Budget note.** Total new audio is well under the 15–20 MB slice, and the imported CC0 textures
occupy roughly 3.8 MB compressed (58 MB uncompressed) against a 250 MB overall budget.

---

## Credits & licences

**Base project** — *High School Horror Android* by **yurifarion**
<https://github.com/yurifarion/High-School-Horror-Android-Unity3D>
Licensed **CC-BY-4.0** — attribution required. See `CC-BY-4.0` / `License.txt`.

**Touch controls** — **NeKONTROLLER** by **NeKoRoSYS**
<https://github.com/NeKoRoSYS/NeKONTROLLER>
Licensed **MIT**. Attribution not legally required but given as good practice.

**Mobile port, additional content and systems** — **HAMZA POWERPLAYER**

---

## Privacy

Last Bell is **fully offline**:

- No login, no accounts, no user profiles
- No network requests of any kind
- No ads, no ad SDKs
- No analytics or telemetry
- All settings and progress are stored locally on the device via `PlayerPrefs`

---

## Known limitations

- **No save/continue system.** Play always starts a fresh run. A Continue button is intentionally
  absent rather than present-and-broken.
- **URP is not used.** The project runs on the Built-in Render Pipeline. URP was never installed and
  could not be added, so all lighting is Built-in (flicker, realtime fixtures, no post-processing).
- **Nothing is baked.** All lighting is realtime; there are no lightmaps.
- **"High" graphics currently matches "Medium"** (4 per-pixel lights, soft shadows). Only Low
  differs.
- **Settings panel layout code is duplicated** between `MainMenuUI` and `PauseMenuUI`. Behaviour is
  single-sourced in `GameSettings`; only the widget construction repeats.
- **In the pause menu, Escape with a sub-panel open closes the panel *and* unpauses.** That is the
  pre-existing `PauseManager` behaviour (the pause state owns Escape) left deliberately untouched —
  use the on-screen **BACK** button to return to the pause list without resuming. On the main menu
  Escape closes the open panel and stays on the menu.
- **The base project's original menu canvas** still exists in the `Menu` scene but is disabled at
  runtime, so its `MenuAction` scripts keep running without drawing.
- Floor 2 room lights are range-capped at 10 m to avoid light bleed and per-pixel cost.
- Several Floor 2 surfaces remain untextured (flat colour) — bricks cover the walls, wood covers
  furniture and floor.

---

## Requirements

- Unity **2022.3.62f3 LTS**
- Android SDK / NDK (for device builds)
- The `com.unity.inputsystem` package (already in `Packages/manifest.json`)
