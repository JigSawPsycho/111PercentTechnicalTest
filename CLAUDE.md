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
  Core/           # Faction, IDamageable, IAbilityOwner, IMoveInputProvider, Health, Hitbox, GroundCheck, GameManager
  Abilities/      # Ability, TimedStrikeAbility, MeleeSwingAbility, PunchAbility, WhipAbility,
                  # ShootAbility, DodgeAbility, ChargeDashAbility, MeleeComboCoordinator
  Player/         # PlayerController, PlayerHealth
  Enemies/        # EnemyBase, MeleeEnemy, RangedEnemy, Projectile
  Waves/          # WaveDefinition (SO), WaveSpawner
  UI/             # HUD
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
- `IAbilityOwner` — character-agnostic contract abilities depend on (Faction, Facing). Implemented by `PlayerController` and `EnemyBase`.
- `IMoveInputProvider` — optional `MoveInputX` provider (player only) so Dodge picks input direction; enemies omit it and Dodge falls back to Facing.
- `Health` — HP, invulnerability window, `Damaged` / `Died` events, faction-aware damage filtering, `InvulnerableOverride`, `SetInvulnerableFor(seconds)` shared i-frame API used by Dodge/ChargeDash on any character.
- `Hitbox` — manual `Physics2D.OverlapBoxNonAlloc` strike, owner-faction filter, per-swing dedupe so a single swing can't double-hit, gizmo
- `GroundCheck` — overlap-circle probe driven from `FixedUpdate`
- `GameManager` — singleton, player registration, scene restart on player death and on all-waves-cleared

**Abilities (`HackSlash.Abilities`)** — character-agnostic: every class below works on any character that implements `IAbilityOwner`. Drop one as a component on the GameObject and it self-wires via `GetComponentInParent`.
- `Ability` — abstract MonoBehaviour base: cooldown, `IsReady` / `IsActive` / `IsLocked`, `TryActivate()` template method, virtual `Cancel()`.
- `TimedStrikeAbility` — abstract refinement of the windup→strike→recovery pattern. Subclasses implement `OnStrike()`. `Cancel()` aborts a pending strike (mid-windup interrupt).
- `MeleeSwingAbility` — generic single melee swing; aligns its `Hitbox.Owner` with the host's `Faction` on Awake.
- `PunchAbility` / `WhipAbility` — distinct subclasses of `MeleeSwingAbility` so the combo coordinator can address each by type. `WhipAbility` is gated by an optional `MeleeComboCoordinator`.
- `ShootAbility` — spawns a `Projectile` whose direction comes from `owner.Facing` and whose faction tag comes from `owner.Faction`.
- `DodgeAbility` — burst velocity + i-frames via `Health.SetInvulnerableFor`. Uses input direction from `IMoveInputProvider` when available, else `Facing`.
- `ChargeDashAbility` — hold-to-charge with sprite flash, then dash with i-frames and a per-enemy-once OverlapBox scan; reads faction from owner.
- `MeleeComboCoordinator` — small router (not an Ability) that composes any two `MeleeSwingAbility` instances into a 2-hit chain with input buffering and a combo window.

**Player (`HackSlash.Player`)**
- `PlayerController` — implements `IAbilityOwner` + `IMoveInputProvider`. Handles input dispatch (`OnMove`/`OnJump`/`OnAttack`/`OnDodge`/`OnSecondaryAttack`), movement, jump, facing, locomotion Animator. Delegates attacks to a `MeleeComboCoordinator` and skills to `DodgeAbility` / `ChargeDashAbility` references. Auto-registers with `GameManager`.
- `PlayerHealth` — wraps `Health`, fires `Hurt` / `Dead` Animator triggers, brief hitstun, forwards `SetInvulnerable` to `Health.SetInvulnerableFor`.

**Enemies (`HackSlash.Enemies`)**
- `EnemyBase` — abstract: implements `IAbilityOwner` (faction=Enemy, exposes `Facing`). Shared health, hitstun, knockback, locomotion-anim wiring, corpse cleanup, `Defeated` event. Default `CancelAttack()` iterates `GetComponentsInChildren<Ability>()` and calls `Cancel()` on each so mid-windup hits interrupt any attached ability.
- `MeleeEnemy` — chase AI only; delegates attack execution to a serialized `MeleeSwingAbility`.
- `RangedEnemy` — kiting AI only; delegates fire execution to a serialized `ShootAbility`.
- `Projectile` — kinematic 2D rigidbody, layer-masked trigger, faction-checked damage, despawn on solid or on hit

**Waves (`HackSlash.Waves`)**
- `WaveDefinition` ScriptableObject — list of `WaveEntry { kind, count, spawnInterval }`, `entryInterval`, `startDelay`
- `WaveSpawner` — coroutine-driven; each wave waits for `startDelay`, drips spawns inside each entry at the entry's `spawnInterval`, gaps between entries at `entryInterval`, then blocks on `AliveEnemies == 0` before the next wave; fires `WaveStarted` / `WaveCleared` / `AllWavesCleared`

**UI (`HackSlash.UI`)**
- `HUD` — health bar, wave label, status label (defeated/victory)

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
3. Open `Assets/Scenes/SampleScene` and press Play

### Notes
- Strikes are fired on timers (`windup` + `recovery` on `TimedStrikeAbility` subclasses) rather than animation events, so the existing `.anim` clips don't need to be modified.
- Abilities are character-agnostic: dropping a `ShootAbility` onto the Player or a `DodgeAbility` onto an enemy works without code changes because each ability reads `Faction` and `Facing` from its host's `IAbilityOwner` implementation.
