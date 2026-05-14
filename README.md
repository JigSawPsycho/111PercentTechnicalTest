# 111Percent Technical Test — 2D Hack & Slash Prototype

A vertical-slice 2D hack-and-slash built in Unity 6. One player, melee combo + dodge + charge dash + a passive ultimate, fighting through scripted enemy waves.

Jump to: [English](#english) · [한국어](#한국어)

---

## English

### Authorship note

**All code in this repository was written by AI (Anthropic's Claude).** I (the candidate) drove every part of this project that isn't typing — system design, architectural boundaries, scope decisions, prefab/scene composition, tuning, asset selection, design handoff, and code review on every change. The AI implemented to my spec; I owned the shape of what got built. Each commit reflects a deliberate design decision, not a free-form generation.

### Time spent

Due to personal circumstances I was only able to spend **about 5 hours** on this project. Within that window I'm happy with what was accomplished — the prototype feels and looks good to play, and the systems it does ship are coherent end-to-end rather than half-finished.

### How to run

1. Install Unity **6000.3.15f1** (Unity 6 LTS).
2. Open the project. Let scripts compile.
3. Open `Assets/Scenes/FinalScene.unity`.
4. Press **Play**.

### Controls

| Action | Keyboard / Mouse | Gamepad |
|---|---|---|
| Move | WASD / Arrows | Left stick |
| Jump | Space | South button |
| Attack (combo) | Left click / Enter | West button |
| Dodge | Left Shift | East button |
| Charge Dash | Hold Right Mouse 0.5s | Hold Right Shoulder 0.5s |

The Ultimate is passive — it auto-fires when its charge meter fills.

### Combat system

Every offensive and defensive action is an **`Ability`** component. Abilities are *character-agnostic*: each one reads `Faction` and `Facing` from its host's `IAbilityOwner` implementation, so the same script works on the player or any enemy with zero changes.

- **Melee combo (Punch → Whip).** Two `MeleeSwingAbility` subclasses chained by a small `MeleeComboCoordinator` router. Attack again inside a **0.45 s** combo window after the first hit lands to fire the second; outside the window, the chain resets. Each swing uses a manual `Physics2D.OverlapBoxNonAlloc` strike with per-swing dedupe so a single swing can't double-hit.
- **Dodge.** ~0.25 s horizontal burst with i-frames via `Health.SetInvulnerableFor`. Direction comes from movement input when available, otherwise from facing — so enemies can use the same component.
- **Charge Dash (secondary).** Hold RMB for 0.5 s; the sprite flashes during the charge, then the player dashes **1.5×** dodge distance with i-frames, damaging every enemy crossed exactly once (per-target dedupe).
- **Unstoppable (passive ultimate).** Every successful hit (melee or charge-dash) accrues charge. At full charge it auto-activates a **5 s** invulnerability window: the body sprite tints gold and a pulsing halo sprite renders behind the animator. Charge resets at wave clear.
- **Damage routing.** `IDamageable` + `DamageInfo { Amount, Source, Origin, Knockback }`. `Health` filters by faction so player attacks can't hurt the player and vice versa. `Health.SetInvulnerableFor(seconds)` is the shared i-frame API used by Dodge, Charge Dash, and Unstoppable.
- **Interruption.** `EnemyBase.CancelAttack()` iterates every `Ability` on the enemy and calls `Cancel()`, so a mid-windup hit interrupts whatever swing or shot was about to fire.

### Movement system

- **Rigidbody2D** driven with the Unity 6 `linearVelocity` API (no `velocity`).
- **One fixed move speed** — no walk/run split, per the brief.
- **Single jump** gated by `GroundCheck`, an overlap-circle probe sampled in `FixedUpdate`.
- **Facing** flips on horizontal input; the sprite mirrors via `spriteRoot.localScale.x`. Abilities read this facing for shoot direction, dodge fallback direction, etc.
- **New Input System** end-to-end (`PlayerInput` SendMessages). The `SecondaryAttack` action receives both press *and* release so a charge held under 0.5 s can be cancelled.
- **Acting states** — dodge, charge dash, and combo windups lock new movement input but preserve in-air momentum where appropriate; on the ground we decelerate toward zero.

### Wave system

- **`WaveDefinition`** (`ScriptableObject`) — one asset per wave. Each contains a list of `WaveEntry { kind, count, spawnInterval }`, an `entryInterval` between entries, and a `startDelay` before the wave begins.
- **`WaveSpawner`** runs a coroutine that, for each wave, waits the `startDelay`, then drips spawns inside each entry at its `spawnInterval`, gaps each entry by `entryInterval`, and finally blocks on `AliveEnemies == 0` before advancing.
- **Events**: `WaveStarted` / `WaveCleared` / `AllWavesCleared` — the HUD and `PlayerController` subscribe.
- **Per-wave tracking** — total kills, melee kills, and ranged kills are exposed for the HUD enemy tracker.
- **Wave-cleared rewards** — `PlayerController` restores full HP, resets the Charge Dash cooldown, and resets the Unstoppable charge meter. That keeps each wave feeling like a fresh round.
- **End conditions** — `GameManager` reloads the scene on player death (with a short delay) and on all-waves-cleared.

### HUD

- **UI Toolkit** (UXML + USS) — the "Neon Brawler" cyberpunk design, scaled to 1920×1080 reference.
- HP / Charge Dash / Ultimate bars with skewed-quad fills, trail lag, and critical flashing.
- Wave pip indicators (boss-pip on the final wave) and a per-type enemy tracker.
- Banner overlays for `WAVE INCOMING`, `FIGHT`, `WAVE CLEAR`, `VICTORY`, `K.O.`
- Combo popup, screen-shake on damage, red screen-edge hit flash, gold edge pulse during Unstoppable.

### Project layout

```
Assets/
  Scripts/
    Core/        Faction, IDamageable, IAbilityOwner, IMoveInputProvider,
                 Health, Hitbox, GroundCheck, GameManager
    Abilities/   Ability, TimedStrikeAbility, MeleeSwingAbility,
                 PunchAbility, WhipAbility, ShootAbility, DodgeAbility,
                 ChargeDashAbility, UnstoppableAbility, MeleeComboCoordinator
    Player/      PlayerController, PlayerHealth
    Enemies/     EnemyBase, MeleeEnemy, RangedEnemy, Projectile
    Waves/       WaveDefinition (SO), WaveSpawner
    UI/          HUD (UI Toolkit), ScreenBorderOverlay, Elements/*
  Scenes/        FinalScene.unity
  Prefabs/       Player, MeleeEnemy, RangedEnemy
  DouglasAvila/  CobraRobot sprites + animation clips (HeroSkin, EnemySkin)
  UI/Resources/  NeonHUD.uxml, InputStrip.uxml, HUD.uss, Fonts/
Design/          Design handoff bundle (HTML/CSS prototype + chat transcripts)
```

All gameplay namespaces live under `HackSlash.*`.

### Tech

- Unity **6000.3.15f1** (Unity 6 LTS)
- Universal Render Pipeline **17.3.0**
- Input System **1.19.0**
- 2D physics (`Rigidbody2D` / `Collider2D`)
- UI Toolkit (runtime UXML/USS) for the HUD

---

## 한국어

### 작성자 노트

**이 리포지토리의 모든 코드는 AI (Anthropic Claude) 가 작성했습니다.** 본인(지원자)은 시스템 설계, 아키텍처 경계, 스코프 결정, 프리팹/씬 구성, 튜닝, 에셋 선택, 디자인 핸드오프, 모든 변경 사항에 대한 코드 리뷰 등 타이핑 외의 모든 부분을 직접 주도했습니다. AI는 제 사양에 맞춰 구현했고, 무엇을 만들지에 대한 형태는 제가 결정했습니다. 각 커밋은 자유 생성이 아니라 의도된 설계 결정의 결과입니다.

### 작업 시간

개인적인 사정으로 인해 본 프로젝트에는 **약 5시간** 정도만 투여할 수 있었습니다. 그 짧은 시간 내에서 이뤄낸 결과물에 만족하고 있습니다 — 프로토타입은 플레이 감각과 비주얼이 모두 좋고, 구현된 시스템들은 미완성된 채로 남겨진 부분 없이 처음부터 끝까지 일관되게 동작합니다.

### 실행 방법

1. Unity **6000.3.15f1** (Unity 6 LTS) 를 설치합니다.
2. 프로젝트를 열고 스크립트가 컴파일될 때까지 기다립니다.
3. `Assets/Scenes/FinalScene.unity` 를 엽니다.
4. **Play** 를 누릅니다.

### 조작

| 동작 | 키보드 / 마우스 | 게임패드 |
|---|---|---|
| 이동 | WASD / 방향키 | 왼쪽 스틱 |
| 점프 | 스페이스 | South 버튼 |
| 공격 (콤보) | 좌클릭 / Enter | West 버튼 |
| 회피 | 좌측 Shift | East 버튼 |
| 차지 대시 | 우클릭 0.5초 홀드 | 우측 숄더 0.5초 홀드 |

궁극기는 패시브입니다 — 충전 게이지가 가득 차면 자동 발동됩니다.

### 전투 시스템

모든 공격 및 방어 동작은 **`Ability`** 컴포넌트로 구현되어 있습니다. 어빌리티는 *캐릭터에 독립적*입니다: 각 스크립트는 호스트의 `IAbilityOwner` 구현으로부터 `Faction` 과 `Facing` 을 읽어오므로, 플레이어든 적이든 동일한 스크립트가 수정 없이 동작합니다.

- **근접 콤보 (펀치 → 채찍).** 두 개의 `MeleeSwingAbility` 서브클래스를 `MeleeComboCoordinator` 라우터가 연결합니다. 첫 타격이 적중한 뒤 **0.45초** 의 콤보 윈도우 내에 다시 공격하면 2타로 이어지며, 윈도우를 벗어나면 콤보가 리셋됩니다. 각 스윙은 `Physics2D.OverlapBoxNonAlloc` 로 직접 판정하며, 한 번의 스윙이 동일 적을 두 번 때리지 않도록 중복 방지 처리가 되어 있습니다.
- **회피.** 약 0.25초간의 수평 버스트와 `Health.SetInvulnerableFor` 를 통한 i-프레임. 이동 입력이 있을 때는 입력 방향, 없을 때는 facing 방향을 사용합니다 — 그래서 동일 컴포넌트를 적도 사용할 수 있습니다.
- **차지 대시 (보조 스킬).** 우클릭을 0.5초 홀드하면 스프라이트가 깜빡이며 충전되고, 릴리스 시 회피 거리의 **1.5배** 를 i-프레임과 함께 돌진하며 경로 위의 모든 적을 한 번씩 타격합니다.
- **언스토퍼블 (패시브 궁극기).** 적에게 입힌 모든 타격(근접 또는 차지 대시)이 게이지를 채웁니다. 게이지가 가득 차면 **5초간** 자동으로 무적 상태가 발동되어 몸체 스프라이트가 골드로 틴팅되고 후광 스프라이트가 펄스합니다. 게이지는 웨이브 클리어 시 초기화됩니다.
- **데미지 라우팅.** `IDamageable` + `DamageInfo { Amount, Source, Origin, Knockback }`. `Health` 는 진영(Faction)으로 필터링하여 플레이어 공격이 플레이어에게, 적의 공격이 적에게 닿지 않도록 합니다. `Health.SetInvulnerableFor(seconds)` 는 회피, 차지 대시, 언스토퍼블이 공통으로 사용하는 i-프레임 API 입니다.
- **공격 중단.** `EnemyBase.CancelAttack()` 는 해당 적의 모든 `Ability` 를 순회하며 `Cancel()` 을 호출하므로, 윈드업 중에 적을 타격하면 그 적이 발사하려던 스윙이나 샷이 중단됩니다.

### 이동 시스템

- **Rigidbody2D** 기반, Unity 6 의 `linearVelocity` API 사용 (`velocity` 대신).
- 과제 명세에 따라 **단일 고정 이동 속도** — 걷기/뛰기 분리 없음.
- **단일 점프**, `GroundCheck` (원형 오버랩 프로브, `FixedUpdate` 에서 샘플링) 로 게이팅.
- 수평 입력에 따라 **Facing 전환**, `spriteRoot.localScale.x` 로 스프라이트 미러링. 어빌리티는 이 facing 값을 발사 방향이나 회피 폴백 방향 등에 사용합니다.
- 입력 처리는 **신 Input System** (`PlayerInput` SendMessages) 으로 일원화. `SecondaryAttack` 액션은 press 와 release 를 모두 받으므로 0.5초 이내 릴리스 시 차지를 취소할 수 있습니다.
- **행동 중 상태** — 회피, 차지 대시, 콤보 윈드업은 신규 이동 입력을 잠그되 적절한 경우 공중 모멘텀을 유지하고, 지면에서는 0으로 감속합니다.

### 웨이브 시스템

- **`WaveDefinition`** (`ScriptableObject`) — 웨이브 1개 = 에셋 1개. 각 정의는 `WaveEntry { kind, count, spawnInterval }` 리스트, 엔트리 간 간격 `entryInterval`, 시작 지연 `startDelay` 를 포함합니다.
- **`WaveSpawner`** 는 코루틴으로 각 웨이브마다 `startDelay` 만큼 대기한 뒤, 각 엔트리 내에서 `spawnInterval` 로 적을 점진 스폰하고, 엔트리 사이는 `entryInterval` 로 간격을 둔 뒤, `AliveEnemies == 0` 이 될 때까지 차단한 후 다음 웨이브로 진행합니다.
- **이벤트**: `WaveStarted` / `WaveCleared` / `AllWavesCleared` — HUD 와 `PlayerController` 가 구독합니다.
- **웨이브별 추적** — 총 처치 수, 근접 처치 수, 원거리 처치 수가 HUD 적 트래커에 노출됩니다.
- **웨이브 클리어 보상** — `PlayerController` 가 HP 를 완전 회복하고, 차지 대시의 쿨다운과 언스토퍼블 게이지를 리셋합니다. 매 웨이브가 새로운 라운드처럼 느껴지도록 설계되었습니다.
- **종료 조건** — `GameManager` 는 플레이어 사망 시(짧은 지연 후) 또는 모든 웨이브 클리어 시 씬을 리로드합니다.

### HUD

- **UI Toolkit** (UXML + USS) — 사이버펑크 풍의 "Neon Brawler" 디자인, 1920×1080 기준 해상도로 스케일링.
- HP / 차지 대시 / 궁극기 게이지 (스큐드 쿼드 필, 트레일 랙, 위급 시 점멸 효과 포함).
- 웨이브 핍 인디케이터 (마지막 웨이브는 보스 핍) 와 종류별 적 트래커.
- `WAVE INCOMING`, `FIGHT`, `WAVE CLEAR`, `VICTORY`, `K.O.` 배너 오버레이.
- 콤보 팝업, 피격 시 스크린 셰이크, 화면 가장자리 붉은 히트 플래시, 언스토퍼블 발동 중 골드 엣지 펄스.

### 프로젝트 구조

```
Assets/
  Scripts/
    Core/        Faction, IDamageable, IAbilityOwner, IMoveInputProvider,
                 Health, Hitbox, GroundCheck, GameManager
    Abilities/   Ability, TimedStrikeAbility, MeleeSwingAbility,
                 PunchAbility, WhipAbility, ShootAbility, DodgeAbility,
                 ChargeDashAbility, UnstoppableAbility, MeleeComboCoordinator
    Player/      PlayerController, PlayerHealth
    Enemies/     EnemyBase, MeleeEnemy, RangedEnemy, Projectile
    Waves/       WaveDefinition (SO), WaveSpawner
    UI/          HUD (UI Toolkit), ScreenBorderOverlay, Elements/*
  Scenes/        FinalScene.unity
  Prefabs/       Player, MeleeEnemy, RangedEnemy
  DouglasAvila/  CobraRobot 스프라이트 및 애니메이션 클립 (HeroSkin, EnemySkin)
  UI/Resources/  NeonHUD.uxml, InputStrip.uxml, HUD.uss, Fonts/
Design/          디자인 핸드오프 번들 (HTML/CSS 프로토타입 및 채팅 트랜스크립트)
```

모든 게임플레이 네임스페이스는 `HackSlash.*` 아래에 있습니다.

### 기술 스택

- Unity **6000.3.15f1** (Unity 6 LTS)
- Universal Render Pipeline **17.3.0**
- Input System **1.19.0**
- 2D 물리 (`Rigidbody2D` / `Collider2D`)
- UI Toolkit (런타임 UXML/USS) — HUD 구현
