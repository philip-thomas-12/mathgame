# ABSTRACT

## MATHEOR — A BODMAS-Based Math Defense Game

Mathematics education often struggles to make abstract rules — such as the order of
operations (BODMAS) — feel tangible to learners. MATHEOR addresses this by turning the
rule itself into the core game mechanic. Built as a 2D arcade game in **Unity 6 (C#)**,
the game drops alien numbers (0–9) and operator artifacts (+, −, ×, ÷, ^, brackets)
from the top of the screen. The player captures these items by clicking them in
sequence to assemble a live arithmetic expression, then fires it at a boss whose
health is reduced by the expression's correctly evaluated value. Because damage is
computed by a real recursive-descent parser implementing BODMAS precedence
(brackets → orders → division/multiplication → addition/subtraction), players
directly experience why `2 + 3 × 4` yields 14 and not 20, and how brackets change
the outcome to 20.

The system implements a complete game loop: wave-based boss fights with escalating
health and difficulty, a scoring system with time-based combo multipliers, a lives
system penalizing missed numbers, input validation that rejects malformed
expressions and division by zero with explanatory feedback, and title, pause and
game-over states. All user-interface elements and sound effects are generated at
runtime in code — the audio is synthesized procedurally from sine tones and chirps —
so the project requires no external assets beyond its original sprite set.

The result is a compact, replayable educational game (~600 lines of C# across nine
scripts) that demonstrates practical applications of parsing, state machines,
object lifecycle management and game-feel design, while reinforcing order-of-
operations skills through immediate, consequential feedback.

**Keywords:** BODMAS, serious games, Unity, C#, expression parsing, game-based learning

---

*Based on the 2017 open-source prototype “Matheor” by Ahmad Satryaji Aulia (MIT License),
extended with the BODMAS combat system, boss waves, HUD and procedural audio.*
