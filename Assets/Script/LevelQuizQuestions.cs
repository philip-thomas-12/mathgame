using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A question-set asset. Used by quiz-mode levels (Math Quiz, High-Level Math
/// Quiz, Computer Arithmetic Quiz). Each entry couples a question string (the
/// boss's "speech") with possible answer options and which option is correct.
///
/// You create these in the editor via "Create → Matheor/Quiz Questions", then
/// drag a LevelData onto a LevelData.quizData. On each question the game picks
/// one still-unanswered, questions-to-clear questions get asked, and the boss HP
/// is the questions-to-clear + base HP.
/// </summary>
[CreateAssetMenu(fileName = "NewQuizQuestions", menuName = "Matheor/Quiz Questions")]
public class LevelQuizQuestions : ScriptableObject {

    [Tooltip("How many questions the player must answer correctly to clear this level.")]
    public int requiredCorrect = 8;

    /// <summary>
    /// Number of wrong answers the player can give before losing this level.
    /// If negative, no limit (one-shot quiz style — wrong answers cost
    /// time/score only). If >= 0, the level fails immediately on that many
    /// wrong answers (boss explodes / game over).
    /// </summary>
    public int maxWrong = -1; // no limit

    [Tooltip("Question + answer data. Each row: question, list of options (one of which is the correct one), correct-option index, and an explanation shown on the correct answer.")]
    public List<Question> questions = new List<Question>();

    [Serializable]
    public struct Question {
        [Tooltip("The question string. For math-mode it can contain simple inline images/operators.")]
        public string question;

        [Tooltip("Options the player can pick (for a quiz mode these are clickable, displayed as multiple choice.")]
        public List<string> options;

        /// <summary>
        /// Which index in options[] is the correct answer. 0-based.
        /// </summary>
        [Tooltip("Which option is the correct one: 0 = first option in the list.")]
        public int correctIndex;

        [TextArea(1, 3)]
        public string explanation; // shown briefly when the player is right, or as feedback hwhen wrong
    }

    /// <summary>
    /// Shuffling helper: pick a random unanswered question from the set. For
    /// a real quiz we'd support "next unanswered", but the MVP uses random
    /// pick from remaining until the set is exhausted, then re-rolls (so the
    /// pool can be shorter than the required-correct count — the player must
    /// repeat questions until enough correct).
    /// </summary>
    public int TotalQuestions => questions.Count;
}
