# THE LAST BELL 2.0 — Release Checklist

Status: **2.0 signed build verified (2026-10-16); on-device pass is user-side.**
Version: **2.0** (version code **20**) · Package: `com.hamzapowerplayer.lastbell` · Unity 2022.3.62f3 · Built-in RP · ARM64 · IL2CPP · minSdk 24 / target 36.

---

## 1. Build (done 2026-10-16 — reproduce from the editor if needed)

The signed 2.0 AAB was built **in-editor** on 2026-10-16 (Succeeded, `jarsigner`: *jar
verified*). To reproduce:

1. Unity → **File ▸ Build Settings** → Android → **Build App Bundle (Google Play)** ☑.
2. Keystore: *Player Settings ▸ Publishing Settings* → keystore `C:/MOBILE-Keys/lastbell.keystore`,
   alias `lastbell` — the passwords live in the editor project settings / `Library/`
   (gitignored), never in the repo.
3. Build. Output goes where the Build Settings window points (historically `Builds/`).
4. Record the AAB hash (`certutil -hashfile <file> SHA256` or `Get-FileHash`) — compare with the
   build record in this file (§6) and `Builds/aab_verification_2.0-code20.txt`.

## 2. Verify the offline manifest (the INTERNET fix)

The 2.0 build carries a **custom main manifest** (`Assets/Plugins/Android/AndroidManifest.xml`) that:
1. removes the INTERNET permission Unity declares in its library manifest (`tools:node="remove"`), and
2. declares the **UnityPlayerActivity** explicitly — a custom main manifest *replaces* Unity's
   launcher template, so without this the bundle ships with **no launcher entry** (Unity logs
   `LauncherManifestDiag.txt: Unable to find Unity activity in manifest` during the build).
   The first 2.0 build hit this; the activity is now mirrored element-for-element from the
   pre-2.0 builds (decoded from their binary manifest with aapt2).

After building, verify the **binary manifest inside the AAB** (this is the authoritative check):

```powershell
# 1. extract base/manifest/AndroidManifest.xml from the AAB and put it at the root of a shell zip
$aab  = 'Builds\LastBell-2.0-code20.aab'
$tmp  = "$env:TEMP\lb_manifest.bin"
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip  = [System.IO.Compression.ZipFile]::OpenRead((Resolve-Path $aab).Path)
$e    = $zip.Entries | Where-Object { $_.FullName -eq 'base/manifest/AndroidManifest.xml' }
$ms   = New-Object System.IO.MemoryStream; $s = $e.Open(); $s.CopyTo($ms); $s.Close(); $zip.Dispose()
[System.IO.File]::WriteAllBytes($tmp, $ms.ToArray())
$apk  = "$env:TEMP\lb_shell.apk"; if (Test-Path $apk) { Remove-Item $apk }
$z    = [System.IO.Compression.ZipFile]::Open($apk, 'Create')
$en   = $z.CreateEntry('AndroidManifest.xml')
$es   = $en.Open(); $b = [System.IO.File]::ReadAllBytes($tmp); $es.Write($b, 0, $b.Length); $es.Close(); $z.Dispose()

# 2. decode and inspect
& 'C:\Android\Sdk\build-tools\36.0.0\aapt2.exe' dump xmltree $apk --file AndroidManifest.xml |
    Select-String -Pattern 'UnityPlayerActivity|LAUNCHER|INTERNET|versionName|versionCode'
```

Expected: **UnityPlayerActivity + android.intent.category.LAUNCHER present, no INTERNET line,
`versionCode` = 20, `versionName` = 2.0**. If `bundletool` is available, its manifest dump works
too. If INTERNET still appears: the custom main manifest was not picked up — confirm it sits
exactly at `Assets/Plugins/Android/AndroidManifest.xml` and rebuild.

## 3. Signing / store prep

- [x] AAB signed with `lastbell.keystore` — `jarsigner -verify -certs` → *jar verified* (2026-10-16).
- [x] Version name `2.0`, version code `20` present in the bundle manifest (decoded, 2026-10-16).
- [x] App icon present in the bundle (6 mipmap densities `base/res/mipmap-*/app_icon.png`; the launcher visual check happens on-device).
- [x] No analytics/ad/account SDK permissions — the final manifest has **zero** `uses-permission` nodes.
- [x] Privacy: the game is **fully offline** — INTERNET absent from the shipped bundle (2026-10-16).

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

## 6. Build record (2026-10-16)

| | |
|---|---|
| Artifact | `Builds/LastBell-2.0-code20.aab` — 39.0 MB |
| Mirrors | `Builds/archive/` (AAB + symbols + full verification record) |
| Build result | `Succeeded` · errors 0 · warnings 5 |
| Signature | `jarsigner` → *jar verified* (self-signed upload key; no timestamp — normal) |
| SHA-256 (AAB) | `2F205919621EAA3B4F37AC3AA3FE34329307A11B8F3973020AA9DA9D93C010C7` |
| SHA-256 (symbols) | `4F838555B30242DD24859AD0F771BCDB630EE7D549DF2AD967A62B348DF28F1D` |
| Manifest facts | code 20 · name 2.0 · minSdk 24 / target 36 · arm64-v8a · **0 `uses-permission` nodes (INTERNET absent)** · `UnityPlayerActivity` + MAIN/LAUNCHER + `exported=true` |
| Full record | `Builds/aab_verification_2.0-code20.txt` |
