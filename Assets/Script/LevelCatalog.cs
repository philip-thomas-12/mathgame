using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Where the list of levels comes from. Priority:
///   1. LevelData assets in a "Resources/Matheor/Levels" folder (instructor-authored).
///   2. A built-in default set of 5 levels (created in code), so the game is
///      instantly playable with zero setup.
///
/// Unlock rule: a level is playable once the player has cleared at least
/// `minClearedLevel` levels (LevelManager tracks the frontier in PlayerPrefs).
/// </summary>
public static class LevelCatalog {

    static LevelData[] cached;

    public static LevelData[] GetLevels() {
        if (cached != null && cached.Length > 0) return cached;

        LevelData[] loaded = Resources.LoadAll<LevelData>("Matheor/Levels");
        if (loaded != null && loaded.Length > 0) {
            System.Array.Sort(loaded, (a, b) => a.minClearedLevel.CompareTo(b.minClearedLevel));
            cached = loaded;
            return cached;
        }
        cached = BuildDefaults();
        return cached;
    }

    public static bool IsUnlocked(int index) {
        LevelData[] levels = GetLevels();
        if (index < 0 || index >= levels.Length) return false;
        return levels[index].minClearedLevel <= LevelManager.HighestClearedLevelPlusOne();
    }

    static LevelData Build(string title, string desc, LevelData.GameMode mode,
                           int bossHp, int questions, int minCleared, LevelQuizQuestions quiz) {
        LevelData d = ScriptableObject.CreateInstance<LevelData>();
        d.title = title;
        d.description = desc;
        d.mode = mode;
        d.bossHp = bossHp;
        d.questionsToClear = questions;
        d.minClearedLevel = minCleared;
        d.quizData = quiz;
        d.lockReason = (minCleared > 0) ? ("clear level " + minCleared + " first") : "";
        return d;
    }

    static LevelData[] BuildDefaults() {
        List<LevelData> list = new List<LevelData>();
        list.Add(Build("LEVEL 1 - BODMAS",
                       "Boss fight: build expressions from falling digits & operators",
                       LevelData.GameMode.BODMAS, 150, 0, 0, null));
        list.Add(Build("LEVEL 2 - MATH QUIZ",
                       "Multiple choice: beat the quiz before lives run out",
                       LevelData.GameMode.QUIZ, 0, 8, 1, DefaultMathQuiz()));
        list.Add(Build("LEVEL 3 - HIGH-LEVEL MATH QUIZ",
                       "Powers, brackets and negatives",
                       LevelData.GameMode.ADVANCED_QUIZ, 0, 8, 2, DefaultAdvancedQuiz()));
        list.Add(Build("LEVEL 4 - COMPUTER ARITHMETIC BASICS",
                       "Hit the boss's target digit: column sums, carries & borrows",
                       LevelData.GameMode.COMPUTER_ARITHMETIC_BASICS, 0, 8, 3, null));
        list.Add(Build("LEVEL 5 - COMPUTER ARITHMETIC QUIZ",
                       "Binary, hex and powers of two",
                       LevelData.GameMode.COMPUTER_ARITHMETIC_QUIZ, 0, 6, 4, DefaultCompQuiz()));
        return list.ToArray();
    }

    static LevelQuizQuestions DefaultMathQuiz() {
        LevelQuizQuestions q = ScriptableObject.CreateInstance<LevelQuizQuestions>();
        q.requiredCorrect = 8;
        q.questions = new List<LevelQuizQuestions.Question> {
            Q("7 × 6 = ?", new[] { "36", "42", "48", "56" }, 1, "7 × 6 = 42"),
            Q("8 + 5 = ?",  new[] { "12", "13", "14", "15" }, 1, "8 + 5 = 13"),
            Q("15 − 9 = ?", new[] { "5", "6", "7", "8" },     1, "15 − 9 = 6"),
            Q("9 × 3 = ?",  new[] { "24", "27", "29", "18" }, 1, "9 × 3 = 27"),
            Q("24 ÷ 4 = ?", new[] { "5", "6", "7", "8" },     1, "24 ÷ 4 = 6"),
            Q("2 + 3 × 4 = ?", new[] { "20", "14", "24", "10" }, 1, "BODMAS: × first → 2 + 12 = 14"),
            Q("18 ÷ 2 + 1 = ?", new[] { "10", "19", "9", "8" }, 0, "÷ first → 9 + 1 = 10"),
            Q("5 × (2 + 3) = ?", new[] { "25", "16", "11", "20" }, 0, "Brackets first → 5 × 5 = 25"),
            Q("36 ÷ 6 = ?", new[] { "5", "6", "7", "9" },     1, "36 ÷ 6 = 6"),
            Q("12 × 2 − 4 = ?", new[] { "16", "20", "24", "28" }, 1, "12 × 2 = 24, 24 − 4 = 20")
        };
        return q;
    }

    static LevelQuizQuestions DefaultAdvancedQuiz() {
        LevelQuizQuestions q = ScriptableObject.CreateInstance<LevelQuizQuestions>();
        q.requiredCorrect = 8;
        q.questions = new List<LevelQuizQuestions.Question> {
            Q("3 ^ 2 + 1 = ?",      new[] { "10", "7", "9", "12" },  0, "Powers first: 9 + 1 = 10"),
            Q("(2 + 3) × 4 = ?",    new[] { "20", "10", "14", "24" }, 0, "Brackets first: 5 × 4 = 20"),
            Q("2 ^ 3 × 2 = ?",      new[] { "16", "12", "8", "64" },  0, "2^3 = 8, 8 × 2 = 16"),
            Q("−5 + 8 = ?",         new[] { "3", "13", "-3", "2" },   0, "−5 + 8 = 3"),
            Q("100 ÷ 4 ÷ 5 = ?",    new[] { "5", "20", "1", "25" },   0, "Left to right: 25 ÷ 5 = 5"),
            Q("5 + 2 × (6 − 4) = ?", new[] { "9", "14", "18", "7" },  0, "Brackets: 5 + 2×2 = 9"),
            Q("2 ^ (1 + 2) = ?",    new[] { "8", "6", "9", "16" },    0, "Brackets: 2^3 = 8"),
            Q("7 × 8 − 6 ÷ 2 = ?",  new[] { "53", "25", "50", "47" }, 0, "× and ÷ first: 56 − 3 = 53"),
            Q("(10 − 3) ^ 2 = ?",   new[] { "49", "17", "21", "14" }, 0, "Brackets: 7^2 = 49"),
            Q("−3 × −4 = ?",        new[] { "12", "-12", "7", "-7" }, 0, "Negative × negative = positive")
        };
        return q;
    }

    static LevelQuizQuestions DefaultCompQuiz() {
        LevelQuizQuestions q = ScriptableObject.CreateInstance<LevelQuizQuestions>();
        q.requiredCorrect = 6;
        q.questions = new List<LevelQuizQuestions.Question> {
            Q("Binary 101 = ? (decimal)",   new[] { "5", "3", "6", "7" },       0, "1×4 + 0×2 + 1×1 = 5"),
            Q("Binary 1101 = ? (decimal)",  new[] { "13", "11", "14", "9" },    0, "8 + 4 + 1 = 13"),
            Q("Decimal 6 = ? (binary)",     new[] { "110", "101", "011", "111" }, 0, "6 = 4 + 2 = 110"),
            Q("Hex A = ? (decimal)",        new[] { "10", "11", "9", "14" },    0, "A = 10 in hex"),
            Q("1 byte = ? bits",            new[] { "8", "4", "16", "32" },     0, "A byte is 8 bits"),
            Q("2 ^ 5 = ?",                  new[] { "32", "16", "25", "10" },   0, "2×2×2×2×2 = 32"),
            Q("Hex F = ? (decimal)",        new[] { "15", "16", "14", "17" },   0, "F = 15 in hex"),
            Q("Binary 1010 + 1 = ? (binary)", new[] { "1011", "1010", "1100", "1001" }, 0, "1010 + 1 = 1011")
        };
        return q;
    }

    static LevelQuizQuestions.Question Q(string text, string[] options, int correct, string explanation) {
        return new LevelQuizQuestions.Question {
            question = text,
            options = new List<string>(options),
            correctIndex = correct,
            explanation = explanation
        };
    }
}
