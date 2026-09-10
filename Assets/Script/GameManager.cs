using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Player input: clicking falling numbers and operator artifacts builds an
/// expression chain ("3 × (8 - 2)"). Pressing SPACE or clicking FIRE
/// evaluates the chain with BODMAS (MathExpr) and launches the result.
/// Clicking a chain token again removes it.
///
/// Per mode:
///   - Standard (BODMAS): value = damage against the boss (waves).
///   - ComputerArithmeticBasics: value must equal the current "?" target
///     digit (GameDirector.SubmitArithmeticAnswer).
///   - Quiz modes: input is handled by QuizOptionInput instead; chains are
///     disabled.
/// </summary>
public class GameManager : MonoBehaviour {

    private RuntimePlatform platform = Application.platform;
    public float velocity = 1.0f;
    public GameObject attackText;

    class ChainToken {
        public GameObject go;
        public bool isOperator;
        public int number;
        public char op;
    }

    readonly List<ChainToken> chain = new List<ChainToken>();
    TextMesh preview;

    bool ChainsEnabled {
        get {
            if (GameDirector.Instance == null) return true;
            Spawner.Mode m = GameDirector.Instance.CurrentMode;
            return m == Spawner.Mode.Standard || m == Spawner.Mode.ComputerArithmeticBasics;
        }
    }

    void Start() {
        if (GameDirector.Instance != null) {
            preview = GameDirector.Instance.CreateExpressionPreview();
            GameDirector.Instance.CreateFireButton();
        }
        // Hidden until a level starts — GameDirector.StartLevel re-activates
        // them for modes that use expression chains.
        if (preview != null) preview.gameObject.SetActive(false);
        RefreshPreview();
    }

    void Update() {
        if (GameDirector.Instance == null || !GameDirector.Instance.InputEnabled) return;

        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) {
            Fire();
            return;
        }

        if (platform == RuntimePlatform.Android) {
            if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began) {
                HandlePointer(Camera.main.ScreenToWorldPoint(Input.GetTouch(0).position));
            }
        } else if (platform == RuntimePlatform.WindowsEditor || platform == RuntimePlatform.OSXEditor
                || platform == RuntimePlatform.WindowsPlayer || platform == RuntimePlatform.OSXPlayer
                || platform == RuntimePlatform.LinuxEditor || platform == RuntimePlatform.LinuxPlayer) {
            if (Input.GetMouseButtonDown(0)) {
                HandlePointer(Camera.main.ScreenToWorldPoint(Input.mousePosition));
            }
        }
    }

    void HandlePointer(Vector3 worldPos) {
        Vector2 point = new Vector2(worldPos.x, worldPos.y);

        if (GameDirector.Instance.IsFireButtonClicked(point)) {
            Fire();
            return;
        }

        if (!ChainsEnabled) return; // quiz modes: options handle their own clicks

        Collider2D hit = Physics2D.OverlapPoint(point);
        if (hit == null) return;

        GameObject go = hit.transform.gameObject;
        if (go == null || go.tag != "number") return;

        // Already in the chain? -> remove it again.
        for (int i = 0; i < chain.Count; i++) {
            if (chain[i].go == go) {
                RemoveTokenAt(i);
                return;
            }
        }

        TryAddToken(go);
    }

    // --- chain building ---------------------------------------------------

    void TryAddToken(GameObject go) {
        if (chain.Count >= 8) { Reject("Expression too long!"); return; }

        ChainToken token = new ChainToken();
        token.go = go;

        OperatorPickup op = go.GetComponent<OperatorPickup>();
        if (op != null) {
            token.isOperator = true;
            token.op = op.Symbol;
            char last = LastKind();
            if (token.op == '(') {
                if (last == 'n' || last == ')') { Reject("Operator needed before ( !"); return; }
            } else if (token.op == ')') {
                if (CountOpenBrackets() <= 0) { Reject("No ( to close!"); return; }
                if (last == '(' || last == 'o') { Reject("Empty brackets!"); return; }
            } else {
                if (last == 'e' || last == 'o' || last == '(') { Reject("Number needed first!"); return; }
            }
        } else {
            token.isOperator = false;
            token.number = NumberValueOf(go);
            char last = LastKind();
            if (last == 'n' || last == ')') { Reject("Operator needed between numbers!"); return; }
        }

        chain.Add(token);
        Rigidbody2D rb = go.GetComponent<Rigidbody2D>();
        if (rb != null) rb.linearVelocity = Vector2.zero; // freeze while selected
        SoundFx.Select();
        RefreshPreview();
    }

    void RemoveTokenAt(int index) {
        ChainToken t = chain[index];
        chain.RemoveAt(index);
        if (t.go != null) {
            Rigidbody2D rb = t.go.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = new Vector2(0f, -velocity);
        }
        SoundFx.Select();
        RefreshPreview();
    }

    char LastKind() {
        if (chain.Count == 0) return 'e';
        ChainToken t = chain[chain.Count - 1];
        if (!t.isOperator) return 'n';
        if (t.op == '(') return '(';
        if (t.op == ')') return ')';
        return 'o';
    }

    int CountOpenBrackets() {
        int open = 0;
        for (int i = 0; i < chain.Count; i++) {
            if (chain[i].isOperator) {
                if (chain[i].op == '(') open++;
                else if (chain[i].op == ')') open--;
            }
        }
        return open;
    }

    int NumberValueOf(GameObject go) {
        // The spawner names every alien after its digit ("0".."9"), so the
        // object name is the authoritative value — survives any prefab
        // field reset. Fall back to the component for safety.
        int parsed;
        if (int.TryParse(go.name, out parsed)) return parsed;
        Number numComp = go.GetComponent<Number>();
        if (numComp != null) return numComp.value;
        return 0;
    }

    // --- firing -------------------------------------------------------------

    void Fire() {
        if (!ChainsEnabled) return;
        if (chain.Count == 0) { Reject("Build an expression first!"); return; }

        int open = CountOpenBrackets();

        // Trim trailing binary operators (never a closing bracket).
        while (chain.Count > 0) {
            ChainToken last = chain[chain.Count - 1];
            if (last.isOperator && last.op != ')') {
                chain.RemoveAt(chain.Count - 1);
            } else break;
        }
        if (chain.Count == 0) { Reject("Add at least one number!"); RefreshPreview(); return; }

        bool hasNumber = false;
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < chain.Count; i++) {
            ChainToken t = chain[i];
            if (!t.isOperator) hasNumber = true;
            if (i > 0) sb.Append(" ");
            if (t.isOperator) {
                if (t.op == '*') sb.Append("×");
                else if (t.op == '/') sb.Append("÷");
                else sb.Append(t.op);
            } else {
                sb.Append(t.number);
            }
        }
        for (int i = 0; i < open; i++) sb.Append(" )");

        if (!hasNumber) { Reject("Add at least one number!"); RefreshPreview(); return; }

        double value;
        if (!MathExpr.TryEvaluate(sb.ToString(), out value)) {
            Reject("Can't divide by 0!");
            return;
        }

        int result = Mathf.Clamp(Mathf.RoundToInt((float)value), 0, 99999);
        Vector3 mid = ChainCenter();

        for (int i = 0; i < chain.Count; i++) {
            if (chain[i].go != null) {
                chain[i].go.SendMessage("operated", SendMessageOptions.DontRequireReceiver);
            }
        }
        chain.Clear();

        if (GameDirector.Instance != null) {
            if (GameDirector.Instance.CurrentMode == Spawner.Mode.ComputerArithmeticBasics) {
                // Computer arithmetic basics: the value must equal the "?"
                // target digit — no boss damage, just solve-or-retry.
                GameDirector.Instance.SubmitArithmeticAnswer(result);
            } else {
                GameDirector.Instance.RegisterAttack(result, mid);
                StartCoroutine(instantiateAttackText(result, 0.35f));
            }
        }
        RefreshPreview();
    }

    Vector3 ChainCenter() {
        if (chain.Count == 0) return Vector3.zero;
        Vector3 sum = Vector3.zero;
        for (int i = 0; i < chain.Count; i++) {
            if (chain[i].go != null) sum += chain[i].go.transform.position;
        }
        return sum / chain.Count;
    }

    void Reject(string reason) {
        SoundFx.Reject();
        if (GameDirector.Instance == null || Camera.main == null) return;
        Vector3 pos = new Vector3(Camera.main.transform.position.x, Camera.main.transform.position.y - 1.0f, 0f);
        GameDirector.Instance.Popup(reason, pos, new Color(1f, 0.4f, 0.35f), 0.85f);
    }

    // --- attack animation ----------------------------------------------------

    IEnumerator instantiateAttackText(int attack, float timer) {
        yield return new WaitForSeconds(timer);

        if (attackText == null) {
            if (GameDirector.Instance != null) GameDirector.Instance.DamageBoss(attack);
            yield break;
        }

        Vector2 position = new Vector2(0, 6);
        GameObject x = Instantiate(attackText, position, Quaternion.identity);
        x.name = attack.ToString();
        TextMesh tm = x.GetComponent<TextMesh>();
        if (tm != null) tm.text = attack.ToString();
        if (GameDirector.Instance != null) GameDirector.Instance.TrackAttackText(x);
        StartCoroutine(launchAtack(x, attack));
    }

    IEnumerator launchAtack(GameObject attackObj, int attack) {
        Rigidbody2D velo = attackObj.GetComponent<Rigidbody2D>();
        if (velo != null) velo.linearVelocity = new Vector2(0, -9f);

        float deadline = Time.time + 3f;
        while (attackObj != null) {
            float targetY = 0.8f;
            if (GameDirector.Instance != null) targetY = GameDirector.Instance.BossTargetY();

            if (attackObj.transform.position.y <= targetY) {
                if (GameDirector.Instance != null) GameDirector.Instance.DamageBoss(attack);
                Destroy(attackObj);
                yield break;
            }
            if (Time.time > deadline) {
                Destroy(attackObj);
                yield break;
            }
            yield return null;
        }
    }

    // --- reset -----------------------------------------------------------------

    public void ResetSelection() {
        chain.Clear();
        RefreshPreview();
    }

    // --- preview -----------------------------------------------------------------

    void RefreshPreview() {
        if (preview == null) return;
        if (chain.Count == 0) {
            preview.text = (GameDirector.Instance != null &&
                            GameDirector.Instance.CurrentMode == Spawner.Mode.ComputerArithmeticBasics)
                ? "build the target digit:  e.g.  5 × 2 − 3"
                : "build your attack:  e.g.  3 × (8 - 2)";
            return;
        }
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < chain.Count; i++) {
            if (i > 0) sb.Append(" ");
            ChainToken t = chain[i];
            if (t.isOperator) {
                if (t.op == '*') sb.Append("×");
                else if (t.op == '/') sb.Append("÷");
                else sb.Append(t.op);
            } else {
                sb.Append(t.number);
            }
        }
        preview.text = sb.ToString();
    }
}
