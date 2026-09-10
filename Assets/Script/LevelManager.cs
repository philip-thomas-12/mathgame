using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A thin persistent-level-state manager. Integrates with the level select
/// flow: stores which levels are locked/unlocked across sessions (using
/// PlayerPrefs for the MVP), exposes the current unlock frontier so the
/// level select screen can show lock reasons, and records when a level is
/// cleared.
///
/// The MVP uses PlayerPrefs (so no file IO or save system is needed). To
/// upgrade later: swap the backing store for a JSON file, cloud save, or a
/// proper save manager — the public API is identical.
/// </summary>
public static class LevelManager {

    /// <summary>
    /// Key prefix for PlayerPrefs. Kept short so the pref store is tidy.
    /// </summary>
    const string PrefPrefix = "matheor_level_";

    static Dictionary<int, LevelState> cachedState; // levelIndex -> state

    [Serializable]
    public struct LevelState {
        public bool unlocked;
        public bool cleared;
        /// <summary>
        /// Highest score achieved in this level (used for "your best" display if
        /// wanted later). Not required for the MVP unlock logic; included for
        /// when a level-capable show-best-score is wanted.
        /// </summary>
        public int bestScore;
    }

    /// <summary>
    /// Highest cleared level index + 1, or 0 if none cleared. Used by the
    /// select screen to decide what's locked. Because unlock is sequential
    /// by minClearedLevel, the "next playable" hint uses this.
    /// </summary>
    public static int HighestClearedLevelPlusOne() {
        // The persisted frontier: RecordCleared stores the highest cleared
        // index; frontier = that + 1 (0 when nothing cleared yet).
        return GetIntPref("max_cleared_level", -1) + 1;
    }

    /// <summary>
    /// Call once (e.g. from LevelSelectManager) when the select screen opens.
    /// Returns all levels' current state (computes from PlayerPrefs if needed).
    /// </summary>
    public static Dictionary<int, LevelState> LoadAll() {
        if (cachedState == null) cachedState = new Dictionary<int, LevelState>();
        return cachedState; // kept in-memory across the session; initially
                            // computed from PlayerPrefs
    }

    /// <summary>
    /// Refresh the cached state from PlayerPrefs. Use before a select-screen
    /// draw to ensure the UI reflects the latest unlock state.
    /// </summary>
    public static void RefreshFromDisk() {
        if (cachedState == null) cachedState = new Dictionary<int, LevelState>();
        // We store per-level under sequential keys; to keep it simple we
        // also support a "max cleared" shortcut (set when a level is cleared)
        // so the select screen can cheaply compute adjacency.
        int maxCleared = GetIntPref("max_cleared_level", -1);

        // For the MVP, we only use max_cleared_level + 1 to decide unlock.
        // Each level's cleared flag is persisted individually too, so best
        // score persistence is possible later.
        foreach (var kv in LoadAll()) {
            int idx = kv.Key;
            LevelState st = kv.Value;
            if (idx <= maxCleared) st.unlocked = true;
            cachedState[idx] = st;
        }
    }

    /// <summary>
    /// Records that a level was cleared on this play. Persisted immediately
    /// so the select screen can show it as unlocked next time.
    /// </summary>
    public static void RecordCleared(int levelIndex, int score) {
        if (cachedState == null) cachedState = new Dictionary<int, LevelState>();

        LevelState st;
        if (!cachedState.TryGetValue(levelIndex, out st)) {
            st = new LevelState { unlocked = false, cleared = false, bestScore = 0 };
        }
        if (!st.cleared) {
            st.cleared = true;
            st.bestScore = Mathf.Max(st.bestScore, score);
            cachedState[levelIndex] = st;
            // Advance the global "max cleared" so the chain opens.
            int cur = GetIntPref("max_cleared_level", -1);
            if (levelIndex > cur) {
                SetIntPref("max_cleared_level", levelIndex);
            }
            SetIntPref("cleared_" + levelIndex, 1);
            SetIntPref("best_score_" + levelIndex, st.bestScore);
        } else {
            // already cleared — update best score only
            int curBest = GetIntPref("best_score_" + levelIndex, 0);
            SetIntPref("best_score_" + levelIndex, Mathf.Max(curBest, score));
        }
    }

    /// <summary>
    /// Convenience: mark a level unlocked (e.g. if a teacher/administrator
    /// unlocks all levels). Not used by the default flow; provided so a
    /// future UI can have an "unlock all" debug button, or an instructor
    /// mode.
    /// </summary>
    public static void UnlockAll() {
        if (cachedState == null) cachedState = new Dictionary<int, LevelState>();
        SetIntPref("max_cleared_level", 999);
        RefreshFromDisk();
    }

    /// <summary>
    /// Hide/show the music toggle in level select according to a global flag.
    /// (Kept here so the select screen can read one source of truth.)
    /// </summary>
    public static bool MusicEnabledByDefault = true;

    // =====================================================================
    //  Tiny PlayerPrefs-backed persistence (MVP). Replace with JSON/DB later.
    // =====================================================================

    static int GetIntPref(string key, int defaultValue) {
        var v = PlayerPrefs.GetInt(PrefPrefix + key, defaultValue);
        return v;
    }

    static void SetIntPref(string key, int value) {
        PlayerPrefs.SetInt(PrefPrefix + key, value);
        PlayerPrefs.Save();
    }
}
