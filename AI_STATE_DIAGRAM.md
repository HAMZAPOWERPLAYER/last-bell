# THE LAST BELL 2.0 — AI State Diagram (Monster)

This documents the behaviour system of `Monster.cs` as shipped in 2.0, the fairness rules
around its teleporting, and the secret-ending flag flow. It is the reference for anyone
touching enemy logic later.

## 1. Behaviour states

`BehaviorState { Dormant, Roaming, Stalking, Attacking, Retreating }` — a **derived** flag
(`state`) recomputed each `Update` from existing timers/conditions. It changes no behaviour by
itself; it exists so the logic and the runtime tests can be read and asserted against.

```
                        keysfound == 0 (or refs missing)
                 ┌───────────────────────────────┐
                 ▼                               │
            ┌─────────┐   first key picked up    │ key counter never
            │ Dormant │ ────────────────────────►│ resets mid-run
            └─────────┘                          │
                                                 ▼
        ┌──────────────────────────────────────────────────────────┐
        │                       (active ladder)                    │
        │  onDanger            →  Attacking   (danger meter runs)  │
        │  Time < retreatUntil →  Retreating  (brief, post-withdraw)│
        │  near && !inView     →  Stalking    (close but unseen)   │
        │  otherwise           →  Roaming                          │
        └──────────────────────────────────────────────────────────┘
```

Priority is top-down: `Attacking > Retreating > Stalking > Roaming`. All conditions come from
the pre-existing timing variables — the enum adds no new timers.

## 2. Awareness — intentionally asymmetric

- **Sight** gates the attack: the monster escalates only when the player is in view (existing
  cone/raycast logic, unchanged).
- **Hearing/teleport pressure** is more generous: it keeps reappearing nearby even when unseen.
  This is the 2.0 design: *unseen but closing* (Stalking) is the intended tension, not a bug.

## 3. Teleport fairness rules (2.0)

Every teleport now validates its destination before moving: **floor raycast** (must land on
geometry from above) + **clearance sphere** (`CheckSphere` — must not intersect walls/props).
`TeleportOutOfView()` re-rolls candidates and skips invalid ones (bounded analytic re-roll, no
do-while — the original infinite-loop bug is fixed and documented in the source).

- It never teleports **closer than `teleportMinDistance`** to the player.
- It never teleports **into view** of the player.
- Validation runs on **every** candidate, including the safe place below.

## 4. Safe-place retreat (fairness guarantee)

The "safe place" is where the monster withdraws after a scare so the player gets breathing room.

```
SafePlaceTimer fires → isSafePlaceavailable = true
        │
        ▼
goToSafePlace():
    rejected if:
      · safePlace == null
      · distance(safePlace, player)   < teleportMinDistance      (too close)
      · !IsHiddenFromPlayer(safePlace.position)                  (visible)
      · !IsValidDestination(safePlace.position)                  (mid-air / inside geometry)
        → falls back to TeleportOutOfView() + re-arms timer
    accepted → move there, re-arm timer, isSafePlaceavailable = false
```

The mid-air/inside-geometry rejection was added in 2.0 after runtime testing caught a rigged
scene destination that put the monster inside a wall.

## 5. Secret-ending flag flow (Phase 3)

```
6 story pages picked up
        │  (RunSave.PageMask, CountBits ≥ 6)
        ▼
ExitTrigger.GameWin()
        │  PlayerPrefs.SetInt("Save.Truth", PagesCollected >= 6 ? 1 : 0)
        ▼
GameManager.EndRun()  → clears run save (PlayerPrefs.Save persists the flag)
        ▼
Bedroom / GameWin scene → EndingCutscene.Start()
        │  consumes the flag (reads once, then deletes it → -1)
        ▼
PlayGameWinEnding(): standard ending
        + if secretTruth: extra beat — "THE LOG IS COMPLETE / Six pages. One name. Yours."
```

Notes: the flag is written **before** `EndRun()` so the save-flag write persists; the cutscene
**consumes** it so a later replay without the pages cannot re-trigger the secret; no
save-schema change (plain PlayerPrefs key, absent by default → `-1`).
