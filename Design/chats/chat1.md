# Beat-em-Up UI Mock

_Started 2026-05-14 01:24 UTC_

---

## User

Create a mock for a side scroller beatemup game UI. Include a health bar, charging ability bar, wave count tracker and enemy count tracker

## Assistant

_[tool: update_todos]_

I'll build an interactive beat-em-up HUD mock. I'll go with an original, bold cinematic style — crimson + electric cyan on a dark gritty backdrop, angular HUD elements like fighting games use, with live ticking state (health draining, charge filling, enemies falling, wave progression). Tweaks will let you try a few HUD style variations.

_[tool: copy_starter_component]_

_[tool: write_file]_

_[tool: write_file]_

_[tool: write_file]_

_[tool: done]_

_[tool: fork_verifier_agent]_

Built **Neon Brawler HUD** — an original side-scroller beat-em-up UI mock with live state:

- **Health bar** (top-left): chunked HP fill with a white "damage trail" that lags behind, level/portrait, low-HP critical flash, and active buffs
- **Charge bar**: cyan special-meter that fills, then locks into a pulsing READY state with three ability slots (Q/E/R)
- **Wave tracker** (top-right): big numeric WAVE 03/08 with angled pip progress and a diamond boss-wave marker
- **Enemy tracker**: hostiles-remaining count, progress bar, per-enemy icon row with crossed-out fallen enemies, and type breakdown (grunt/blade/boss)
- Plus a combo + score readout, input prompt strip, and a "MINI-BOSS INBOUND" event banner

Background is a placeholder back-alley scene with silhouetted fighters (player in accent color, three enemies with their own floating health bars) — swap for real art when you have it.

**Tweaks** (toggle from toolbar): three HUD frame styles (Slash / Bracket / Arcade), four accent colors, film grain, scanlines, and a "force low-HP" toggle to preview the critical state.

