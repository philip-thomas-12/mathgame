using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central game director: owns the game state machine (level select → playing →
/// level complete / game over), score / lives, the HUD (built in code, no scene
/// edits needed), floating popups, camera shake and the restart / return-to-menu
/// flow. Created automatically when Play is pressed.
///
/// Modes (one per level, chosen on the level-select screen):
///   1. BODMAS                   — boss combat: build expressions from falling tokens, fire them
///   2. MATH QUIZ                — multiple-choice arithmetic questions; boss HP = questions left
///   3. HIGH-LEVEL MATH QUIZ     — harder quiz: powers (^), brackets, negative numbers
///   4. THE MISSING X (ALGEBRA)  — solve ax+b=c style equations for x; build x from falling tokens and fire
///   5. TRAJECTORY INTERCEPTOR   — parabolic artillery defense: intercept y=ax²+bx+c shells by
///                                  computing the root or apex x coordinate and firing it
///
/// Level gating: LevelCatalog defines the levels and their unlock order;
/// LevelManager persists cleared progress (PlayerPrefs). The select screen shows
/// every level, including locked ones with their lock reason.
///
/// Keys: SPACE = fire · P = pause · ESC = back to level select · M = music.
/// </summary>
public class GameDirector : MonoBehaviour {

    public enum GameState { Ready, Playing, LevelComplete, GameOver }

    public static GameDirector Instance { get; private set; }

    public GameState State { get; private set; }
    public bool InputEnabled {
        get { return State == GameState.Playing && !paused && Time.timeScale > 0f; }
    }
    public bool SpawningEnabled {
        get { return State == GameState.Playing && !paused; }
    }

    public int Score { get; private set; }
    public int Lives { get; private set; }
    public Spawner.Mode CurrentMode { get; private set; }
    public LevelData CurrentLevel { get; private set; }
    public int CurrentLevelIndex { get; private set; }
    public bool HasDoubleDamage { get; private set; }
    public void ConsumeDoubleDamage() { HasDoubleDamage = false; UpdateHud(); }

    const int StartLives = 5;
    const float ComboWindow = 6f;
    const float FontPx = 64f;

    int combo;
    float comboEndsAt, clickLockUntil;
    float freezeEndsAt;
    int expectedSolution = -1;
    string activeChallengeText = "";
    TrajectoryMissile activeMissile;
    int currentCorrectIndex = -1;   // quiz mode: correct option of the live question
    string currentExplanation = "";
    bool paused;

    Camera cam;
    Vector3 camHome;
    Spawner spawner;
    Boss boss;
    GameObject pendingBossGo;   // scene boss, hidden while the level-select screen is up
    float baseVelocity = 2.5f;

    TextMesh scoreText, livesText, levelText, modeText, targetText, buffText;
    TextMesh centerText, subText, previewText, questionText;
    GameObject fireButton, dockBg;
    readonly List<GameObject> attackTexts = new List<GameObject>();
    readonly List<GameObject> quizOptionObjects = new List<GameObject>();
    Coroutine shakeCo, quizCo, attackCo;

    static readonly Color GoldColor = new Color(1f, 0.85f, 0.35f);
    static readonly Color RedColor = new Color(1f, 0.35f, 0.30f);
    static readonly Color WhiteColor = new Color(0.95f, 0.95f, 0.98f);
    static readonly Color MutedWhite = new Color(0.70f, 0.70f, 0.78f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap() {
        if (Instance == null) new GameObject("~GameDirector").AddComponent<GameDirector>();
    }

    void Awake() {
        Instance = this;
        State = GameState.Ready;
        Time.timeScale = 1f;
        cam = Camera.main;
        if (cam != null) camHome = cam.transform.position;
    }

    void Start() {
        Score = 0;
        Lives = StartLives;
        combo = 0;

        spawner = FindAnyObjectByType<Spawner>();
        if (spawner != null) baseVelocity = Mathf.Max(spawner.velocity, 1f);

        // The level-select screen is code-generated too — nothing to wire in the scene.
        if (FindAnyObjectByType<LevelSelectManager>() == null) {
            new GameObject("~LevelSelect").AddComponent<LevelSelectManager>();
        }
        // Hide the scene boss (and its old label) until a level actually starts.
        GameObject foundBoss = GameObject.Find("boss");
        if (foundBoss != null) {
            foundBoss.SetActive(false);
            pendingBossGo = foundBoss;
        }
        SoundFx.StartMusic();
    }

    // =====================================================================
    //  Main loop
    // =====================================================================

    void Update() {
        if (Input.GetKeyDown(KeyCode.M)) SoundFx.ToggleMusic();

        if (State == GameState.Ready) {
            // Normally the level-select screen drives this state. Fallback:
            // if it is somehow absent, clicking starts level 1.
            if (CurrentLevel == null && Clicked() && FindAnyObjectByType<LevelSelectManager>() == null) {
                StartLevelByIndex(0);
            }
            return;
        }
        if (State == GameState.GameOver) {
            if (Time.unscaledTime > clickLockUntil && Clicked()) RestartLevel();
            return;
        }
        if (State == GameState.LevelComplete) {
            if (Clicked()) ReturnToSelect();
            return;
        }
        if (State != GameState.Playing) return;

        if (Input.GetKeyDown(KeyCode.P)) TogglePause();
        if (Input.GetKeyDown(KeyCode.Escape)) ReturnToSelect();
        if (paused) return;

        if (freezeEndsAt > 0f && Time.time > freezeEndsAt) {
            freezeEndsAt = 0f;
            if (spawner != null) spawner.SetSpeedMultiplier(1.0f);
            UpdateHud();
        }

        if (combo > 0 && Time.time > comboEndsAt) combo = 0;
        UpdateBuffText();
    }

    bool Clicked() {
        if (Input.GetMouseButtonDown(0)) return true;
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began) return true;
        return false;
    }

    void TogglePause() {
        paused = !paused;
        Time.timeScale = paused ? 0f : 1f;
        if (paused) ShowCenter("PAUSED", WhiteColor, 0.9f, "P = resume   ·   ESC = level select");
        else HideCenter();
    }

    void ShowCenter(string txt, Color c, float height, string sub) {
        if (centerText == null || subText == null) return;
        centerText.text = txt;
        centerText.color = c;
        centerText.characterSize = CharSize(height);
        centerText.gameObject.SetActive(true);
        subText.text = sub;
        subText.color = WhiteColor;
        subText.gameObject.SetActive(true);
    }

    void HideCenter() {
        if (centerText != null) centerText.gameObject.SetActive(false);
        if (subText != null) subText.gameObject.SetActive(false);
    }

    // =====================================================================
    //  Level lifecycle
    // =====================================================================

    /// <summary>Called by LevelSelectManager when the player picks a level.</summary>
    public void StartLevel(LevelData data, int index) {
        if (data == null) return;

        CurrentLevel = data;
        CurrentLevelIndex = index;
        CurrentMode = MapMode(data.mode);
        Score = 0;
        Lives = StartLives;
        combo = 0;
        expectedSolution = -1;
        currentCorrectIndex = -1;
        paused = false;
        Time.timeScale = 1f;

        if (activeMissile != null) {
            Destroy(activeMissile.gameObject);
            activeMissile = null;
        }

        spawner = FindAnyObjectByType<Spawner>();
        if (spawner != null) {
            spawner.CurrentMode = CurrentMode;
            baseVelocity = Mathf.Max(spawner.velocity, 1f);
        }

        // In quiz / algebra / trajectory modes the boss HP is the number of problems to solve,
        // so the HP bar doubles as level progress. Combat uses the level's own HP.
        bool isProblemMode = CurrentMode != Spawner.Mode.Standard;
        int hp = isProblemMode ? Mathf.Max(1, data.questionsToClear)
                               : Mathf.Max(1, data.bossHp);
        SetupBoss(hp);
        BuildHud();
        HideCenter();

        bool combatHud = (CurrentMode == Spawner.Mode.Standard ||
                          CurrentMode == Spawner.Mode.AlgebraSolver ||
                          CurrentMode == Spawner.Mode.TrajectoryInterceptor);
        if (fireButton != null) fireButton.SetActive(combatHud);
        if (previewText != null) previewText.gameObject.SetActive(combatHud);
        if (dockBg != null) dockBg.SetActive(combatHud);
        if (questionText != null) questionText.gameObject.SetActive(!combatHud);
        if (targetText != null) {
            bool hasTarget = (CurrentMode == Spawner.Mode.AlgebraSolver ||
                              CurrentMode == Spawner.Mode.TrajectoryInterceptor);
            targetText.gameObject.SetActive(hasTarget);
            targetText.text = "";
        }

        State = GameState.Playing;
        UpdateHud();
        SoundFx.Go();
        Popup(data.title, CenterTop(), GoldColor, 0.6f);

        if (CurrentMode == Spawner.Mode.Quiz || CurrentMode == Spawner.Mode.AdvancedQuiz) {
            SpawnNextQuizQuestion();
        } else if (CurrentMode == Spawner.Mode.AlgebraSolver) {
            GenerateNextAlgebraProblem();
        } else if (CurrentMode == Spawner.Mode.TrajectoryInterceptor) {
            GenerateNextTrajectoryProblem();
        }
    }

    void StartLevelByIndex(int index) {
        LevelData[] levels = LevelCatalog.GetLevels();
        if (index < 0 || index >= levels.Length) return;
        StartLevel(levels[index], index);
    }

    Spawner.Mode MapMode(LevelData.GameMode m) {
        switch (m) {
            case LevelData.GameMode.QUIZ:                    return Spawner.Mode.Quiz;
            case LevelData.GameMode.ADVANCED_QUIZ:           return Spawner.Mode.AdvancedQuiz;
            case LevelData.GameMode.ALGEBRA_SOLVER:          return Spawner.Mode.AlgebraSolver;
            case LevelData.GameMode.TRAJECTORY_INTERCEPTOR:  return Spawner.Mode.TrajectoryInterceptor;
            default:                                         return Spawner.Mode.Standard;
        }
    }

    void SetupBoss(int hp) {
        // GameObject.Find can't see inactive objects, so we keep the hidden
        // scene boss in pendingBossGo and re-use it here.
        GameObject bossGo = pendingBossGo;
        if (bossGo == null) {
            bossGo = GameObject.Find("boss");
            if (bossGo == null) bossGo = CreateFallbackBoss();
        }
        bossGo.SetActive(true);
        pendingBossGo = null;
        boss = bossGo.GetComponent<Boss>();
        if (boss == null) boss = bossGo.AddComponent<Boss>();
        boss.Init(hp);
    }

    void RestartLevel() {
        if (CurrentLevel == null) { ReturnToSelect(); return; }
        StartLevel(CurrentLevel, CurrentLevelIndex);
    }

    void ReturnToSelect() {
        Time.timeScale = 1f;
        paused = false;
        HasDoubleDamage = false;
        freezeEndsAt = 0f;
        if (activeMissile != null) {
            Destroy(activeMissile.gameObject);
            activeMissile = null;
        }
        if (spawner != null) spawner.SetSpeedMultiplier(1.0f);
        SoundFx.SetMusicPitch(1.0f);
        ClearNumbers();
        ClearAttackTexts();
        ClearQuizOptions();
        if (fireButton != null) fireButton.SetActive(false);
        if (previewText != null) previewText.gameObject.SetActive(false);
        if (dockBg != null) dockBg.SetActive(false);
        if (questionText != null) questionText.gameObject.SetActive(false);
        if (targetText != null) targetText.gameObject.SetActive(false);
        CurrentLevel = null;
        CurrentLevelIndex = -1;
        State = GameState.Ready;
        HideCenter();
        // Hide the boss again while the select screen is up.
        if (boss != null) {
            boss.gameObject.SetActive(false);
            pendingBossGo = boss.gameObject;
            boss = null;
        }
        var ls = FindAnyObjectByType<LevelSelectManager>();
        if (ls != null) ls.Enable();
    }

    /// <summary>Called by LevelSelectManager (ESC handling etc.).</summary>
    public bool ShowLevelSelect() {
        ReturnToSelect();
        return FindAnyObjectByType<LevelSelectManager>() != null;
    }

    // =====================================================================
    //  HUD
    // =====================================================================

    void BuildHud() {
        float s = UiScale();
        Vector2 c = CamCenter();
        float topY = c.y + CamHalfH() - 0.75f * s;
        float leftX = c.x - CamHalfW() * 0.93f;
        float rightX = c.x + CamHalfW() * 0.93f;

        if (scoreText == null) scoreText = MakeWorldText("SCORE 0", new Vector3(leftX, topY, 0f), 0.5f, WhiteColor, 50, TextAnchor.UpperLeft);
        if (livesText == null) livesText = MakeWorldText("LIVES " + Lives, new Vector3(rightX, topY, 0f), 0.5f, WhiteColor, 50, TextAnchor.UpperRight);
        if (levelText == null) levelText = MakeWorldText("", new Vector3(c.x, topY, 0f), 0.42f, WhiteColor, 50, TextAnchor.UpperCenter);
        if (modeText == null)  modeText  = MakeWorldText("", new Vector3(c.x, topY - 0.62f * s, 0f), 0.32f, MutedWhite, 50, TextAnchor.UpperCenter);
        if (buffText == null)  buffText  = MakeWorldText("", new Vector3(leftX, topY - 0.58f * s, 0f), 0.34f, GoldColor, 50, TextAnchor.UpperLeft);
        if (targetText == null) targetText = MakeWorldText("", new Vector3(c.x, topY - 1.05f * s, 0f), 0.34f, GoldColor, 50, TextAnchor.UpperCenter);
        if (questionText == null) questionText = MakeWorldText("", new Vector3(c.x, c.y + CamHalfH() * 0.35f, 0f), 0.42f, WhiteColor, 46, TextAnchor.UpperCenter);
        if (centerText == null) {
            centerText = MakeWorldText("", new Vector3(c.x, c.y + 0.6f, 0f), 1.0f, GoldColor, 58, TextAnchor.MiddleCenter);
            centerText.fontStyle = FontStyle.Bold;
        }
        if (subText == null) subText = MakeWorldText("", new Vector3(c.x, c.y - 0.8f, 0f), 0.42f, WhiteColor, 58, TextAnchor.UpperCenter);
        targetText.gameObject.SetActive(false);
        questionText.gameObject.SetActive(false);
        centerText.gameObject.SetActive(false);
        subText.gameObject.SetActive(false);
    }

    string ModeLabelFor(Spawner.Mode mode) {
        switch (mode) {
            case Spawner.Mode.Standard:                  return "BODMAS BOSS FIGHT";
            case Spawner.Mode.Quiz:                      return "MATH QUIZ";
            case Spawner.Mode.AdvancedQuiz:              return "HIGH-LEVEL MATH QUIZ";
            case Spawner.Mode.AlgebraSolver:              return "THE MISSING X (ALGEBRA)";
            case Spawner.Mode.TrajectoryInterceptor:      return "TRAJECTORY INTERCEPTOR (y = ax² + bx + c)";
            default:                                     return "";
        }
    }

    void UpdateHud() {
        if (scoreText != null) scoreText.text = "SCORE " + Score;
        if (livesText != null) {
            livesText.text = "LIVES " + Lives;
            livesText.color = (Lives <= 1) ? RedColor : WhiteColor;
        }
        if (levelText != null) levelText.text = (CurrentLevel != null) ? CurrentLevel.title : "";
        if (modeText != null) modeText.text = ModeLabelFor(CurrentMode);
        UpdateBuffText();
    }

    void UpdateBuffText() {
        if (buffText == null) return;
        string b = "";
        if (freezeEndsAt > Time.time) {
            b += "⏱ SLOW: " + (freezeEndsAt - Time.time).ToString("0.0") + "s  ";
        }
        if (HasDoubleDamage) {
            b += "⚡ 2X DMG READY  ";
        }
        if (combo >= 2) {
            b += "🔥 COMBO x" + combo;
        }
        buffText.text = b;
    }

    // =====================================================================
    //  Tactical Power-Ups
    // =====================================================================

    public void ActivatePowerup(PowerupPickup.Type type, Vector3 at) {
        if (State != GameState.Playing) return;
        switch (type) {
            case PowerupPickup.Type.Freeze:
                freezeEndsAt = Time.time + 6.0f;
                if (spawner != null) spawner.SetSpeedMultiplier(0.4f);
                Popup("⏱ TIME WARP! (SLOW)", at, new Color(0.25f, 0.85f, 1f), 0.55f);
                break;
            case PowerupPickup.Type.Bomb:
                DetonateBomb(at);
                break;
            case PowerupPickup.Type.DoubleDamage:
                HasDoubleDamage = true;
                Popup("⚡ 2X DAMAGE READY!", at, GoldColor, 0.55f);
                break;
            case PowerupPickup.Type.ExtraLife:
                Lives = Mathf.Min(Lives + 1, StartLives);
                Popup("❤️ +1 LIFE!", at, new Color(1f, 0.35f, 0.65f), 0.55f);
                break;
        }
        UpdateHud();
    }

    void DetonateBomb(Vector3 at) {
        if (CombatVfx.Instance != null) {
            CombatVfx.Instance.SpawnShockwave(at, new Color(1f, 0.4f, 0.2f), 8.0f);
        }
        SoundFx.Explosion(true);
        Popup("💣 BOMB DETONATED!", at, new Color(1f, 0.4f, 0.2f), 0.65f);
        Shake(0.3f, 0.15f);

        // Vaporize alien numbers currently below Y = 6.0
        GameObject[] nums = GameObject.FindGameObjectsWithTag("number");
        int vaporized = 0;
        for (int i = 0; i < nums.Length; i++) {
            if (nums[i] != null && nums[i].transform.position.y < 6.0f) {
                if (CombatVfx.Instance != null) {
                    CombatVfx.Instance.SpawnPickupSparkle(nums[i].transform.position, Color.red);
                }
                Destroy(nums[i]);
                vaporized++;
            }
        }
        if (vaporized > 0) {
            Score += vaporized * 50;
            UpdateHud();
        }
    }

    // =====================================================================
    //  Gameplay API — combat (called by GameManager / Boss / Number)
    // =====================================================================

    public void PlayBodmasAttack(List<string> steps, int finalDmg, Vector3 fromMid, System.Action onLaunch) {
        if (attackCo != null) StopCoroutine(attackCo);
        attackCo = StartCoroutine(BodmasAttackCo(steps, finalDmg, fromMid, onLaunch));
    }

    IEnumerator BodmasAttackCo(List<string> steps, int finalDmg, Vector3 fromMid, System.Action onLaunch) {
        if (steps != null && steps.Count > 1) {
            Vector3 centerPos = CenterTop() + Vector3.down * 0.4f;
            TextMesh banner = MakeWorldText("", centerPos, 0.55f, GoldColor, 60, TextAnchor.MiddleCenter);
            banner.fontStyle = FontStyle.Bold;

            for (int i = 0; i < steps.Count; i++) {
                banner.text = steps[i];
                SoundFx.Step();
                float timer = 0f;
                while (timer < 0.28f) {
                    timer += Time.deltaTime;
                    float scale = 1f + Mathf.Sin((timer / 0.28f) * Mathf.PI) * 0.18f;
                    banner.transform.localScale = Vector3.one * scale;
                    yield return null;
                }
            }

            banner.text = "⚡ " + finalDmg + " DAMAGE! ⚡";
            SoundFx.Laser();
            if (CombatVfx.Instance != null) {
                Vector3 bossTarget = (boss != null) ? boss.transform.position : centerPos + Vector3.down * 4f;
                CombatVfx.Instance.SpawnLaserBeam(centerPos, bossTarget, GoldColor, 0.25f);
            }
            yield return new WaitForSeconds(0.2f);
            Destroy(banner.gameObject);
        }

        if (onLaunch != null) onLaunch();
    }

    public void RegisterAttack(int damage, Vector3 at) {
        if (State != GameState.Playing) return;
        combo = (Time.time <= comboEndsAt) ? combo + 1 : 1;
        comboEndsAt = Time.time + ComboWindow;
        int pts = damage * 10 * combo;
        Score += pts;
        UpdateHud();
        Popup("+" + pts + ((combo >= 2) ? "   x" + combo : ""), at, GoldColor, 0.5f);
    }

    public void DamageBoss(int dmg) {
        if (State != GameState.Playing || boss == null || !boss.Alive) return;
        boss.TakeDamage(dmg);
    }

    public void OnBossHit() {
        SoundFx.Hit();
        Shake(0.16f, 0.09f);
    }

    public void BossDefeated() {
        if (State != GameState.Playing) return;
        CompleteLevel();
    }

    public void RegisterMiss() {
        if (State != GameState.Playing) return;
        Lives--;
        combo = 0;
        UpdateHud();
        SoundFx.Miss();
        Popup("MISSED!", new Vector3(CamCenter().x, CamCenter().y + CamHalfH() * 0.35f, 0f), RedColor, 0.55f);
        if (Lives <= 0) GameOverNow();
    }

    // =====================================================================
    //  Gameplay API — quiz mode (called by QuizOptionInput)
    // =====================================================================

    public void PlayerPickedQuizOption(int chosen) {
        if (State != GameState.Playing || CurrentLevel == null || CurrentLevel.quizData == null) return;

        ClearQuizOptions();
        bool correct = (chosen == currentCorrectIndex);

        if (correct) {
            combo = (Time.time <= comboEndsAt) ? combo + 1 : 1;
            comboEndsAt = Time.time + ComboWindow;
            int pts = 100 * combo;
            Score += pts;
            UpdateHud();
            SoundFx.Fire();
            Popup("+" + pts + ((combo >= 2) ? "   x" + combo : ""), CenterTop(), GoldColor, 0.5f);
            if (boss != null && boss.Alive) boss.TakeDamage(1); // HP bar = questions left
        } else {
            combo = 0;
            Lives--;
            UpdateHud();
            SoundFx.Reject();
            Popup("WRONG!", new Vector3(CamCenter().x, CamCenter().y + CamHalfH() * 0.35f, 0f), RedColor, 0.55f);
            if (Lives <= 0) { GameOverNow(); return; }
        }

        if (questionText != null) {
            questionText.text = (correct ? "CORRECT!  " : "WRONG!  ") + currentExplanation;
            questionText.color = correct ? GoldColor : RedColor;
            questionText.gameObject.SetActive(true);
        }
        if (quizCo != null) StopCoroutine(quizCo);
        quizCo = StartCoroutine(NextQuestionCo(correct ? 1.4f : 1.8f));
    }

    IEnumerator NextQuestionCo(float delay) {
        yield return new WaitForSeconds(delay);
        if (State != GameState.Playing) yield break;
        SpawnNextQuizQuestion();
    }

    void SpawnNextQuizQuestion() {
        if (CurrentLevel == null || CurrentLevel.quizData == null ||
            CurrentLevel.quizData.questions.Count == 0) {
            if (questionText != null) {
                questionText.text = "No questions configured for this level.";
                questionText.color = RedColor;
                questionText.gameObject.SetActive(true);
            }
            return;
        }

        var qs = CurrentLevel.quizData.questions;
        var q = qs[Random.Range(0, qs.Count)];
        currentCorrectIndex = q.correctIndex;
        currentExplanation = string.IsNullOrEmpty(q.explanation) ? "" : q.explanation;

        if (questionText != null) {
            questionText.text = q.question;
            questionText.color = WhiteColor;
            questionText.gameObject.SetActive(true);
        }
        SpawnQuizOptions(q.options);
    }

    void SpawnQuizOptions(List<string> options) {
        if (options == null || options.Count == 0) return;

        float spacing = 2.0f;
        float startX = -spacing * (options.Count - 1) * 0.5f;
        float y = CamCenter().y - 0.6f;

        for (int i = 0; i < options.Count; i++) {
            Vector3 pos = new Vector3(CamCenter().x + startX + i * spacing, y, 0f);
            TextMesh opt = MakeWorldText(options[i], pos, 0.45f, WhiteColor, 56, TextAnchor.MiddleCenter);
            opt.fontStyle = FontStyle.Bold;
            opt.gameObject.name = "quiz_option_" + i;
            BoxCollider2D col = opt.gameObject.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1.9f, 0.9f);
            opt.gameObject.AddComponent<QuizOptionInput>().Init(this, i);
            quizOptionObjects.Add(opt.gameObject);
        }
    }

    void ClearQuizOptions() {
        for (int i = 0; i < quizOptionObjects.Count; i++) {
            if (quizOptionObjects[i] != null) Destroy(quizOptionObjects[i]);
        }
        quizOptionObjects.Clear();
    }

    // =====================================================================
    //  Gameplay API — Algebra Solver (Level 4) & Trajectory Defense (Level 5)
    // =====================================================================

    public void GenerateNextAlgebraProblem() {
        int x = Random.Range(2, 9);
        int a = Random.Range(2, 6);
        int b = Random.Range(1, 10);
        int type = Random.Range(0, 5);

        string eq = "";
        string hint = "";
        switch (type) {
            case 0: // a*x + b = c
                eq = a + "x + " + b + " = " + (a * x + b);
                hint = "hint: subtract " + b + ", then divide by " + a;
                break;
            case 1: // a*x - b = c
                eq = a + "x − " + b + " = " + (a * x - b);
                hint = "hint: add " + b + ", then divide by " + a;
                break;
            case 2: // a*(x + b) = c
                eq = a + "(x + " + b + ") = " + (a * (x + b));
                hint = "hint: divide by " + a + ", then subtract " + b;
                break;
            case 3: // a*(x - b) = c
                b = Random.Range(1, x);
                eq = a + "(x − " + b + ") = " + (a * (x - b));
                hint = "hint: divide by " + a + ", then add " + b;
                break;
            case 4: // x^2 - b = c
                int bSq = Random.Range(2, 10);
                eq = "x² − " + bSq + " = " + (x * x - bSq);
                hint = "hint: add " + bSq + ", then take square root";
                break;
        }

        expectedSolution = x;
        activeChallengeText = "SOLVE FOR x:   " + eq + "\n" + hint;
        if (targetText != null) {
            targetText.text = activeChallengeText;
            targetText.color = GoldColor;
            targetText.characterSize = CharSize(0.35f);
            targetText.gameObject.SetActive(true);
        }

        // Guarantee the answer digit falls immediately — tinted gold so the
        // player can spot and catch it without waiting for random spawns.
        if (spawner != null) spawner.SpawnGuaranteedAnswerDigit(x, isCyan: false);
    }

    public void GenerateNextTrajectoryProblem() {
        if (activeMissile != null) {
            Destroy(activeMissile.gameObject);
            activeMissile = null;
        }

        int r1 = Random.Range(1, 4);
        int r2 = Random.Range(r1 + 2, r1 + 6); // positive root / landing point
        int type = Random.Range(0, 3);
        int target = r2;
        string prompt = "";

        switch (type) {
            case 0:
                // factored form – right root is the landing coordinate
                prompt = "⚠ PARABOLA ZERO:  y = −(x − " + r1 + ")(x − " + r2 + ")"
                       + "\nFire the LARGER root x to intercept the shell!\n"
                       + "(Roots: x = " + r1 + "  or  x = " + r2 + ")";
                target = r2;
                break;
            case 1:
                int h = Random.Range(3, 8);
                int k = Random.Range(9, 25);
                prompt = "⚠ DEFLECT AT APEX:  y = −(x − " + h + ")² + " + k
                       + "\nFire the APEX x coordinate to neutralize the trajectory!"
                       + "\n(Vertex is at x = " + h + ")";
                target = h;
                break;
            case 2:
                int B = r1 + r2;
                int C = r1 * r2;
                prompt = "⚠ RADAR LOCK:  y = −x² + " + B + "x − " + C
                       + "\nFire the OUTER (larger) root x!"
                       + "\n(Factor: −(x − " + r1 + ")(x − " + r2 + "))";
                target = r2;
                break;
        }

        expectedSolution = target;
        activeChallengeText = prompt;
        if (targetText != null) {
            targetText.text = activeChallengeText;
            targetText.color = new Color(0.35f, 0.9f, 1f);
            targetText.characterSize = CharSize(0.34f);
            targetText.gameObject.SetActive(true);
        }

        // Guarantee the intercept coordinate falls immediately — tinted cyan so
        // the player can spot and catch it without waiting for random spawns.
        if (spawner != null) spawner.SpawnGuaranteedAnswerDigit(target, isCyan: true);

        // Spawn real parabolic missile arching across screen!
        Vector3 bossPos = (boss != null) ? boss.transform.position : new Vector3(CamCenter().x, CamCenter().y + 3f, 0f);
        Vector3 targetLanding = new Vector3(CamCenter().x - 1.5f, CamCenter().y - CamHalfH() + 0.9f, 0f);

        GameObject missileGo = new GameObject("trajectory_shell");
        activeMissile = missileGo.AddComponent<TrajectoryMissile>();
        activeMissile.Launch(bossPos, targetLanding, apex: 4.8f, duration: 9.5f);
    }

    public void SubmitAlgebraOrTrajectoryAnswer(int value) {
        if (State != GameState.Playing) return;

        if (expectedSolution < 0) return;

        if (value == expectedSolution) {
            combo = (Time.time <= comboEndsAt) ? combo + 1 : 1;
            comboEndsAt = Time.time + ComboWindow;
            int pts = 250 * combo;
            Score += pts;
            UpdateHud();

            if (CurrentMode == Spawner.Mode.TrajectoryInterceptor) {
                if (activeMissile != null) {
                    activeMissile.Intercept(new Vector3(CamCenter().x, CamCenter().y - CamHalfH() + 0.8f, 0f));
                    activeMissile = null;
                }
                Popup("INTERCEPTED! (x = " + value + ")", CenterTop(), new Color(0.2f, 0.9f, 1f), 0.55f);
            } else {
                SoundFx.Laser();
                SoundFx.Explosion(false);
                Popup("CORRECT! x = " + value + "  (+" + pts + ")", CenterTop(), GoldColor, 0.55f);
            }

            if (boss != null && boss.Alive) boss.TakeDamage(1); // 1 problem cleared!

            expectedSolution = -1;
            StartCoroutine(NextProblemCo(1.2f));
        } else {
            combo = 0;
            Lives--;
            UpdateHud();
            SoundFx.Reject();
            Popup(value + " is WRONG! Target was " + expectedSolution, CenterTop(), RedColor, 0.55f);
            if (Lives <= 0) GameOverNow();
        }
    }

    IEnumerator NextProblemCo(float delay) {
        yield return new WaitForSeconds(delay);
        if (State != GameState.Playing) yield break;

        if (CurrentMode == Spawner.Mode.AlgebraSolver) GenerateNextAlgebraProblem();
        else if (CurrentMode == Spawner.Mode.TrajectoryInterceptor) GenerateNextTrajectoryProblem();
    }

    // Legacy wrappers for backwards compatibility
    public void SetArithmeticTarget(int target) { expectedSolution = target; if (targetText != null) targetText.text = "TARGET: " + target; }
    public void SubmitArithmeticAnswer(int val) { SubmitAlgebraOrTrajectoryAnswer(val); }

    // =====================================================================
    //  End-of-level flow
    // =====================================================================

    void CompleteLevel() {
        State = GameState.LevelComplete;
        Time.timeScale = 0f;
        if (activeMissile != null) {
            Destroy(activeMissile.gameObject);
            activeMissile = null;
        }
        ClearNumbers();
        ClearAttackTexts();
        ClearQuizOptions();
        if (fireButton != null) fireButton.SetActive(false);
        if (previewText != null) previewText.gameObject.SetActive(false);
        if (dockBg != null) dockBg.SetActive(false);
        if (questionText != null) questionText.gameObject.SetActive(false);
        if (targetText != null) targetText.gameObject.SetActive(false);
        ShowCenter("LEVEL COMPLETE!", GoldColor, 1.1f, "SCORE " + Score + "   ·   click to continue");
        LevelManager.RecordCleared(CurrentLevelIndex, Score);
        SoundFx.Win();
    }

    void GameOverNow() {
        State = GameState.GameOver;
        Time.timeScale = 0f;
        if (activeMissile != null) {
            Destroy(activeMissile.gameObject);
            activeMissile = null;
        }
        ClearNumbers();
        ClearAttackTexts();
        ClearQuizOptions();
        GameManager gm = FindAnyObjectByType<GameManager>();
        if (gm != null) gm.ResetSelection();
        if (fireButton != null) fireButton.SetActive(false);
        if (previewText != null) previewText.gameObject.SetActive(false);
        if (dockBg != null) dockBg.SetActive(false);
        if (questionText != null) questionText.gameObject.SetActive(false);
        if (targetText != null) targetText.gameObject.SetActive(false);
        ShowCenter("GAME OVER", RedColor, 1.1f, "SCORE " + Score + "   ·   CLICK TO RETRY");
        clickLockUntil = Time.unscaledTime + 1.5f;
        // Clear any frozen mid-air popups so they don't overlap this text.
        TextMesh[] allTexts = FindObjectsByType<TextMesh>();
        for (int i = 0; i < allTexts.Length; i++) {
            if (allTexts[i].gameObject.name == "popup_text") Destroy(allTexts[i].gameObject);
        }
        StartCoroutine(LoseSfxCo());
    }

    IEnumerator LoseSfxCo() {
        yield return new WaitForSecondsRealtime(0.15f);
        SoundFx.Lose();
    }

    void ClearNumbers() {
        GameObject[] nums = GameObject.FindGameObjectsWithTag("number");
        for (int i = 0; i < nums.Length; i++) Destroy(nums[i]);
    }

    void ClearAttackTexts() {
        for (int i = 0; i < attackTexts.Count; i++) {
            if (attackTexts[i] != null) Destroy(attackTexts[i]);
        }
        attackTexts.Clear();
    }

    // =====================================================================
    //  UI factories (used by GameManager too)
    // =====================================================================

    public TextMesh CreateExpressionPreview() {
        if (dockBg == null) {
            dockBg = new GameObject("dock_bg");
            SpriteRenderer sr = dockBg.AddComponent<SpriteRenderer>();
            sr.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
            sr.color = new Color(0.04f, 0.06f, 0.12f, 0.85f);
            sr.sortingOrder = 40;
            dockBg.transform.position = new Vector3(CamCenter().x, CamCenter().y - CamHalfH() + 0.65f, 0f);
            dockBg.transform.localScale = new Vector3(CamHalfW() * 2.1f, 1.35f, 1f);
            dockBg.SetActive(false);
        }

        previewText = MakeWorldText("",
            new Vector3(CamCenter().x - 0.7f, CamCenter().y - CamHalfH() + 0.65f, 0f),
            0.44f, WhiteColor, 45, TextAnchor.MiddleCenter);
        previewText.fontStyle = FontStyle.Bold;
        return previewText;
    }

    public GameObject CreateFireButton() {
        Vector3 p = new Vector3(CamCenter().x + CamHalfW() - 1.4f, CamCenter().y - CamHalfH() + 0.8f, 0f);
        TextMesh tm = MakeWorldText("FIRE", p, 0.7f, new Color(0.35f, 1f, 0.45f), 52, TextAnchor.MiddleCenter);
        tm.fontStyle = FontStyle.Bold;
        fireButton = tm.gameObject;
        fireButton.SetActive(false);
        return fireButton;
    }

    public bool IsFireButtonClicked(Vector2 worldPoint) {
        return fireButton != null && fireButton.activeSelf &&
               Vector2.Distance(fireButton.transform.position, worldPoint) < 1.0f;
    }

    public float BossTargetY() {
        if (boss == null) return 0.8f;
        Renderer r = boss.GetComponent<Renderer>();
        if (r == null) r = boss.GetComponentInChildren<Renderer>();
        float top = (r != null) ? r.bounds.max.y : boss.transform.position.y + 1f;
        return Mathf.Min(top + 0.35f, 5.2f);
    }

    public void TrackAttackText(GameObject go) { attackTexts.Add(go); }

    public void Popup(string txt, Vector3 pos, Color color, float height) {
        StartCoroutine(PopupCo(txt, pos, color, height));
    }

    IEnumerator PopupCo(string txt, Vector3 pos, Color color, float height) {
        TextMesh tm = MakeWorldText(txt, pos, height, color, 55, TextAnchor.MiddleCenter);
        tm.gameObject.name = "popup_text"; // lets GameOverNow clear frozen popups
        float t = 0f;
        while (t < 0.9f) {
            t += Time.deltaTime;
            if (tm == null) yield break;
            tm.transform.position = pos + new Vector3(0f, t * 0.8f, 0f);
            Color c = color;
            c.a = Mathf.Clamp01(1.6f - t * 1.8f);
            tm.color = c;
            yield return null;
        }
        if (tm != null) Destroy(tm.gameObject);
    }

    GameObject CreateFallbackBoss() {
        Texture2D tex = MakeBossTexture();
        GameObject go = new GameObject("boss");
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 32f);
        sr.sortingOrder = 4;
        go.transform.position = new Vector3(CamCenter().x, CamCenter().y - CamHalfH() + 2.2f, 0f);
        return go;
    }

    Texture2D MakeBossTexture() {
        int size = 96;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color clear = new Color(0f, 0f, 0f, 0f);
        Color body = new Color(0.55f, 0.32f, 0.82f);
        Color edge = new Color(0.38f, 0.18f, 0.60f);
        Color eyeWhite = Color.white;
        Color eyeDark = new Color(0.10f, 0.05f, 0.20f);
        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                float dx = (x - 47.5f) / 47.5f;
                float dy = (y - 47.5f) / 47.5f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                Color c = clear;
                if (r < 0.90f) c = body;
                if (r >= 0.82f && r < 0.98f) c = edge;
                float e1 = Mathf.Sqrt((dx + 0.30f) * (dx + 0.30f) + (dy - 0.12f) * (dy - 0.12f));
                float e2 = Mathf.Sqrt((dx - 0.30f) * (dx - 0.30f) + (dy - 0.12f) * (dy - 0.12f));
                if (e1 < 0.17f || e2 < 0.17f) c = eyeWhite;
                if (e1 < 0.07f || e2 < 0.07f) c = eyeDark;
                if (Mathf.Abs(dy + 0.35f) < 0.06f && Mathf.Abs(dx) < 0.35f && r < 0.9f) c = eyeDark;
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        return tex;
    }

    // =====================================================================
    //  Text helpers
    // =====================================================================

    /// <summary>
    /// Creates a TextMesh whose on-screen height is ~`height` world units.
    /// The ONLY way UI text is created, so sizes stay consistent.
    /// </summary>
    public TextMesh MakeWorldText(string txt, Vector3 pos, float height, Color color, int order, TextAnchor anchor) {
        GameObject go = new GameObject("ui_text");
        go.transform.position = pos;
        TextMesh tm = go.AddComponent<TextMesh>();
        tm.font = OperatorPickup.BuiltinFont();
        tm.text = txt;
        tm.fontSize = (int)FontPx;
        tm.characterSize = CharSize(height);
        tm.anchor = anchor;
        tm.alignment = TextAlignment.Center;
        tm.color = color;
        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = tm.font.material;
        mr.sortingOrder = order;
        return tm;
    }

    float CharSize(float height) { return height * UiScale() / (FontPx * 0.1f); }

    public void Shake(float dur, float mag) {
        if (cam == null) return;
        if (shakeCo != null) StopCoroutine(shakeCo);
        shakeCo = StartCoroutine(ShakeCo(dur, mag));
    }

    IEnumerator ShakeCo(float dur, float mag) {
        float t = 0f;
        while (t < dur) {
            t += Time.unscaledDeltaTime;
            Vector2 r = Random.insideUnitCircle * mag * (1f - t / dur);
            cam.transform.position = camHome + new Vector3(r.x, r.y, 0f);
            yield return null;
        }
        cam.transform.position = camHome;
        shakeCo = null;
    }

    Vector3 CenterTop() {
        return new Vector3(CamCenter().x, CamCenter().y + CamHalfH() * 0.45f, 0f);
    }

    Vector2 CamCenter() { return (cam != null) ? (Vector2)cam.transform.position : Vector2.zero; }
    float CamHalfH() { return (cam != null) ? cam.orthographicSize : 5.5f; }
    float CamHalfW() { return (cam != null) ? cam.orthographicSize * cam.aspect : 5.5f * 1.777f; }
    float UiScale() { return CamHalfH() / 5.5f; }
}

/// <summary>
/// Click handler for one multiple-choice option in quiz mode.
/// Lives on the option's TextMesh object (with a BoxCollider2D).
/// </summary>
public class QuizOptionInput : MonoBehaviour {
    GameDirector director;
    int optionIndex;

    public void Init(GameDirector owner, int index) {
        director = owner;
        optionIndex = index;
    }

    void OnMouseDown() {
        if (director != null) director.PlayerPickedQuizOption(optionIndex);
    }
}
