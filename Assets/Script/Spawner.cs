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

    public enum Mode {
        Standard,
        Quiz,
        AdvancedQuiz,
        AlgebraSolver,
        TrajectoryInterceptor,
        ComputerArithmeticBasics = AlgebraSolver,
        ComputerArithmeticQuiz = TrajectoryInterceptor
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

    float speedMultiplier = 1.0f;

    public void SetSpeedMultiplier(float mult) {
        speedMultiplier = mult;
        Rigidbody2D[] bodies = FindObjectsByType<Rigidbody2D>(FindObjectsSortMode.None);
        for (int i = 0; i < bodies.Length; i++) {
            if (bodies[i] != null && bodies[i].linearVelocity.y < -0.1f) {
                bodies[i].linearVelocity = new Vector2(0f, -this.velocity * speedMultiplier);
            }
        }
    }

    void GenerateNumber() {
        if (GameDirector.Instance == null || !GameDirector.Instance.SpawningEnabled) return;

        // Quiz modes (Level 2 & 3): no falling items
        if (CurrentMode == Mode.Quiz || CurrentMode == Mode.AdvancedQuiz) {
            return;
        }

        Vector2 position = new Vector2(Random.Range(-2.0f, 2.0f), 11f);
        float r = Random.value;

        // Level 1 (BODMAS), Level 4 (Algebra), and Level 5 (Trajectory) all use falling tokens:
        if (r < 0.52f) SpawnAlienNumber(position);
        else if (r < 0.82f) SpawnOperator(position);
        else if (r < 0.90f) SpawnBracket(position);
        else SpawnPowerup(position);
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
        if (velo != null) velo.linearVelocity = new Vector2(0, -this.velocity * speedMultiplier);
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
        rb.linearVelocity = new Vector2(0, -this.velocity * speedMultiplier);
    }

    void SpawnPowerup(Vector2 position) {
        PowerupPickup.Type[] types = { PowerupPickup.Type.Freeze, PowerupPickup.Type.Bomb, PowerupPickup.Type.DoubleDamage, PowerupPickup.Type.ExtraLife };
        PowerupPickup.Type chosen = types[Random.Range(0, types.Length)];
        GameObject go = new GameObject("powerup");
        go.transform.position = position;
        go.AddComponent<PowerupPickup>().Setup(chosen);

        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.linearVelocity = new Vector2(0, -this.velocity * speedMultiplier);
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
        rb.linearVelocity = new Vector2(0, -this.velocity * speedMultiplier);
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
