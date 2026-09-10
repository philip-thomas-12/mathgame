using System.Collections;
using UnityEngine;

/// <summary>
/// A falling "artifact": an operator pickup (+, -, ×, ÷, ^, brackets).
/// The player clicks these to add them to their expression chain.
/// Visuals are generated in code (colored disc + glyph), so no sprites
/// or scene edits are needed.
/// </summary>
public class OperatorPickup : MonoBehaviour {

    public char Symbol { get; private set; }
    public bool IsBracket { get { return Symbol == '(' || Symbol == ')'; } }

    bool isOperated;
    bool destroyScheduled;
    Vector2 startPoint;
    Vector3 startScale;
    float startTime;
    const float duration = 1.0f;
    static readonly Vector2 endPoint = new Vector2(0f, 5f);
    int rotateDirection;

    static Texture2D discTexture;

    public void Setup(char symbol) {
        Symbol = symbol;
        gameObject.tag = "number"; // reuses the existing clickable tag

        Color c = ColorFor(symbol);
        BuildVisual(symbol, c);

        if (GetComponent<Collider2D>() == null) {
            CircleCollider2D col = gameObject.AddComponent<CircleCollider2D>();
            col.radius = 0.55f;
        }
    }

    /// <summary>
    /// Computer-arithmetic mode: a golden "?" artifact. The digit it hides is
    /// the target the player must reach (GameDirector keeps the value). It is
    /// NOT tagged "number", so expression chains ignore it.
    /// </summary>
    public void SetupDigitTarget(int target) {
        gameObject.name = "digitTarget_" + target;
        BuildVisual('?', new Color(1f, 0.82f, 0.30f));
        if (GetComponent<Collider2D>() == null) {
            CircleCollider2D col = gameObject.AddComponent<CircleCollider2D>();
            col.radius = 0.55f;
        }
    }

    static Color ColorFor(char symbol) {
        switch (symbol) {
            case '+': return new Color(0.35f, 0.90f, 0.40f);
            case '-': return new Color(1.00f, 0.62f, 0.20f);
            case '×': return new Color(0.75f, 0.45f, 0.95f);
            case '÷': return new Color(0.40f, 0.65f, 1.00f);
            case '^': return new Color(1.00f, 0.50f, 0.75f);
            default:  return new Color(0.75f, 0.78f, 0.85f); // brackets
        }
    }

    void BuildVisual(char symbol, Color color) {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = Sprite.Create(GetDiscTexture(),
                                  new Rect(0f, 0f, GetDiscTexture().width, GetDiscTexture().height),
                                  new Vector2(0.5f, 0.5f), 40f);
        sr.color = color;
        sr.sortingOrder = 5;

        GameObject glyphGo = new GameObject("glyph");
        glyphGo.transform.SetParent(transform, false);
        glyphGo.transform.localPosition = Vector3.zero;

        TextMesh tm = glyphGo.AddComponent<TextMesh>();
        tm.font = BuiltinFont();
        tm.text = symbol.ToString();
        tm.fontSize = 64;
        tm.characterSize = 0.11f; // ~0.7 world units tall at fontSize 64 — fits inside the 1.6-unit disc
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = new Color(0.08f, 0.08f, 0.12f);

        MeshRenderer mr = glyphGo.GetComponent<MeshRenderer>();
        mr.sharedMaterial = tm.font.material;
        mr.sortingOrder = 6;
    }

    static Texture2D GetDiscTexture() {
        if (discTexture != null) return discTexture;
        int size = 64;
        discTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                float dx = (x - center) / center;
                float dy = (y - center) / center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01(0.92f - dist) * 6f; // soft edge
                discTexture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(alpha)));
            }
        }
        discTexture.Apply();
        return discTexture;
    }

    static Font _font;
    internal static Font BuiltinFont() {
        if (_font != null) return _font;
        try { _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
        catch { _font = Resources.GetBuiltinResource<Font>("Arial.ttf"); }
        return _font;
    }

    void Start() {
        if (transform.position.x > 0) rotateDirection = -1; else rotateDirection = 1;
    }

    void Update() {
        if (!isOperated && transform.position.y < KillLineY()) {
            // dropped off screen: operators are free to miss, only aliens cost a life
            if (!destroyScheduled) {
                destroyScheduled = true;
                Destroy(gameObject, 1.0f);
            }
        }

        if (isOperated) {
            float t = (Time.time - startTime) / duration;
            transform.position = Vector2.Lerp(startPoint, endPoint, t);
            transform.localScale = Vector2.Lerp(startScale, Vector3.zero, t);
            transform.Rotate(Vector3.forward * 8 * rotateDirection);
            if (transform.position.y >= endPoint.y - 0.01f) Destroy(gameObject);
        }
    }

    /// <summary>World Y below which an artifact is recycled (operators are free to miss — only aliens cost a life).</summary>
    static float KillLineY() {
        Camera cam = Camera.main;
        if (cam != null && cam.orthographic) {
            return cam.transform.position.y - cam.orthographicSize - 0.3f;
        }
        return -0.2f;
    }

    /// <summary>Called (via SendMessage) when the expression this token belongs to is fired.</summary>
    void operated() {
        startPoint = transform.position;
        startScale = transform.localScale;
        startTime = Time.time;
        isOperated = true;
    }
}
