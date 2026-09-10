# 🛸 MATHEOR — BODMAS Math Defense

An arcade math game built with **Unity 6**: aliens (numbers) and artifacts (operators) fall
from the sky — click them to build a real arithmetic expression, then fire it at the boss.
The damage is computed with **true BODMAS / order of operations**, so `2 + 3 × 4` deals
**14** damage, not 20. Brackets are your power move: `(2 + 3) × 4` deals **20**.

The game now has **5 levels with progressive unlocking** — pick one from the level-select
screen at launch (locked levels are visible with the reason they're locked):

| # | Level | What you do |
|---|-------|-------------|
| 1 | **BODMAS** | Boss fight: build expressions from falling digits & operators, fire at the boss |
| 2 | **Math Quiz** | Multiple-choice questions; each correct answer drains the boss (HP = questions left) |
| 3 | **High-Level Math Quiz** | Harder quiz: powers `^`, brackets, negative numbers |
| 4 | **Computer Arithmetic Basics** | Catch golden `?` artifacts, then build an expression equal to the hidden target digit |
| 5 | **Computer Arithmetic Quiz** | Binary, hexadecimal, powers of two, bits & bytes |

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

- **BODMAS expression combat** — order of operations decides your damage
- **Boss fights with waves** — animated HP bar that turns yellow → red as it drops
- **Score · 5 lives · combo multiplier · endless waves** with difficulty ramp
- **Title screen, pause (P), wave-break and game-over screens, one-click restart**
- **Live expression preview** and friendly validation popups (it teaches, not just rejects)
- **Procedural sound effects** — all synthesized at runtime, zero audio assets
- **No scene edits needed** — the HUD, boss fallback, popups and camera shake are generated in code; just press ▶ Play

---

## 🗂️ Project Structure

All gameplay code lives in `Assets/Script/`:

| Script | Role |
|---|---|
| `MathExpr.cs` | BODMAS evaluator — tokenizer + recursive-descent parser |
| `LevelCatalog.cs` | The 5 levels: names, modes, unlock order, built-in question banks |
| `LevelSelectManager.cs` | Level-select screen (shows locked levels + reasons) |
| `LevelManager.cs` | Persists cleared levels / unlock progress (PlayerPrefs) |
| `LevelData.cs` / `LevelQuizQuestions.cs` | Data assets for authoring extra levels & question sets |
| `GameManager.cs` | Input: builds the expression chain, fires attacks at the boss |
| `GameDirector.cs` | Game state machine, score/lives/combo/waves, HUD, popups, screen shake |
| `Spawner.cs` | Spawns aliens + operator artifacts, difficulty ramp |
| `Number.cs` | A falling alien number (0–9); reports misses |
| `OperatorPickup.cs` | A falling operator artifact (+ − × ÷ ^ ( )) |
| `Boss.cs` | Boss HP, damage flash, HP bar |
| `SoundFx.cs` | Procedural SFX (sine tones & chirps) |
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

1. Title screen → click to start, point out SCORE / LIVES / WAVE HUD.
2. Build `2 + 3 × 4` on purpose — damage **14**. "Why not 20? BODMAS!"
3. Build `(2 + 3) × 4` with bracket artifacts — damage **20**.
4. Try `8 ÷ 0` — the game rejects division by zero with a popup.
5. Fire fast to stack a **COMBO**, then drop the boss to trigger **WAVE CLEARED**.
6. Show a missed alien costing a life, then **P** pause and restart.

---

## 🙏 Credits

- **Original prototype** — “Matheor” (2017, Unity 5.5) by [satraul/matheor](https://github.com/satraul/matheor), © 2017 Ahmad Satryaji Aulia, MIT License
- **BODMAS expansion** — boss waves, operator artifacts, expression parser, HUD, procedural SFX (2026)
