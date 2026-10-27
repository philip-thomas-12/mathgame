using UnityEngine;

/// <summary>
/// Spawns falling items: alien numbers (0-9), operator artifacts
/// (+ - × ÷ ^) and occasionally brackets. GameDirector sets the Mode per
/// level; quiz modes disable auto-spawning (the director shows questions and
/// answer options itself).
///
/// Level 4 (AlgebraSolver) and Level 5 (TrajectoryInterceptor) also call
/// SpawnGuaranteedAnswerDigit() immediately after generating each problem, so
/// the correct digit is always visible on screen from the moment the question
/// appears — highlighted in gold/cyan so the player can spot and catch it fast.
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

    // -----------------------------------------------------------------------
    //  Guaranteed answer digit (Level 4 & 5)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Immediately spawns the digit <paramref name="digit"/> at a visible
    /// top-center position so the player can catch it right away.
    ///
    /// Uses a gold tint (algebra) or cyan tint (trajectory) so it stands out
    /// from ordinary grey alien numbers.  The token still has a tag "number"
    /// so GameManager can add it to the chain as normal.
    ///
    /// The digit is spawned slightly off-center to avoid overlapping with
    /// the equation text and to give the player a clear target to aim at.
    /// </summary>
    public void SpawnGuaranteedAnswerDigit(int digit, bool isCyan = false) {
        if (digit < 0 || digit > 9) return;   // only single-digit answers supported
        if (numbers == null || numbers.Length <= digit || numbers[digit] == null) {
            // Prefab array not wired — create a procedural fallback
            SpawnProceduralAnswerDigit(digit, isCyan);
            return;
        }

        // Spawn from a fixed, visible position near the top-centre.
        // Slight horizontal offset avoids covering the equation text.
        Vector2 pos = new Vector2(Random.Range(-0.5f, 0.5f), 8.5f);

        GameObject go = Instantiate(numbers[digit], pos, Quaternion.identity);
        go.name = digit.ToString();

        Number numComp = go.GetComponent<Number>();
        if (numComp != null) {
            numComp.value = digit;
            numComp.IsAnswerToken = true;   // won't cost a life if missed
        }

        // Highlight tint so the player knows which digit is the answer
        SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
        if (sr == null) sr = go.GetComponentInChildren<SpriteRenderer>();
        if (sr != null) {
            sr.color = isCyan ? new Color(0.3f, 0.95f, 1f)   // Level 5: cyan
                              : new Color(1f,  0.85f, 0.25f); // Level 4: gold
        }

        // Slightly slower fall so the player has time to react
        Rigidbody2D rb = go.GetComponent<Rigidbody2D>();
        if (rb != null) rb.linearVelocity = new Vector2(0f, -Mathf.Max(velocity * 0.70f, 1.4f));

        // Small sparkle VFX at spawn point
        if (CombatVfx.Instance != null) {
            CombatVfx.Instance.SpawnPickupSparkle(
                (Vector3)(pos) + Vector3.down * 0.5f,
                isCyan ? new Color(0.3f, 0.95f, 1f) : new Color(1f, 0.85f, 0.25f));
        }
    }

    /// <summary>Fallback when the numbers prefab array is not set up.
    /// Creates a procedural text token identical to how OperatorPickup works.
    /// </summary>
    void SpawnProceduralAnswerDigit(int digit, bool isCyan) {
        Vector2 pos = new Vector2(Random.Range(-0.5f, 0.5f), 8.5f);
        GameObject go = new GameObject(digit.ToString());
        go.transform.position = pos;
        go.tag = "number";

        // Reuse OperatorPickup rendering pipeline for a styled text glyph
        OperatorPickup op = go.AddComponent<OperatorPickup>();
        op.SetupAnswerDigit(digit, isCyan);

        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.linearVelocity = new Vector2(0f, -Mathf.Max(velocity * 0.70f, 1.4f));
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
