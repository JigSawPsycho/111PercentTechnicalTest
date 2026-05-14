/* global React, ReactDOM, TweaksPanel, TweakSection, TweakSlider, TweakToggle, TweakRadio, TweakColor, useTweaks */
const { useState, useEffect, useRef, useMemo } = React;

const TWEAK_DEFAULTS = /*EDITMODE-BEGIN*/{
  "hudStyle": "slash",
  "accent": "#ff2d4a",
  "showGrain": true,
  "showScanlines": true,
  "playerName": "RAVEN KOJIMA",
  "lowHealth": false
}/*EDITMODE-END*/;

const ACCENTS = ["#ff2d4a", "#ffb000", "#a26bff", "#21d07a"];

/* ---------- helpers ---------- */
const clamp = (v, lo, hi) => Math.max(lo, Math.min(hi, v));
const pad = (n, w = 2) => String(n).padStart(w, "0");

/* ---------- placeholder scene ---------- */
function Scene({ accent }) {
  return (
    <div className="scene">
      <div className="scene-sky"></div>
      <div className="scene-buildings">
        {[...Array(12)].map((_, i) => (
          <div
            key={i}
            className="bldg"
            style={{
              left: `${i * 9 - 4}%`,
              height: `${30 + ((i * 37) % 35)}%`,
              width: `${7 + (i % 3)}%`,
              opacity: 0.55 + ((i * 17) % 40) / 100,
            }}
          ></div>
        ))}
      </div>
      <div className="scene-haze" style={{ background: `radial-gradient(ellipse at 60% 70%, ${accent}22, transparent 60%)` }}></div>
      <div className="scene-ground"></div>
      <div className="scene-floor-grid"></div>

      {/* Fighter silhouettes — placeholders */}
      <div className="fighter player" aria-label="player fighter placeholder">
        <div className="fighter-shadow"></div>
        <div className="fighter-body" style={{ background: accent }}></div>
        <div className="fighter-tag">P1 ◂ PLAYER</div>
      </div>
      <div className="fighter enemy a" aria-label="enemy fighter placeholder">
        <div className="fighter-shadow"></div>
        <div className="fighter-body enemy-body"></div>
        <div className="enemy-bar"><span style={{ width: "62%" }}></span></div>
      </div>
      <div className="fighter enemy b">
        <div className="fighter-shadow"></div>
        <div className="fighter-body enemy-body"></div>
        <div className="enemy-bar"><span style={{ width: "30%" }}></span></div>
      </div>
      <div className="fighter enemy c">
        <div className="fighter-shadow"></div>
        <div className="fighter-body enemy-body"></div>
        <div className="enemy-bar"><span style={{ width: "88%" }}></span></div>
      </div>

      <div className="hit-fx">
        <div className="hit hit-1">POW</div>
      </div>
    </div>
  );
}

/* ---------- HUD: player panel (health + charge) ---------- */
function PlayerPanel({ name, hp, hpMax, charge, chargeMax, style, accent }) {
  const hpPct = (hp / hpMax) * 100;
  const chgPct = (charge / chargeMax) * 100;
  const chargeReady = charge >= chargeMax;
  const critical = hpPct < 25;

  return (
    <div className={`player-panel style-${style} ${critical ? "critical" : ""}`}>
      <div className="portrait">
        <div className="portrait-frame">
          <div className="portrait-inner">
            <div className="portrait-silhouette" style={{ background: accent }}></div>
            <div className="portrait-scan"></div>
          </div>
          <div className="portrait-lvl">LV<span>27</span></div>
        </div>
      </div>

      <div className="player-stats">
        <div className="player-id">
          <span className="player-name">{name}</span>
          <span className="player-class">STREET BRAWLER · S-RANK</span>
        </div>

        <div className="bar-row">
          <div className="bar-label">
            <span className="bar-tag">HP</span>
            <span className="bar-val mono">{pad(Math.round(hp), 3)}<span className="bar-max">/{hpMax}</span></span>
          </div>
          <div className="bar hp-bar">
            <div className="bar-track">
              <div
                className="bar-fill hp-fill"
                style={{ width: `${hpPct}%` }}
              ></div>
              <div className="bar-fill hp-fill-trail" style={{ width: `${hpPct}%` }}></div>
              <div className="bar-notches">
                {[25, 50, 75].map((n) => (
                  <span key={n} style={{ left: `${n}%` }}></span>
                ))}
              </div>
            </div>
          </div>
        </div>

        <div className="bar-row">
          <div className="bar-label">
            <span className="bar-tag chg">CHARGE</span>
            <span className="bar-val mono">
              {chargeReady ? <span className="ready-tag">READY</span> : `${Math.round(chgPct)}%`}
            </span>
          </div>
          <div className={`bar chg-bar ${chargeReady ? "ready" : ""}`}>
            <div className="bar-track">
              <div className="bar-fill chg-fill" style={{ width: `${chgPct}%` }}></div>
              <div className="chg-pulse"></div>
            </div>
          </div>
          <div className="ability-icons">
            <div className={`ability ${chargeReady ? "lit" : ""}`} title="DRAGON FIST">
              <span className="ab-glyph">◈</span>
              <span className="ab-key">Q</span>
            </div>
            <div className="ability dim" title="Locked">
              <span className="ab-glyph">✦</span>
              <span className="ab-key">E</span>
            </div>
            <div className="ability dim" title="Locked">
              <span className="ab-glyph">⌬</span>
              <span className="ab-key">R</span>
            </div>
          </div>
        </div>

        <div className="status-row">
          <span className="status buff">▲ ATK +15%</span>
          <span className="status buff">▲ SPD +08%</span>
          <span className="status">⏱ 02:47</span>
        </div>
      </div>
    </div>
  );
}

/* ---------- HUD: wave + enemy tracker (top-right) ---------- */
function WavePanel({ wave, waveMax, enemiesLeft, enemiesTotal, style, accent }) {
  const wavePct = (wave / waveMax) * 100;
  const enemyPct = ((enemiesTotal - enemiesLeft) / enemiesTotal) * 100;
  return (
    <div className={`wave-panel style-${style}`}>
      <div className="wave-header">
        <div className="wave-eyebrow">DISTRICT 7 · LOWER NEON</div>
        <div className="wave-title">
          <span className="wave-word">WAVE</span>
          <span className="wave-num mono">{pad(wave)}</span>
          <span className="wave-of">/{pad(waveMax)}</span>
        </div>
      </div>

      <div className="wave-progress">
        <div className="wave-pips">
          {[...Array(waveMax)].map((_, i) => {
            const cleared = i < wave - 1;
            const current = i === wave - 1;
            const boss = i === waveMax - 1;
            return (
              <span
                key={i}
                className={`pip ${cleared ? "cleared" : ""} ${current ? "current" : ""} ${boss ? "boss" : ""}`}
              >
                {boss ? "✦" : ""}
              </span>
            );
          })}
        </div>
      </div>

      <div className="enemy-tracker">
        <div className="enemy-head">
          <span className="et-label">HOSTILES REMAINING</span>
          <span className="et-count mono">
            <span className="et-now">{pad(enemiesLeft)}</span>
            <span className="et-sep">/</span>
            <span className="et-tot">{pad(enemiesTotal)}</span>
          </span>
        </div>
        <div className="enemy-bar-row">
          <div className="enemy-bar-track">
            <div className="enemy-bar-fill" style={{ width: `${enemyPct}%` }}></div>
          </div>
        </div>
        <div className="enemy-icons">
          {[...Array(enemiesTotal)].map((_, i) => {
            const down = i < enemiesTotal - enemiesLeft;
            const isBoss = i === enemiesTotal - 1;
            return (
              <div key={i} className={`enemy-icon ${down ? "down" : ""} ${isBoss ? "boss" : ""}`}>
                {isBoss ? "♛" : "▼"}
              </div>
            );
          })}
        </div>
        <div className="enemy-types">
          <span className="etype">× 3 GRUNT</span>
          <span className="etype">× 2 BLADE</span>
          <span className="etype boss-type">× 1 BOSS</span>
        </div>
      </div>
    </div>
  );
}

/* ---------- HUD: combo + score (bottom) ---------- */
function ComboPanel({ combo, score, multiplier }) {
  return (
    <div className="combo-panel">
      <div className="combo-block">
        <div className="combo-num mono">{combo}</div>
        <div className="combo-label">HIT COMBO</div>
        <div className="combo-mult">×{multiplier.toFixed(1)}</div>
      </div>
      <div className="score-block">
        <div className="score-label">SCORE</div>
        <div className="score-num mono">{pad(score, 7)}</div>
      </div>
    </div>
  );
}

/* ---------- input prompt strip (bottom-right) ---------- */
function InputStrip() {
  const keys = [
    { k: "←→", l: "MOVE" },
    { k: "J", l: "PUNCH" },
    { k: "K", l: "KICK" },
    { k: "L", l: "GRAB" },
    { k: "SPC", l: "DODGE" },
    { k: "Q", l: "SPECIAL" },
  ];
  return (
    <div className="input-strip">
      {keys.map((k, i) => (
        <div className="ks" key={i}>
          <span className="ks-key">{k.k}</span>
          <span className="ks-lbl">{k.l}</span>
        </div>
      ))}
    </div>
  );
}

/* ---------- main app ---------- */
function App() {
  const [t, setTweak] = useTweaks(TWEAK_DEFAULTS);
  const accent = t.accent;

  /* live state */
  const [hp, setHp] = useState(186);
  const hpMax = 240;
  const [charge, setCharge] = useState(43);
  const chargeMax = 100;
  const [enemiesLeft, setEnemiesLeft] = useState(6);
  const enemiesTotal = 6;
  const [wave, setWave] = useState(3);
  const waveMax = 8;
  const [combo, setCombo] = useState(27);
  const [score, setScore] = useState(184250);
  const [multiplier, setMultiplier] = useState(2.4);

  /* simulate light combat */
  useEffect(() => {
    const id = setInterval(() => {
      setHp((h) => {
        const next = h - Math.random() * 3;
        if (next < 30) return Math.min(hpMax, next + 80); // pretend medkit
        return clamp(next, 0, hpMax);
      });
      setCharge((c) => {
        const next = c + 4 + Math.random() * 2;
        return next > chargeMax ? 0 : next;
      });
      setCombo((c) => clamp(c + Math.round(Math.random() * 3), 0, 99));
      setScore((s) => s + Math.round(120 + Math.random() * 400));
      setMultiplier((m) => clamp(m + (Math.random() - 0.45) * 0.1, 1, 5));
    }, 700);
    return () => clearInterval(id);
  }, []);

  useEffect(() => {
    const id = setInterval(() => {
      setEnemiesLeft((e) => {
        if (e <= 1) {
          setWave((w) => (w < waveMax ? w + 1 : w));
          return enemiesTotal;
        }
        return e - 1;
      });
    }, 4200);
    return () => clearInterval(id);
  }, []);

  // override hp for "low health" tweak
  const displayHp = t.lowHealth ? 38 : hp;

  return (
    <div
      className={`game ${t.showGrain ? "grain" : ""} ${t.showScanlines ? "scan" : ""}`}
      style={{ "--accent": accent, "--accent-glow": accent + "66" }}
    >
      <Scene accent={accent} />

      {/* top-left HUD */}
      <div className="hud hud-tl">
        <PlayerPanel
          name={t.playerName}
          hp={displayHp}
          hpMax={hpMax}
          charge={charge}
          chargeMax={chargeMax}
          style={t.hudStyle}
          accent={accent}
        />
      </div>

      {/* top-right HUD */}
      <div className="hud hud-tr">
        <WavePanel
          wave={wave}
          waveMax={waveMax}
          enemiesLeft={enemiesLeft}
          enemiesTotal={enemiesTotal}
          style={t.hudStyle}
          accent={accent}
        />
      </div>

      {/* bottom-left HUD */}
      <div className="hud hud-bl">
        <ComboPanel combo={combo} score={score} multiplier={multiplier} />
      </div>

      {/* bottom-right HUD */}
      <div className="hud hud-br">
        <InputStrip />
      </div>

      {/* center crowd-event banner */}
      <div className="event-banner">
        <div className="eb-line">⚠ MINI-BOSS INBOUND</div>
        <div className="eb-sub">CLEAR REMAINING TO ADVANCE</div>
      </div>

      <TweaksPanel title="Tweaks" defaultPos={{ right: 16, bottom: 16 }}>
        <TweakSection title="HUD Style">
          <TweakRadio
            label="Frame"
            value={t.hudStyle}
            options={[
              { value: "slash", label: "Slash" },
              { value: "bracket", label: "Bracket" },
              { value: "arcade", label: "Arcade" },
            ]}
            onChange={(v) => setTweak("hudStyle", v)}
          />
          <TweakColor
            label="Accent"
            value={t.accent}
            options={ACCENTS}
            onChange={(v) => setTweak("accent", v)}
          />
        </TweakSection>
        <TweakSection title="Atmosphere">
          <TweakToggle label="Film grain" value={t.showGrain} onChange={(v) => setTweak("showGrain", v)} />
          <TweakToggle label="Scanlines" value={t.showScanlines} onChange={(v) => setTweak("showScanlines", v)} />
          <TweakToggle label="Force low-HP state" value={t.lowHealth} onChange={(v) => setTweak("lowHealth", v)} />
        </TweakSection>
      </TweaksPanel>
    </div>
  );
}

ReactDOM.createRoot(document.getElementById("root")).render(<App />);
