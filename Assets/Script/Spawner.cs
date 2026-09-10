using UnityEngine;

/// <summary>
/// Spawns falling items: alien numbers (0-9), operator artifacts
/// (+ - × ÷ ^) and occasionally brackets. GameDirector sets the Mode per
/// level; quiz modes disable auto-spawning (the director shows questions and
/// answer options itself), and computer-arithmetic basics adds golden "?"
/// target artifacts via SpawnDigitTarget.
/// </summary>
public class Spawner : MonoBehaviour {

    public float velocity = 2.5f;
    public GameObject[] numbers = new GameObject[10];

    float baseInterval = 1.5f;
    float baseVelocity = 2.5f;
    float bracketToggle;

    /// <summary>Per-level spawning behaviour. Set by GameDirector.StartLevel.</summary>
    public enum Mode {
        Standard,
        Quiz,
        AdvancedQuiz,
        ComputerArithmeticBasics,
        ComputerArithmeticQuiz
    }

    public Mode CurrentMode { get; set; }

    void Start() {
        baseVelocity = (velocity > 0.05f) ? velocity : 2.5f;
        velocity = baseVelocity;
        RestartAutoSpawn();
    }

    void RestartAutoSpawn() {
        CancelInvoke("GenerateNumber");
        InvokeRepeating("GenerateNumber", 0.2f, baseInterval);
    }

    void GenerateNumber() {
        if (GameDirector.Instance == null || !GameDirector.Instance.SpawningEnabled) return;

        // Quiz modes: no falling items — the director shows the question and
        // answer options itself.
        if (CurrentMode == Mode.Quiz || CurrentMode == Mode.AdvancedQuiz ||
            CurrentMode == Mode.ComputerArithmeticQuiz) {
            return;
        }

        Vector2 position = new Vector2(Random.Range(-2.0f, 2.0f), 11f);
        float r = Random.value;

        if (CurrentMode == Mode.ComputerArithmeticBasics) {
            // Numbers to build answers from, plus "?" target artifacts.
            if (r < 0.55f) SpawnAlienNumber(position);
            else if (r < 0.85f) SpawnDigitTarget(position);
            else SpawnOperator(position);
        } else {
            if (r < 0.60f) SpawnAlienNumber(position);
            else if (r < 0.97f) SpawnOperator(position);
            else SpawnBracket(position);
        }
    }

    void SpawnAlienNumber(Vector2 position) {
        int no = Random.Range(0, 10);
        GameObject x = Instantiate(this.numbers[no], position, Quaternion.identity);
        x.name = no.ToString();

        // IMPORTANT: set the digit explicitly. Do not rely on the prefab's
        // serialized field — if it resets (upgrade, new prefab, duplicate),
        // every alien would silently evaluate as 0.
        Number numComp = x.GetComponent<Number>();
        if (numComp != null) numComp.value = no;

        Rigidbody2D velo = x.GetComponent<Rigidbody2D>();
        if (velo != null) velo.linearVelocity = new Vector2(0, -this.velocity);
    }

    void SpawnDigitTarget(Vector2 position) {
        // A golden "?" artifact: the digit it hides is the answer the player
        // must reach with their expression. Not tagged "number", so the
        // GameManager ignores it while building chains.
        int target = Random.Range(0, 10);
        GameObject go = new GameObject("digitTarget_" + target);
        go.transform.position = position;
        go.AddComponent<OperatorPickup>().SetupDigitTarget(target);

        if (GameDirector.Instance != null) {
            GameDirector.Instance.SetArithmeticTarget(target);
        }

        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.linearVelocity = new Vector2(0, -this.velocity);
    }

    void SpawnOperator(Vector2 position) {
        char symbol;
        float r = Random.value;
        if (r < 0.24f) symbol = '+';
        else if (r < 0.48f) symbol = '-';
        else if (r < 0.72f) symbol = '×';
        else if (r < 0.94f) symbol = '÷';
        else symbol = '^';

        CreateArtifact(symbol, position);
    }

    void SpawnBracket(Vector2 position) {
        bracketToggle = 1f - bracketToggle;
        char symbol = (bracketToggle > 0f) ? '(' : ')';
        CreateArtifact(symbol, position);
    }

    void CreateArtifact(char symbol, Vector2 position) {
        GameObject go = new GameObject("artifact_" + symbol);
        go.transform.position = position;
        go.tag = "number"; // reuse the existing clickable tag
        go.AddComponent<OperatorPickup>().Setup(symbol);

        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.linearVelocity = new Vector2(0, -this.velocity);
    }

    public void SetDifficulty(float interval, float fallVelocity) {
        velocity = fallVelocity;
        if (CurrentMode == Mode.Quiz || CurrentMode == Mode.AdvancedQuiz ||
            CurrentMode == Mode.ComputerArithmeticQuiz) {
            return; // no auto-spawn in quiz modes
        }
        CancelInvoke("GenerateNumber");
        InvokeRepeating("GenerateNumber", 0.2f, interval);
    }

    public void ResetDifficulty() {
        velocity = baseVelocity;
        SetDifficulty(baseInterval, baseVelocity);
    }
}
