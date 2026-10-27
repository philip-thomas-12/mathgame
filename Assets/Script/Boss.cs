using System.Collections;
using UnityEngine;

/// <summary>
/// The boss at the bottom of the screen.
/// Upgraded with:
///  - Active shield conditions (e.g., weak to even/odd, minimum damage threshold)
///  - Smooth ghost HP bar depletion (fighting-game style)
///  - Enrage phase (rage bobbing, glow, and tension music)
///  - Procedural impact VFX
/// </summary>
public class Boss : MonoBehaviour {

    public enum ShieldType {
        None,
        EvenBonus,     // Even expressions deal 1.5x damage, odd 0.5x
        OddBonus,      // Odd expressions deal 1.5x damage, even 0.5x
        MinDamage,     // Requires at least 20 damage to penetrate shield
        Multi5         // Multiples of 5 deal 2.0x crit damage
    }

    public int MaxHp { get; private set; }
    public int Hp { get; private set; }
    public bool Alive { get { return Hp > 0; } }
    public ShieldType CurrentShield { get; private set; }
    public bool IsEnraged { get; private set; }

    SpriteRenderer sr;
    Color baseColor = Color.white;
    TextMesh label;
    TextMesh shieldLabel;
    Renderer mainRenderer;
    Vector3 basePos;
    float bobT;
    bool canBob;

    SpriteRenderer barBg, barGhost, barFill;
    float barWidth = 3.2f;
    Vector3 barAnchor;
    float ghostFillWidth;

    static Texture2D whiteTex;

    public void Init(int maxHp) {
        MaxHp = maxHp;
        Hp = maxHp;
        IsEnraged = false;
        CurrentShield = ShieldType.None;

        sr = GetComponent<SpriteRenderer>();
        if (sr != null) baseColor = sr.color;

        foreach (TextMesh oldLabel in GetComponentsInChildren<TextMesh>()) oldLabel.gameObject.SetActive(false);
        mainRenderer = (sr != null) ? (Renderer)sr : GetComponentInChildren<Renderer>();

        basePos = transform.position;
        canBob = GetComponent<Animator>() == null;
        bobT = Random.value * 6f;

        Camera cam = Camera.main;
        if (cam != null) barWidth = cam.orthographicSize * 0.55f;
        ghostFillWidth = barWidth;

        // In wave 2 or higher (HP > 160), activate a shield rule!
        if (maxHp > 160) {
            ShieldType[] shields = { ShieldType.EvenBonus, ShieldType.OddBonus, ShieldType.MinDamage, ShieldType.Multi5 };
            CurrentShield = shields[Random.Range(0, shields.Length)];
        }

        CreateBar();
        RefreshVisuals();
    }

    void Update() {
        float speedMult = IsEnraged ? 4.0f : 2.0f;
        float bobAmp = IsEnraged ? 0.18f : 0.12f;

        if (canBob && GameDirector.Instance != null && GameDirector.Instance.InputEnabled) {
            bobT += Time.deltaTime * speedMult;
            transform.position = basePos + new Vector3(0f, Mathf.Sin(bobT) * bobAmp, 0f);
        }
        PositionBar();
        UpdateGhostBar();
    }

    /// <summary>Calculates effective damage after applying boss shields.</summary>
    public int ProcessShieldDamage(int incomingDmg, out bool isCrit, out string shieldFeedback) {
        isCrit = false;
        shieldFeedback = null;

        if (CurrentShield == ShieldType.None) return incomingDmg;

        int finalDmg = incomingDmg;
        switch (CurrentShield) {
            case ShieldType.EvenBonus:
                if (incomingDmg % 2 == 0) {
                    finalDmg = Mathf.RoundToInt(incomingDmg * 1.5f);
                    isCrit = true;
                    shieldFeedback = "SHIELD WEAKNESS! 1.5x EVEN DMG";
                } else {
                    finalDmg = Mathf.Max(1, Mathf.RoundToInt(incomingDmg * 0.6f));
                    shieldFeedback = "SHIELD RESISTED ODD DMG!";
                }
                break;

            case ShieldType.OddBonus:
                if (incomingDmg % 2 != 0) {
                    finalDmg = Mathf.RoundToInt(incomingDmg * 1.5f);
                    isCrit = true;
                    shieldFeedback = "SHIELD WEAKNESS! 1.5x ODD DMG";
                } else {
                    finalDmg = Mathf.Max(1, Mathf.RoundToInt(incomingDmg * 0.6f));
                    shieldFeedback = "SHIELD RESISTED EVEN DMG!";
                }
                break;

            case ShieldType.MinDamage:
                if (incomingDmg < 20) {
                    finalDmg = 0;
                    shieldFeedback = "SHIELD DEFLECTED! (REQ >= 20 DMG)";
                } else {
                    finalDmg = incomingDmg;
                    shieldFeedback = "SHIELD PENETRATED!";
                    isCrit = true;
                }
                break;

            case ShieldType.Multi5:
                if (incomingDmg > 0 && incomingDmg % 5 == 0) {
                    finalDmg = incomingDmg * 2;
                    isCrit = true;
                    shieldFeedback = "MULTIPLE OF 5! 2.0x CRIT!";
                } else {
                    finalDmg = incomingDmg;
                }
                break;
        }

        return finalDmg;
    }

    public void TakeDamage(int rawDmg) {
        if (!Alive) return;

        bool isCrit = false;
        string shieldFeedback;
        int effectiveDmg = ProcessShieldDamage(rawDmg, out isCrit, out shieldFeedback);

        if (effectiveDmg == 0) {
            SoundFx.ShieldHit();
            if (shieldFeedback != null && GameDirector.Instance != null) {
                GameDirector.Instance.Popup(shieldFeedback, transform.position + Vector3.up * 1.5f, new Color(0.4f, 0.8f, 1f), 0.45f);
            }
            return;
        }

        if (shieldFeedback != null && GameDirector.Instance != null) {
            Color feedbackColor = isCrit ? new Color(1f, 0.85f, 0.35f) : new Color(0.6f, 0.8f, 1f);
            GameDirector.Instance.Popup(shieldFeedback, transform.position + Vector3.up * 1.5f, feedbackColor, 0.45f);
        }

        Hp = Mathf.Max(0, Hp - effectiveDmg);
        RefreshVisuals();

        if (CombatVfx.Instance != null) {
            CombatVfx.Instance.SpawnBossHit(transform.position + Vector3.up * 0.5f, effectiveDmg, isCrit);
        }

        if (isCrit) SoundFx.Explosion(true);
        else SoundFx.Explosion(false);

        if (sr != null) StartCoroutine(Flash(isCrit));
        if (GameDirector.Instance != null) GameDirector.Instance.OnBossHit();

        // Enrage check (at < 35% HP)
        if (Hp > 0 && Hp <= MaxHp * 0.35f && !IsEnraged) {
            IsEnraged = true;
            SoundFx.SetMusicPitch(1.15f);
            if (GameDirector.Instance != null) {
                GameDirector.Instance.Popup("BOSS ENRAGED!", transform.position + Vector3.up * 2.2f, new Color(1f, 0.2f, 0.2f), 0.65f);
                GameDirector.Instance.Shake(0.35f, 0.2f);
            }
        }

        if (Hp <= 0) {
            SoundFx.SetMusicPitch(1.0f);
            if (GameDirector.Instance != null) GameDirector.Instance.BossDefeated();
        }
    }

    public void Reset(int maxHp) {
        MaxHp = maxHp;
        Hp = maxHp;
        IsEnraged = false;
        if (sr != null) sr.color = baseColor;
        transform.position = basePos;
        ghostFillWidth = barWidth;
        SoundFx.SetMusicPitch(1.0f);
        RefreshVisuals();
    }

    IEnumerator Flash(bool isCrit) {
        if (sr == null) yield break;
        sr.color = isCrit ? new Color(1f, 0.9f, 0.3f) : new Color(1f, 0.25f, 0.25f);
        yield return new WaitForSeconds(0.12f);
        if (sr != null) sr.color = IsEnraged ? new Color(1f, 0.55f, 0.55f) : baseColor;
    }

    // --- HP bar & Shields -------------------------------------------------

    void CreateBar() {
        barBg = MakeBarSprite(new Color(0f, 0f, 0f, 0.6f), 40);
        barGhost = MakeBarSprite(new Color(1f, 0.85f, 0.4f, 0.85f), 41); // yellow ghost bar
        barFill = MakeBarSprite(new Color(0.35f, 0.95f, 0.45f), 42);

        if (GameDirector.Instance != null) {
            label = GameDirector.Instance.MakeWorldText("",
                transform.position, 0.4f, new Color(0.95f, 0.95f, 0.98f), 43, TextAnchor.LowerCenter);

            shieldLabel = GameDirector.Instance.MakeWorldText("",
                transform.position, 0.34f, new Color(0.4f, 0.85f, 1f), 43, TextAnchor.LowerCenter);
        }
    }

    static SpriteRenderer MakeBarSprite(Color color, int order) {
        if (whiteTex == null) whiteTex = Texture2D.whiteTexture;
        GameObject go = new GameObject("~bossBarPart");
        SpriteRenderer r = go.AddComponent<SpriteRenderer>();
        r.sprite = Sprite.Create(whiteTex, new Rect(0f, 0f, whiteTex.width, whiteTex.height),
                                 new Vector2(0f, 0.5f), whiteTex.width);
        r.color = color;
        r.sortingOrder = order;
        return r;
    }

    void RefreshVisuals() {
        float ratio = (MaxHp > 0) ? (float)Hp / MaxHp : 0f;
        float targetWidth = Mathf.Max(0.02f, barWidth * ratio);

        if (barBg != null) {
            Vector3 s = barBg.transform.localScale;
            s.x = barWidth;
            s.y = 0.28f;
            barBg.transform.localScale = s;
        }

        if (barFill != null) {
            Vector3 s = barFill.transform.localScale;
            s.x = targetWidth;
            s.y = 0.28f;
            barFill.transform.localScale = s;
            barFill.color = (ratio > 0.5f) ? new Color(0.35f, 0.95f, 0.45f)
                          : (ratio > 0.25f) ? new Color(0.95f, 0.85f, 0.30f)
                          : new Color(0.95f, 0.30f, 0.30f);
        }

        if (label != null) {
            label.text = "HP " + Hp + "/" + MaxHp + (IsEnraged ? " [ENRAGED]" : "");
            label.color = IsEnraged ? new Color(1f, 0.4f, 0.4f) : Color.white;
        }

        if (shieldLabel != null) {
            if (CurrentShield == ShieldType.None) {
                shieldLabel.text = "";
            } else {
                shieldLabel.text = GetShieldBadgeText(CurrentShield);
            }
        }
    }

    string GetShieldBadgeText(ShieldType s) {
        switch (s) {
            case ShieldType.EvenBonus: return "🛡 SHIELD: WEAK TO EVEN (1.5x)";
            case ShieldType.OddBonus:  return "🛡 SHIELD: WEAK TO ODD (1.5x)";
            case ShieldType.MinDamage: return "🛡 SHIELD: NEEDS >= 20 DMG";
            case ShieldType.Multi5:    return "🛡 SHIELD: 5x MULTIPLIER (2x CRIT)";
            default:                   return "";
        }
    }

    void UpdateGhostBar() {
        if (barGhost == null || barFill == null) return;
        float currentFill = barFill.transform.localScale.x;
        if (ghostFillWidth > currentFill) {
            ghostFillWidth = Mathf.MoveTowards(ghostFillWidth, currentFill, Time.deltaTime * 1.5f);
            Vector3 s = barGhost.transform.localScale;
            s.x = ghostFillWidth;
            s.y = 0.28f;
            barGhost.transform.localScale = s;
        } else {
            ghostFillWidth = currentFill;
        }
    }

    void PositionBar() {
        float topY = transform.position.y + 1.0f;
        if (mainRenderer != null) topY = mainRenderer.bounds.max.y;
        barAnchor = new Vector3(transform.position.x - barWidth * 0.5f, topY + 0.35f, 0f);

        if (barBg != null) barBg.transform.position = barAnchor;
        if (barGhost != null) barGhost.transform.position = barAnchor;
        if (barFill != null) barFill.transform.position = barAnchor;

        if (label != null) {
            label.transform.position = new Vector3(transform.position.x, topY + 0.72f, 0f);
        }
        if (shieldLabel != null) {
            shieldLabel.transform.position = new Vector3(transform.position.x, topY + 1.15f, 0f);
        }
    }
}

