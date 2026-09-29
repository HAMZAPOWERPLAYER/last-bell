# Cloud Build Guide — Last Bell

Building an Android APK/AAB in the cloud instead of locally. Follow in order; Phase 0 is the only
part done on this machine.

**Why cloud build:** the first local Android build takes 10–30 minutes and needs the SDK/NDK/JDK
toolchain working. A cloud runner does that for you, gives reproducible builds, and keeps the
artifact history.

**Prerequisites already true for this project**

- Unity **2022.3.62f3** (must be pinned in the build service)
- Package ID `com.hamzapowerplayer.lastbell`
- All 5 scenes are already in Build Settings: `Menu`, `MainLevel`, `Bedroom`, `GameWin`, `GameOver`
- `.gitignore` is correct (`Library/`, `Temp/`, `Builds/` excluded → repo ≈ 110 MB)
- Built-in Render Pipeline (no URP shader stripping complications)

---

## Phase 0 — Local prep (on this machine, no network needed)

```powershell
cd C:\MOBILE
git init
git add .
git commit -m "Last Bell: mobile port baseline"
```

Also add a `.gitattributes` so line endings and binary assets are handled consistently on a
CI checkout (the project does not currently have one).

**Generate the release keystore** — do this locally and keep the file safe. If you lose it you can
never update the app on Play Store again.

```powershell
& "C:\Program Files\Unity 2022.3.62f3\Editor\Data\PlaybackEngines\AndroidPlayer\OpenJDK\bin\keytool.exe" `
  -genkeypair -v -keystore lastbell.keystore -alias lastbell `
  -keyalg RSA -keysize 2048 -validity 10000
```

Store `lastbell.keystore` **outside the repo** (or in the repo only if it is private and you accept
the risk). Never commit it to a public repo.

---

## Phase 1 — Push to a Git remote (needs your GitHub/GitLab account)

```powershell
git remote add origin https://github.com/<your-user>/last-bell.git
git branch -M main
git push -u origin main
```

Create the empty remote repository first (GitHub → New repository → **don't** add a README or
.gitignore, you already have them).

---

## Phase 2 — Pick a build service

### Option A — GitHub Actions + game-ci (free for public repos)

1. **Get a Unity licence file.** Unity Personal: activate manually and obtain the `.ulf` file.
   Unity Plus/Pro: you need the serial.
2. **Add repository secrets** (Settings → Secrets and variables → Actions):

   | Secret | Value |
   |---|---|
   | `UNITY_LICENSE` | contents of your `.ulf` file (Personal) |
   | `UNITY_EMAIL` | your Unity account email |
   | `UNITY_PASSWORD` | your Unity account password |
   | `UNITY_SERIAL` | serial number (Plus/Pro only) |
   | `ANDROID_KEYSTORE_BASE64` | base64 of `lastbell.keystore` |
   | `ANDROID_KEYSTORE_PASS` | keystore password |
   | `ANDROID_KEYALIAS` | `lastbell` |
   | `ANDROID_KEYALIAS_PASS` | key password |

3. **Add `.github/workflows/android.yml`** (a starting point — expect to iterate on secrets):

```yaml
name: Android Build
on:
  push:
    branches: [main]
  workflow_dispatch:

jobs:
  build:
    name: Build Android
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - uses: actions/cache@v4
        with:
          path: Library
          key: Library-Android-${{ hashFiles('ProjectSettings/ProjectVersion.txt') }}
          restore-keys: Library-Android-

      - uses: game-ci/unity-builder@v4
        env:
          UNITY_LICENSE: ${{ secrets.UNITY_LICENSE }}
          UNITY_EMAIL: ${{ secrets.UNITY_EMAIL }}
          UNITY_PASSWORD: ${{ secrets.UNITY_PASSWORD }}
        with:
          targetPlatform: Android
          unityVersion: 2022.3.62f3
          # androidPackage = .apk (sideload / test)
          # androidAppBundle = .aab (Play Store upload)
          androidExportType: androidPackage
          androidKeystoreName: lastbell.keystore
          androidKeystoreBase64: ${{ secrets.ANDROID_KEYSTORE_BASE64 }}
          androidKeystorePass: ${{ secrets.ANDROID_KEYSTORE_PASS }}
          androidKeyaliasName: ${{ secrets.ANDROID_KEYALIAS }}
          androidKeyaliasPass: ${{ secrets.ANDROID_KEYALIAS_PASS }}

      - uses: actions/upload-artifact@v4
        with:
          name: LastBell-Android
          path: build/Android
```

4. Push — the workflow runs automatically. Download the artifact from the run summary.

### Option B — Unity Build Automation (least friction with Unity)

1. Sign in at Unity Cloud / DevOps with your Unity account.
2. **Link the Git repository** (install the GitHub/GitLab app and grant access).
3. Create a build target:
   - Platform: **Android**
   - Unity version: **2022.3.62f3** (pin it — do not let it default)
   - Project subfolder: leave empty (the repo root *is* the Unity project)
4. **Credentials:** upload `lastbell.keystore`, alias `lastbell`, plus both passwords.
5. **Build type:** APK for testing, AAB for the Play Store.
6. Start the build and download the artifact when it finishes.

---

## Phase 3 — Verify the build

```powershell
# with the phone connected over USB, developer mode on
adb install -r LastBell.apk
```

Then check, in this order:

1. **It launches** and the main menu appears with all four buttons visible (Play / Settings /
   Credits / Quit) — this is the first real test of the ±180 landscape layout, which has only been
   reasoned about, never seen on a device.
2. **Buttons are readable** — single horizontal lines, no vertical letter stacking.
3. **Touch controls work** — the left stick moves you, right-half drag looks around. This is the
   first Android run since `Active Input Handling` was changed to `Both`.
4. **Pause** via the HUD button and the Android back button; Resume returns cleanly.
5. **Settings persist** — change the graphics tier, force-close the app, reopen, confirm it stuck.
6. **Play a full run**: pick up a key (the entity activates), collect 3 keys, reach the exit.
   Nobody has completed a run end to end yet — this is the outstanding verification.
7. **Frame rate** on the target device, especially on Floor 2 which has the most realtime lights.

---

## Gotchas

- **Pin Unity to 2022.3.62f3.** A different patch may silently upgrade the project and invalidate
  the verified scene wiring and input settings.
- **`ProjectSettings/` must be committed.** It carries the package ID, Active Input Handling, the
  Android texture (ASTC) overrides and the graphics tiers. It is not in `.gitignore` — keep it that
  way.
- **Don't commit `Library/`.** It is 313 MB and regenerates. Rebuilding it is why CI caches matter.
- **First cloud build is slow** (20–40 min: shader variants + IL2CPP). Later builds reuse the cache.
- **Keystore loss is permanent.** Back up `lastbell.keystore` and its passwords somewhere you will
  still have in five years.
- **AAB is for Play Store; APK is for testing.** Unity defaults to AAB — be explicit whichever way
  you go.
