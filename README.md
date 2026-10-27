# 🛸 MATHEOR — BODMAS Math Defense

An arcade math game built with **Unity 6**: aliens (numbers) and artifacts (operators) fall
from the sky — click them to build a real arithmetic expression, then fire it at the boss.
The damage is computed with **true BODMAS / order of operations**, so `2 + 3 × 4` deals
**14** damage, not 20. Brackets are your power move: `(2 + 3) × 4` deals **20**.

The game now has **5 levels with progressive unlocking** — pick one from the level-select
screen at launch (locked levels are visible with the reason they're locked):

| # | Level | What you do |
|---|-------|-------------|
| 1 | **BODMAS Defense** | Boss fight: build expressions from falling digits & operators, fire at the boss |
| 2 | **Math Quiz** | Multiple-choice questions; each correct answer drains the boss (HP = questions left) |
| 3 | **High-Level Math Quiz** | Harder quiz: powers `^`, brackets, negative numbers |
| 4 | **The Missing x (Algebra)** | Solve for $x$ from incoming algebraic equations ($3x - 5 = 16$), fire $x$ to break missiles |
| 5 | **Trajectory Interceptor** | Parabolic artillery defense: compute roots & apex of $y = ax^2 + bx + c$ to intercept shells |

Clear a level and the next one unlocks (progress is saved between sessions).

> Originally a 2017 Unity 5.5 prototype ("combine falling alien numbers").
> Expanded into a full game loop with a BODMAS combat system, boss fights, waves,
> score/combo/lives, difficulty ramp, and procedural sound.

---

## 🎮 How to Play

| Action | Input |
|---|---|
| Add a falling number / operator to your attack | **Click it** |
| Remove it from the attack | **Click it again** |
| Fire the attack / submit | **SPACE / ENTER** or click **FIRE** |
| Answer a quiz question | **Click the option** |
| Pause | **P** |
| Back to level select | **ESC** |
| Music on/off | **M** |

1. **Alien numbers (0–9)** and **operator artifacts** (`+ − × ÷ ^` and `( )`) fall from the top.
2. Click them **in order** to chain an expression — the live preview at the bottom shows your chain.
3. The game stops illegal chains *before* you build them (`3 + + 5`, empty brackets, …) with a popup explaining why.
4. Press **SPACE** — the expression is parsed with BODMAS, and the result launches at the boss as damage.
5. Every alien that falls past the bottom **costs 1 life** (5 lives). Operators are free to miss.
6. Beat the boss → next wave: he returns with more HP and the spawn rate/speed ramp up.
7. Chain attacks within ~6 seconds of each other to build a **COMBO multiplier** on your score.

### 🧠 The BODMAS moment (great for a demo)
| Expression you build | Damage | Why |
|---|---|---|
| `2 + 3 × 4` | **14** | × before + |
| `(2 + 3) × 4` | **20** | brackets first |
| `3 ^ 2 + 1` | **10** | powers before + |
| `8 ÷ 0` | ✗ rejected | the game refuses division by zero |

The evaluator (`MathExpr.cs`) is a real recursive-descent parser:
**B**rackets → **O**rders (`^`, right-associative) → **D**ivision/**M**ultiplication → **A**ddition/**S**ubtraction.

---

## ✨ Features

- **Step-by-Step BODMAS Calculation Tree** — when firing, the game animates the intermediate reductions in real-time (e.g. `(2 + 3) × 4` ➔ `5 × 4` ➔ `20!`), demonstrating exact order of operations!
- **Active Boss Shields & Enrage Phase** — higher waves feature tactical shield rules (weak to even/odd, minimum damage requirements, $5\times$ multiples crit) and boss enrage states with dynamic tension music.
- **Tactical Power-Up Artifacts** — falling power-ups:
  - ⏱ **SLOW**: Time warp slows all falling objects by 60% for 6s.
  - 💣 **BOMB**: Screen-clearing shockwave destroying impending threats.
  - ⚡ **2X**: Doubles the damage of the next fired expression.
  - ❤️ **LIFE**: Restores 1 lost life.
- **Procedural Particle VFX System** — explosions, sparkles on collection, shockwaves, laser beam strikes, and smooth "ghost" health bar depletion.
- **Procedural Sound Engine** — synthesized white-noise explosions, laser sweeps, powerup chimes, metallic shield clangs, and tempo-reactive background music.
- **Expression Dock & Buff HUD** — arcade dashboard plate with live expression preview, active power-up cooldown meters, and combo multipliers.
- **Zero External Assets Needed** — HUD, buttons, particles, and audio are completely synthesized at runtime in C# code.

---

## 🗂️ Project Structure

All gameplay code lives in `Assets/Script/`:

| Script | Role |
|---|---|
| `MathExpr.cs` | BODMAS evaluator — tokenizer + recursive-descent parser + stepwise reduction |
| `CombatVfx.cs` | Procedural particle VFX (boss hits, sparkles, shockwaves, laser beams) |
| `PowerupPickup.cs` | Tactical powerups (Slow, Bomb, 2X Damage, Extra Life) |
| `Boss.cs` | Boss HP, shields (even/odd/min/multi5), ghost health bar, enrage phase |
| `SoundFx.cs` | Procedural SFX (sine tones, noise explosions, laser sweeps, dynamic music pitch) |
| `LevelCatalog.cs` | The 5 levels: names, modes, unlock order, built-in question banks |
| `LevelSelectManager.cs` | Level-select screen (shows locked levels + reasons) |
| `LevelManager.cs` | Persists cleared levels / unlock progress (PlayerPrefs) |
| `GameManager.cs` | Input: expression chaining, powerup collection, firing |
| `GameDirector.cs` | State machine, HUD, buff tracking, BODMAS breakdown animation |
| `Spawner.cs` | Spawns numbers, operator pickups, and powerup drops with speed modifiers |
| `Number.cs` | A falling alien number (0–9); reports misses |
| `OperatorPickup.cs` | A falling operator artifact (+ − × ÷ ^ ( )) |
| `TrajectoryMissile.cs` | Level 5 parabolic artillery shell — arcs across screen; intercepted by correct x coordinate |
| `BackgroundStar.cs` | Cosmetic parallax stars |

---

## ▶️ Running It

1. Open the project in **Unity 6** (developed against `6000.6.0f1`) via Unity Hub.
2. Open the scene `Assets/Scene/MainGame.unity`.
3. Press **Play** ▶ — the game bootstraps itself, no setup required.

**Building a standalone version:** *File → Build Settings* → add `MainGame` → pick
**macOS**, **Windows** or **Android** → *Build*. Input supports mouse (desktop) and touch (Android).

---

## 🎓 For the Presentation — one-minute demo script

### Level 1 — BODMAS Boss Fight
1. Title screen → click to start, point out SCORE / LIVES / WAVE HUD.
2. Build `2 + 3 × 4` on purpose — damage **14**. "Why not 20? BODMAS!"
3. Build `(2 + 3) × 4` with bracket artifacts — damage **20**.
4. Try `8 ÷ 0` — the game rejects division by zero with a popup.
5. Fire fast to stack a **COMBO**, then drop the boss to trigger **WAVE CLEARED**.
6. Show a missed alien costing a life, then **P** pause and restart.

### Level 4 — The Missing X (Algebra)
1. Open from the level-select screen (clear Level 1–3 first, or reset `PlayerPrefs`).
2. The HUD shows an algebraic missile equation, e.g. `SOLVE FOR x:   3x + 4 = 19`.
3. The solution is `x = 5`. Catch the `5` from the falling tokens and press **FIRE**.
4. Correct → the equation explodes with a laser + "CORRECT! x = 5" popup. Wrong → lose a life.
5. Each equation comes with a step hint (e.g. *"hint: subtract 4, then divide by 3"*).

### Level 5 — Trajectory Curve Interceptor
1. A real parabolic missile arcs across the screen with a glowing orange trail.
2. The HUD shows the quadratic, e.g. `⚠ PARABOLA ZERO: y = −(x−1)(x−7)`.
3. The larger root is `x = 7`. Catch `7` from falling tokens and press **FIRE**.
4. The laser beam intercepts the shell mid-flight with a cyan shockwave explosion.

---

## 🙏 Credits

- **Original prototype** — “Matheor” (2017, Unity 5.5) by [satraul/matheor](https://github.com/satraul/matheor), © 2017 Ahmad Satryaji Aulia, MIT License
- **BODMAS expansion** — boss waves, operator artifacts, expression parser, HUD, procedural SFX (2026)
