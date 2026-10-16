# THE LAST BELL 2.0 — Release Checklist

Status: **code-complete on `feature/last-bell-2.0`; signed build + on-device pass are user-side.**
Version: **2.0** (version code **20**) · Package: `com.hamzapowerplayer.lastbell` · Unity 2022.3.62f3 · Built-in RP · ARM64 · IL2CPP · minSdk 24 / target 36.

---

## 1. Build (user-side — needs the keystore password)

1. Unity → **File ▸ Build Settings** → Android → **Build App Bundle (Google Play)** ☑.
2. Keystore: *Player Settings ▸ Publishing Settings* → keystore `C:/MOBILE-Keys/lastbell.keystore`, alias `lastbell` — **password is not stored in the repo, enter it when Unity asks.**
3. Build. Output goes where the Build Settings window points (historically `Builds/`).
4. Record the AAB hash (`certutil -hashfile <file> SHA256` or `Get-FileHash`) — compare with the previous record in `Builds/`.

## 2. Verify the offline manifest (the INTERNET fix)

The 2.0 build carries a **custom main manifest** (`Assets/Plugins/Android/AndroidManifest.xml`) that removes the INTERNET permission Unity declares in its library manifest.
After building, verify the merged manifest — **one** of these, whichever tooling is at hand:

```powershell
# Preferred: bundletool
bundletool dump manifest --bundle <app.aab> | Select-String -i "internet"        # expect: no output

# Alternative: aapt2 on a base APK extracted from the AAB
aapt2 dump permissions <split>.apk                                               # expect: no INTERNET line
```

Also confirm `android:exported="true"` is still on `com.unity3d.player.UnityPlayerActivity` in the merged manifest (it comes from Unity's library manifest; this is a sanity check that the merge behaved).

If INTERNET still appears: the custom main manifest was not picked up — confirm the file is exactly at `Assets/Plugins/Android/AndroidManifest.xml` (not inside a sub-folder) and rebuild.

## 3. Signing / store prep

- [ ] AAB signed with `lastbell.keystore` (verify with `jarsigner -verify -verbose <app.aab>`).
- [ ] Version name `2.0`, version code `20` visible in the bundle.
- [ ] App icon present in the bundle (6 mipmap slots from `LastBellAppIcon.jpg` — 2.0 assigns them; the launcher visual check happens on-device).
- [ ] No analytics/ad/account SDK permissions in the merged manifest (only the expected storage/vibrate set Unity adds).
- [ ] Privacy: the game is **fully offline** — no network code paths ship in 2.0.

## 4. On-device acceptance (physical phone)

Run the §12 checklist in `README.md`, plus the 2.0-specific additions:

- [ ] Main menu with a **saved run** shows all **six** rows (CONTINUE/NEW RUN/SETTINGS/CREDITS/LICENCES/QUIT) — no row overlapping another, all inside the screen.
- [ ] Settings shows the new **FLASHING** row; toggling to **Reduced** visibly softens the level's tube flicker and stops the menu title dips; **RESET DEFAULTS** restores look/volume/graphics.
- [ ] Pause button (top-right) is not covered by a notch/punch-hole in landscape.
- [ ] Audio: atmosphere bed + stairs sting + door cue + gate unlock cue all audible and not clipping.
- [ ] FPS / memory / thermal / loading times recorded (see TEST_REPORT.md "unverified" section — this is the only remaining performance evidence).

## 5. Known gaps accepted for 2.0 (do not block release)

- Pickup SFX left **empty** (no suitable free asset found; documented in TEST_REPORT.md).
- Adaptive launcher icon not authored (square source; launcher masks it).
- Extra "lore note" props deferred (six-page story system already covers the narrative beat).
- Additional accessibility (text scaling) deferred to 2.1.
