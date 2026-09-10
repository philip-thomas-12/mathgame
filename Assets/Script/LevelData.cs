using UnityEngine;

/// <summary>
/// Metadata for one playable level / game mode. One asset per level in the
/// project, editable in the Unity Inspector (or created with
/// [CreateAssetMenu]). The LevelSelectManager and LevelManager drive the
/// flow: titles are shown in a select screen, locked levels display the
/// reason (e.g. "clear Unit 1"), and clearing a level unlocks the next.
///
/// Note: chains use a clear serialisation-friendly pattern so the data can
/// live as plain assets plus optional JSON, and so the editor can preview them.
/// </summary>
[CreateAssetMenu(fileName = "NewLevelData", menuName = "Matheor/Level Data")]
public class LevelData : ScriptableObject {

    [Tooltip("Display name on the level select screen and top-left HUD")]
    public string title = "Level 1";

    [Tooltip("Short player-facing description, shown when the level is locked and as the sub-text")]
    [TextArea(1, 3)]
    public string description = "Describe what the player does in this level.";

    [Tooltip("Why the level is locked (e.g. \"Complete level 1 BODMAS first\"). "
             + "Shown only when locked > 0. Empty = unlocked.")]
    [TextArea(1, 2)]
    public string lockReason = "";

    /// <summary>
    /// 0 means unlocked (player can pick it). > 0 means locked —interpreted as
    /// the minimum cleared level index before this one opens. If set, we compare
    /// against the highest cleared level to decide visibility. If lockReason is
    /// empty but minClearedLevel is set, a generic message is shown.
    /// </summary>
    public int minClearedLevel = 0; // 0 == unlocked

    /// <summary>
    /// The playable mode this level uses. The same core loop is reused: a boss
    /// fight, an expression chain (numbers + operators falling), a FIRE button,
    /// and a boss HP bar. Each mode changes what items fall, how the expression
    /// is evaluated, and how the boss reacts — so the "gameplay" variety is
    /// meaningful but the codebase is reused end-to-end.
    /// </summary>
    public enum GameMode {
        // Standard BODMAS combat: falling numbers + operators, boss HP bar,
        // waves on clear.
        BODMAS,

        // Quiz mode: the boss presents a question (shown via a popup + top
        // hint), the player types / clicks answer options. Correct answer =
        // fire. Wrong answer = feedback only, no penalty (or small penaliy
        // when configured). The boss's HP represents the number of questions
        // answered.
        QUIZ,

        // Same as QUIZ but questions are data-set-defined (see LevelQuizQuestions
        // and LevelMathQuestions assets). Used for high-school/college level
        // material.
        ADVANCED_QUIZ,

        // Computer-arithmetic basics: carries/borrows shown as the boss's
        // "calculation" on a panel; the player builds an expression that
        // produces the correct digit. Boss HP = number of problems.
        COMPUTER_ARITHMETIC_BASICS,

        // Computer-arithmetic quiz: questions about binary/hex/complements,
        // powers of two, overflow, and signed representation.
        COMPUTER_ARITHMETIC_QUIZ
    }

    public GameMode mode = GameMode.BODMAS;

    /// <summary>
    /// Boss HP for the standard combat / computer-arithmetic modes. In quiz
    /// modes this is the number of questions.
    /// </summary>
    public int bossHp = 150;

    /// <summary>
    /// For quiz modes: how many questions to present per beat? In practice
    /// the quiz mode runs one question at a time; this is the question pool
    /// size / difficulty expressed as the target correct answers to clear.
    /// </summary>
    public int questionsToClear = 8;

    /// <summary>
    /// Minimum score threshold the player must reach before this level is
    /// considered "cleared" (score is tracked per play session; for an MVP
    /// we clear a level by beating the boss, but score thresholds let a
    /// level act like a real assignment). 0 = boss-clear is enough.
    /// </summary>
    public int minScore = 0; // 0 = boss clear enough

    /// <summary>
    /// Optional reference to a question-set asset for quiz modes. Leave null
    /// for combat/computer-arithmetic modes.
    /// </summary>
    public LevelQuizQuestions quizData = null;
}
