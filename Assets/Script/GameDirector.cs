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
///   1. BODMAS                        — boss combat: build expressions, fire them
///   2. MATH QUIZ                     — multiple-choice questions, boss HP = questions
///   3. HIGH-LEVEL MATH QUIZ          — harder questions (powers, brackets, negatives)
///   4. COMPUTER ARITHMETIC BASICS    — build expressions that hit the boss's target digit
///   5. COMPUTER ARITHMETIC QUIZ      — binary / hex / powers-of-two questions
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

    const int StartLives = 5;
    const float ComboWindow = 6f;
    const float FontPx = 64f;

    int combo;
    float comboEndsAt, clickLockUntil;
    int arithmeticTarget = -1;      // computer-arithmetic mode: the digit to reach
    int currentCorrectIndex = -1;   // quiz mode: correct option of the live question
    string currentExplanation = "";
    bool paused;

    Camera cam;
    Vector3 camHome;
    Spawner spawner;
    Boss boss;
    GameObject pendingBossGo;   // scene boss, hidden while the level-select screen is up
    float baseVelocity = 2.5f;

    TextMesh scoreText, livesText, levelText, modeText, targetText;
    TextMesh centerText, subText, previewText, questionText;
    GameObject fireButton;
    readonly List<GameObject> attackTexts = new List<GameObject>();
    readonly List<GameObject> quizOptionObjects = new List<GameObject>();
    Coroutine shakeCo, quizCo;

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

        if (combo > 0 && Time.time > comboEndsAt) combo = 0;
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
        arithmeticTarget = -1;
        currentCorrectIndex = -1;
        paused = false;
        Time.timeScale = 1f;

        spawner = FindAnyObjectByType<Spawner>();
        if (spawner != null) {
            spawner.CurrentMode = CurrentMode;
            baseVelocity = Mathf.Max(spawner.velocity, 1f);
        }

        // In quiz / arithmetic modes the boss HP is the number of problems to solve,
        // so the HP bar doubles as level progress. Combat uses the level's own HP.
        bool isProblemMode = CurrentMode != Spawner.Mode.Standard;
        int hp = isProblemMode ? Mathf.Max(1, data.questionsToClear)
                               : Mathf.Max(1, data.bossHp);
        SetupBoss(hp);
        BuildHud();
        HideCenter();

        bool combatHud = (CurrentMode == Spawner.Mode.Standard ||
                          CurrentMode == Spawner.Mode.ComputerArithmeticBasics);
        if (fireButton != null) fireButton.SetActive(combatHud);
        if (previewText != null) previewText.gameObject.SetActive(combatHud);
        if (questionText != null) questionText.gameObject.SetActive(!combatHud);
        if (targetText != null) {
            targetText.gameObject.SetActive(CurrentMode == Spawner.Mode.ComputerArithmeticBasics);
            targetText.text = "TARGET: ?";
        }

        State = GameState.Playing;
        UpdateHud();
        SoundFx.Go();
        Popup(data.title, CenterTop(), GoldColor, 0.6f);

        if (CurrentMode == Spawner.Mode.Quiz ||
            CurrentMode == Spawner.Mode.AdvancedQuiz ||
            CurrentMode == Spawner.Mode.ComputerArithmeticQuiz) {
            SpawnNextQuizQuestion();
        }
    }

    void StartLevelByIndex(int index) {
        LevelData[] levels = LevelCatalog.GetLevels();
        if (index < 0 || index >= levels.Length) return;
        StartLevel(levels[index], index);
    }

    Spawner.Mode MapMode(LevelData.GameMode m) {
        switch (m) {
            case LevelData.GameMode.QUIZ:                        return Spawner.Mode.Quiz;
            case LevelData.GameMode.ADVANCED_QUIZ:               return Spawner.Mode.AdvancedQuiz;
            case LevelData.GameMode.COMPUTER_ARITHMETIC_BASICS:  return Spawner.Mode.ComputerArithmeticBasics;
            case LevelData.GameMode.COMPUTER_ARITHMETIC_QUIZ:    return Spawner.Mode.ComputerArithmeticQuiz;
            default:                                             return Spawner.Mode.Standard;
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
        ClearNumbers();
        ClearAttackTexts();
        ClearQuizOptions();
        if (fireButton != null) fireButton.SetActive(false);
        if (previewText != null) previewText.gameObject.SetActive(false);
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
            case Spawner.Mode.ComputerArithmeticBasics:  return "COMPUTER ARITHMETIC BASICS";
            case Spawner.Mode.ComputerArithmeticQuiz:    return "COMPUTER ARITHMETIC QUIZ";
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
    }

    // =====================================================================
    //  Gameplay API — combat (called by GameManager / Boss / Number)
    // =====================================================================

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
    //  Gameplay API — computer arithmetic basics (called by GameManager/Spawner)
    // =====================================================================

    /// <summary>The spawner tells us which digit the newest "?" artifact hides.</summary>
    public void SetArithmeticTarget(int target) {
        arithmeticTarget = target;
        if (targetText != null) targetText.text = "TARGET: " + target;
    }

    /// <summary>The player fired an expression; its value must equal the target digit.</summary>
    public void SubmitArithmeticAnswer(int value) {
        if (State != GameState.Playing) return;

        if (arithmeticTarget < 0) {
            Popup("Catch a ? target first!", CenterTop(), GoldColor, 0.45f);
            SoundFx.Reject();
            return;
        }

        if (value == arithmeticTarget) {
            Score += 150;
            UpdateHud();
            SoundFx.Fire();
            Popup("+150", CenterTop(), GoldColor, 0.5f);
            arithmeticTarget = -1;
            if (targetText != null) targetText.text = "TARGET: ?";
            if (boss != null && boss.Alive) boss.TakeDamage(1); // HP bar = problems left
        } else {
            SoundFx.Reject();
            Popup(value + "  is not " + arithmeticTarget, CenterTop(), RedColor, 0.45f);
        }
    }

    // =====================================================================
    //  End-of-level flow
    // =====================================================================

    void CompleteLevel() {
        State = GameState.LevelComplete;
        Time.timeScale = 0f;
        ClearNumbers();
        ClearAttackTexts();
        ClearQuizOptions();
        if (fireButton != null) fireButton.SetActive(false);
        if (previewText != null) previewText.gameObject.SetActive(false);
        if (questionText != null) questionText.gameObject.SetActive(false);
        if (targetText != null) targetText.gameObject.SetActive(false);
        ShowCenter("LEVEL COMPLETE!", GoldColor, 1.1f, "SCORE " + Score + "   ·   click to continue");
        LevelManager.RecordCleared(CurrentLevelIndex, Score);
        SoundFx.Win();
    }

    void GameOverNow() {
        State = GameState.GameOver;
        Time.timeScale = 0f;
        ClearNumbers();
        ClearAttackTexts();
        ClearQuizOptions();
        GameManager gm = FindAnyObjectByType<GameManager>();
        if (gm != null) gm.ResetSelection();
        if (fireButton != null) fireButton.SetActive(false);
        if (previewText != null) previewText.gameObject.SetActive(false);
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
        previewText = MakeWorldText("",
            new Vector3(CamCenter().x, CamCenter().y - CamHalfH() + 0.35f, 0f),
            0.42f, WhiteColor, 45, TextAnchor.LowerCenter);
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
