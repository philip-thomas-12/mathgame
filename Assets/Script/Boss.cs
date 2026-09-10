using System.Collections;
using UnityEngine;

/// <summary>
/// The boss at the bottom of the screen. Attached at runtime by GameDirector
/// to the existing "boss" scene object (a fallback sprite is created if the
/// scene object is missing). Takes damage from fired expressions.
/// </summary>
public class Boss : MonoBehaviour {

    public int MaxHp { get; private set; }
    public int Hp { get; private set; }
    public bool Alive { get { return Hp > 0; } }

    SpriteRenderer sr;
    Color baseColor = Color.white;
    TextMesh label;
    Renderer mainRenderer;
    Vector3 basePos;
    float bobT;
    bool canBob;

    SpriteRenderer barBg, barFill;
    float barWidth = 3.2f;
    Vector3 barAnchor;

    static Texture2D whiteTex;

    public void Init(int maxHp) {
        MaxHp = maxHp;
        Hp = maxHp;

        sr = GetComponent<SpriteRenderer>();
        if (sr != null) baseColor = sr.color;

        // The old scene may carry its own oversized "bossText" label — hide it;
        // we render one small, clean HP label of our own instead.
        foreach (TextMesh oldLabel in GetComponentsInChildren<TextMesh>()) oldLabel.gameObject.SetActive(false);
        mainRenderer = (sr != null) ? (Renderer)sr : GetComponentInChildren<Renderer>();

        basePos = transform.position;
        canBob = GetComponent<Animator>() == null;
        bobT = Random.value * 6f;

        Camera cam = Camera.main;
        if (cam != null) barWidth = cam.orthographicSize * 0.55f;

        CreateBar();
        RefreshVisuals();
    }

    void Update() {
        if (canBob && GameDirector.Instance != null && GameDirector.Instance.InputEnabled) {
            bobT += Time.deltaTime * 2f;
            transform.position = basePos + new Vector3(0f, Mathf.Sin(bobT) * 0.12f, 0f);
        }
        PositionBar();
    }

    public void TakeDamage(int dmg) {
        if (!Alive) return;
        Hp = Mathf.Max(0, Hp - dmg);
        RefreshVisuals();
        if (sr != null) StartCoroutine(Flash());
        if (GameDirector.Instance != null) GameDirector.Instance.OnBossHit();
        if (Hp <= 0 && GameDirector.Instance != null) GameDirector.Instance.BossDefeated();
    }

    public void Reset(int maxHp) {
        MaxHp = maxHp;
        Hp = maxHp;
        if (sr != null) sr.color = baseColor;
        transform.position = basePos;
        RefreshVisuals();
    }

    IEnumerator Flash() {
        sr.color = new Color(1f, 0.25f, 0.25f);
        yield return new WaitForSeconds(0.1f);
        if (sr != null) sr.color = baseColor;
    }

    // --- HP bar -----------------------------------------------------------

    void CreateBar() {
        barBg = MakeBarSprite(new Color(0f, 0f, 0f, 0.55f), 40);
        barFill = MakeBarSprite(new Color(0.35f, 0.95f, 0.45f), 41);
        if (GameDirector.Instance != null) {
            label = GameDirector.Instance.MakeWorldText("",
                transform.position, 0.4f, new Color(0.95f, 0.95f, 0.98f), 42, TextAnchor.LowerCenter);
        }
    }

    static SpriteRenderer MakeBarSprite(Color color, int order) {
        if (whiteTex == null) whiteTex = Texture2D.whiteTexture;
        GameObject go = new GameObject("~bossBarPart");
        SpriteRenderer r = go.AddComponent<SpriteRenderer>();
        r.sprite = Sprite.Create(whiteTex, new Rect(0f, 0f, whiteTex.width, whiteTex.height),
                                 new Vector2(0f, 0.5f), whiteTex.width); // 1 world unit wide at scale (1,1)
        r.color = color;
        r.sortingOrder = order;
        return r;
    }

    void RefreshVisuals() {
        float ratio = (MaxHp > 0) ? (float)Hp / MaxHp : 0f;

        if (barBg != null) {
            Vector3 s = barBg.transform.localScale;
            s.x = barWidth;
            s.y = 0.28f;
            barBg.transform.localScale = s;
        }
        if (barFill != null) {
            Vector3 s = barFill.transform.localScale;
            s.x = Mathf.Max(0.02f, barWidth * ratio);
            s.y = 0.28f;
            barFill.transform.localScale = s;
            barFill.color = (ratio > 0.5f) ? new Color(0.35f, 0.95f, 0.45f)
                          : (ratio > 0.25f) ? new Color(0.95f, 0.85f, 0.30f)
                          : new Color(0.95f, 0.30f, 0.30f);
        }
        if (label != null) label.text = "HP " + Hp + "/" + MaxHp;
    }

    void PositionBar() {
        float topY = transform.position.y + 1.0f;
        if (mainRenderer != null) topY = mainRenderer.bounds.max.y;
        barAnchor = new Vector3(transform.position.x - barWidth * 0.5f, topY + 0.35f, 0f);
        if (barBg != null) barBg.transform.position = barAnchor;
        if (barFill != null) barFill.transform.position = barAnchor;
        if (label != null) {
            label.transform.position = new Vector3(transform.position.x, topY + 0.75f, 0f);
        }
    }
}
