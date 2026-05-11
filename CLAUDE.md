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
  Scripts/        # gameplay code (currently empty — to be created)
  Scenes/         # SampleScene.unity is the working scene
  Settings/       # URP render pipeline settings
  DouglasAvila/   # CobraRobot sprites + animation clips (HeroSkin, EnemySkin)
```

Suggested script organization once code is added:
```
Assets/Scripts/
  Player/         # PlayerController, PlayerCombat, PlayerHealth
  Enemies/        # EnemyBase, MeleeEnemy, RangedEnemy, Projectile
  Waves/          # WaveSpawner, WaveDefinition (ScriptableObject)
  Core/           # GameManager, HitboxReceiver, Health component
```

## Conventions
- Use the **new Input System** (not legacy `Input.GetKey`)
- Use **Rigidbody2D** with `linearVelocity` for movement (Unity 6 API)
- Animation transitions via Animator parameters, not direct `Play()` calls
- Keep tunable values (speed, jump force, damage, cooldowns) as serialized fields so they can be tuned in the Inspector
- Reuse `EnemyBase` for shared enemy behavior (health, hit reaction, death)

## Current State
- Project is scaffolded; URP, Input System, and 2D toolchain are configured
- CobraRobot sprites and animation clips are imported (HeroSkin + EnemySkin)
- `Assets/Scripts/` is empty — all gameplay code is still to be written
- `SampleScene` is the starting scene
- No prefabs yet — player/enemy prefabs will be built using the existing sprites and clips
