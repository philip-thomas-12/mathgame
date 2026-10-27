using System.Collections;
using UnityEngine;

/// <summary>
/// A collectible tactical power-up artifact falling from the top.
/// Generated in code with procedural discs and glyphs.
/// </summary>
public class PowerupPickup : MonoBehaviour {

    public enum Type {
        Freeze,       // Slows falling speeds by 60% for 6s
        Bomb,         // Destroys falling alien threats below mid-screen
        DoubleDamage, // 2x damage on next fired expression
        ExtraLife     // Restores 1 life (up to max 5)
    }

    public Type PowerType { get; private set; }

    static Texture2D discTexture;
    bool collected;
    float startY;

    public void Setup(Type type) {
        PowerType = type;
        gameObject.name = "powerup_" + type;
        startY = transform.position.y;

        Color c = ColorFor(type);
        string glyph = GlyphFor(type);
        BuildVisual(glyph, c);

        if (GetComponent<Collider2D>() == null) {
            CircleCollider2D col = gameObject.AddComponent<CircleCollider2D>();
            col.radius = 0.65f;
        }
    }

    static Color ColorFor(Type t) {
        switch (t) {
            case Type.Freeze:       return new Color(0.25f, 0.85f, 1.0f);
            case Type.Bomb:         return new Color(1.0f, 0.40f, 0.20f);
            case Type.DoubleDamage: return new Color(1.0f, 0.85f, 0.25f);
            case Type.ExtraLife:    return new Color(1.0f, 0.35f, 0.65f);
            default:                return Color.white;
        }
    }

    static string GlyphFor(Type t) {
        switch (t) {
            case Type.Freeze:       return "SLOW";
            case Type.Bomb:         return "BOMB";
            case Type.DoubleDamage: return "2X";
            case Type.ExtraLife:    return "+LIFE";
            default:                return "?";
        }
    }

    void BuildVisual(string glyph, Color color) {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();
        Texture2D tex = GetDiscTexture();
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 36f);
        sr.color = color;
        sr.sortingOrder = 6;

        GameObject glyphGo = new GameObject("glyph");
        glyphGo.transform.SetParent(transform, false);
        glyphGo.transform.localPosition = Vector3.zero;

        TextMesh tm = glyphGo.AddComponent<TextMesh>();
        tm.font = OperatorPickup.BuiltinFont();
        tm.text = glyph;
        tm.fontSize = 58;
        tm.characterSize = (glyph.Length > 2) ? 0.08f : 0.11f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.fontStyle = FontStyle.Bold;
        tm.color = new Color(0.08f, 0.08f, 0.12f);

        MeshRenderer mr = glyphGo.GetComponent<MeshRenderer>();
        mr.sharedMaterial = tm.font.material;
        mr.sortingOrder = 7;
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
                float alpha = Mathf.Clamp01(0.95f - dist) * 5f;
                // Add a bright outer border ring
                if (dist >= 0.72f && dist <= 0.92f) alpha = 1.0f;
                discTexture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(alpha)));
            }
        }
        discTexture.Apply();
        return discTexture;
    }

    void Update() {
        if (collected) return;

        // Subtle pulsing animation so powerups stand out
        float pulse = 1.0f + Mathf.Sin(Time.time * 6f) * 0.12f;
        transform.localScale = new Vector3(pulse, pulse, 1f);

        Camera cam = Camera.main;
        if (cam != null && transform.position.y < (cam.transform.position.y - cam.orthographicSize - 0.5f)) {
            Destroy(gameObject);
        }
    }

    void OnMouseDown() {
        Collect();
    }

    public void Collect() {
        if (collected) return;
        collected = true;

        SoundFx.Powerup();
        if (CombatVfx.Instance != null) {
            CombatVfx.Instance.SpawnPickupSparkle(transform.position, ColorFor(PowerType));
        }

        if (GameDirector.Instance != null) {
            GameDirector.Instance.ActivatePowerup(PowerType, transform.position);
        }

        Destroy(gameObject);
    }
}
