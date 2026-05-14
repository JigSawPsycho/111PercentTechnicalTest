# 111Percent Technical Test — 2D Hack & Slash Prototype

## Project Overview
A small 2D hack-and-slash prototype built in Unity. The goal is a vertical-slice playable loop: a single player fights waves of enemies using a melee combo, dodge, and jump.

## Engine & Setup
- **Unity:** 6000.3.15f1 (Unity 6 LTS)
- **Render Pipeline:** Universal Render Pipeline (URP) 17.3.0
- **Input:** Unity Input System 1.19.0 (new input system)
- **2D Packages:** 2D Animation, Aseprite Importer, Sprite, Tilemap
- **Physics:** 2D physics (Rigidbody2D / Collider2D)

## Art Assets (already in project)
- `Assets/DouglasAvila/CobraRobot/Sprites/` — CobraRobot sprite sheets
- `Assets/DouglasAvila/CobraRobot/Animations/` — animation clips, split into two skins:
  - **HeroSkin** — used for the player
  - **EnemySkin** — used for enemies
- Clips available per skin: `Idle`, `Running`, `Jumping`, `Falling`, `Attack_Punch`, `Attack_Whip`, `Shooting`, `Damaged`, `Dead`

## Gameplay Scope

### Player (1)
Mechanics to implement:
- **Movement:** left/right at a single fixed speed (no run/walk split)
- **Jump:** single jump with grounded check
- **Melee combo:** 2-hit chain (e.g. `Attack_Punch` → `Attack_Whip`); second hit only chains within a short input window after the first
- **Dodge:** short burst of invulnerability + displacement, on cooldown
- **Charge Dash (secondary):** hold right-click for 0.5s — sprite flashes during the charge, then the player dashes forward 1.5× the dodge distance with i-frames, damaging every enemy crossed once

### Enemies (2 types)
- **Melee Enemy:** approaches the player and performs a close-range attack when in range
- **Ranged Enemy:** keeps distance and fires a projectile at the player

### Wave Spawn System
- Spawns enemies in discrete waves
- Each wave defines composition (count + mix of melee/ranged) and spawn points
- Next wave begins after the current wave is cleared (exact pacing TBD)

## Project Structure
```
Assets/
  Scripts/        # gameplay code (implemented — see breakdown below)
  Scenes/         # SampleScene.unity is the working scene
  Settings/       # URP render pipeline settings
  DouglasAvila/   # CobraRobot sprites + animation clips (HeroSkin, EnemySkin)
  Generated/      # AnimatorControllers, prefabs, WaveDefinitions produced by the editor builder
```

Script layout (all namespaces under `HackSlash.*`):
```
Assets/Scripts/
  Core/           # Faction, IDamageable, Health, Hitbox, GroundCheck, GameManager
  Player/         # PlayerController, PlayerCombat, PlayerHealth
  Enemies/        # EnemyBase, MeleeEnemy, RangedEnemy, Projectile
  Waves/          # WaveDefinition (SO), WaveSpawner
  UI/             # HUD
  Editor/         # SampleSceneBuilder (menu: 111Percent → Build Sample Scene)
```

## Conventions
- Use the **new Input System** (not legacy `Input.GetKey`)
- Use **Rigidbody2D** with `linearVelocity` for movement (Unity 6 API)
- Animation transitions via Animator parameters, not direct `Play()` calls
- Keep tunable values (speed, jump force, damage, cooldowns) as serialized fields so they can be tuned in the Inspector
- Reuse `EnemyBase` for shared enemy behavior (health, hit reaction, death)

## Current State

### Implemented

**Core (`HackSlash.Core`)**
- `Faction` enum (Player / Enemy)
- `IDamageable` + `DamageInfo` struct (amount, source faction, origin, knockback)
- `Health` — HP, invulnerability window, `Damaged` / `Died` events, faction-aware damage filtering, `InvulnerableOverride` for dodge i-frames
- `Hitbox` — manual `Physics2D.OverlapBoxNonAlloc` strike, owner-faction filter, per-swing dedupe so a single swing can't double-hit, gizmo
- `GroundCheck` — overlap-circle probe driven from `FixedUpdate`
- `GameManager` — singleton, player registration, scene restart on player death and on all-waves-cleared

**Player (`HackSlash.Player`)**
- `PlayerController` — new Input System via `PlayerInput` SendMessages (`OnMove`/`OnJump`/`OnAttack`/`OnDodge`/`OnSecondaryAttack`); fixed-speed horizontal movement, single jump w/ grounded check, dodge (burst velocity + i-frames + cooldown), charge-dash secondary (hold-to-charge, sprite color flash, then horizontal dash at 1.5× dodge distance with i-frames and per-enemy-once damage scan); writes `Speed`/`Grounded`/`VerticalVelocity` to the Animator; auto-registers with `GameManager`
- `PlayerCombat` — 2-hit combo (`Attack_Punch` → `Attack_Whip`), input buffering during the active swing, combo window after, timer-based strike (no animation events required)
- `PlayerHealth` — wraps `Health`, fires `Hurt` trigger / `Dead` bool on the Animator, dodge invulnerability hook, brief hitstun

**Enemies (`HackSlash.Enemies`)**
- `EnemyBase` — abstract: shared health, hitstun, knockback, facing, locomotion-anim wiring, corpse cleanup, `Defeated` event, virtual `CancelAttack()` hook
- `MeleeEnemy` — chase → windup → timer-driven strike → recovery → cooldown. `CancelAttack` drops the pending strike when interrupted, so getting hit mid-windup negates damage.
- `RangedEnemy` — kites to a preferred distance, retreats inside a closer band, windup → timer-driven projectile spawn → recovery. Same interrupt behavior.
- `Projectile` — kinematic 2D rigidbody, layer-masked trigger, faction-checked damage, despawn on solid or on hit

**Waves (`HackSlash.Waves`)**
- `WaveDefinition` ScriptableObject — list of `WaveEntry { kind, count, spawnInterval }`, `entryInterval`, `startDelay`
- `WaveSpawner` — coroutine-driven; each wave waits for `startDelay`, drips spawns inside each entry at the entry's `spawnInterval`, gaps between entries at `entryInterval`, then blocks on `AliveEnemies == 0` before the next wave; fires `WaveStarted` / `WaveCleared` / `AllWavesCleared`

**UI (`HackSlash.UI`)**
- `HUD` — health bar, wave label, status label (defeated/victory)

**Editor tooling**
- `SampleSceneBuilder` — menu **111Percent → Build Sample Scene**. One click generates:
  - `Assets/Generated/Animators/` — `HeroAnimator`, `MeleeEnemyAnimator`, `RangedEnemyAnimator` (states + parameters + transitions wired to the existing `.anim` clips)
  - `Assets/Generated/Prefabs/` — `Player`, `MeleeEnemy`, `RangedEnemy`, `Projectile`
  - `Assets/Generated/Waves/` — `Wave1`, `Wave2`, `Wave3` (ramping difficulty)
  - Rebuilds `SampleScene` with camera, ground + walls, 3 spawn points, player, `GameManager`, `WaveSpawner`, HUD canvas, and an `EventSystem` driven by `InputSystemUIInputModule`
- Ensures custom layers `Ground` (6) / `Player` (7) / `Enemy` (8) / `Projectile` (9) exist in the Tag Manager

**Input**
- Added a `Dodge` action to `InputSystem_Actions.inputactions` bound to **Left Shift** (keyboard) and **gamepad east button**
- Added a `SecondaryAttack` action bound to **Right Mouse Button** and **gamepad right shoulder** — handler receives both press and release via `PlayerInput` SendMessages, so the charge can be cancelled by releasing before the 0.5s threshold

### Controls
- **WASD / Arrows** — move
- **Space / Gamepad South** — jump
- **Left Click / Enter / Gamepad West** — attack (combos)
- **Left Shift / Gamepad East** — dodge
- **Right Click / Gamepad Right Shoulder** — hold to charge, release-fires charge-dash at 0.5s held

### How to run
1. Open the project in Unity 6000.3.15f1
2. Let scripts compile
3. Top menu → **111Percent → Build Sample Scene**
4. Open `Assets/Scenes/SampleScene` and press Play

### Notes
- Strikes are fired on timers (`strikeTimings` on `PlayerCombat`, `attackWindup` on enemies) rather than animation events, so the existing `.anim` clips don't need to be modified.
- The editor builder is idempotent: running it again wipes `SampleScene`'s contents and regenerates everything in `Assets/Generated/`.
