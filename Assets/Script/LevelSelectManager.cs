using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The level-select screen, generated entirely in code (no scene edits).
/// Lists every level from LevelCatalog as a clickable row — including LOCKED
/// ones, which show why they are locked ("clear level N first"). Clicking a
/// locked row plays a reject sound + popup; clicking an unlocked row hands the
/// level to GameDirector.StartLevel.
/// </summary>
public class LevelSelectManager : MonoBehaviour {

    readonly List<GameObject> cardRoots = new List<GameObject>();

    void Start() {
        Enable();
    }

    /// <summary>(Re)builds and shows the level rows. Called on start and on returning from a level.</summary>
    public void Enable() {
        ClearCards();
        BuildCards();
    }

    /// <summary>Hides the select screen (called when a level starts).</summary>
    public void Hide() {
        ClearCards();
    }

    void ClearCards() {
        for (int i = 0; i < cardRoots.Count; i++) {
            if (cardRoots[i] != null) Destroy(cardRoots[i]);
        }
        cardRoots.Clear();
    }

    void BuildCards() {
        Camera cam = Camera.main;
        Vector2 c = (cam != null) ? (Vector2)cam.transform.position : Vector2.zero;
        float halfH = (cam != null) ? cam.orthographicSize : 5.5f;
        float halfW = halfH * ((cam != null) ? cam.aspect : 1.777f);

        LevelData[] levels = LevelCatalog.GetLevels();

        // Header.
        TextMesh header = MakeText("SELECT LEVEL",
            new Vector3(c.x, c.y + halfH - 0.8f, 0f), 0.75f, new Color(1f, 0.85f, 0.35f), 30, TextAnchor.MiddleCenter);
        header.fontStyle = FontStyle.Bold;
        cardRoots.Add(header.gameObject);

        // One row per level.
        float rowH = Mathf.Min(1.05f, (halfH * 1.7f) / Mathf.Max(1, levels.Length));
        float y0 = c.y + halfH - 2.1f;

        for (int i = 0; i < levels.Length; i++) {
            LevelData lvl = levels[i];
            bool unlocked = LevelCatalog.IsUnlocked(i);
            bool cleared = LevelManager.HighestClearedLevelPlusOne() > lvl.minClearedLevel;

            GameObject root = new GameObject("level_card_" + i);
            root.transform.position = new Vector3(c.x, y0 - i * rowH, 0f);
            BoxCollider2D col = root.AddComponent<BoxCollider2D>();
            col.size = new Vector2(halfW * 1.7f, rowH * 0.92f);
            root.AddComponent<LevelCardInput>().Init(this, i);

            Color accent = unlocked ? new Color(0.40f, 1.00f, 0.55f) : new Color(0.65f, 0.65f, 0.72f);
            Color mainColor = unlocked ? new Color(0.95f, 0.95f, 0.98f) : new Color(0.62f, 0.62f, 0.70f);

            string status;
            if (cleared) status = "CLEARED";
            else if (unlocked) status = "READY";
            else status = "LOCKED - " + (string.IsNullOrEmpty(lvl.lockReason) ? "clear earlier levels" : lvl.lockReason);

            TextMesh main = MakeText(lvl.title + "   " + lvl.description,
                root.transform.position, 0.32f, mainColor, 31, TextAnchor.MiddleLeft);
            main.transform.SetParent(root.transform, false);
            main.transform.localPosition = new Vector3(-halfW * 0.80f, 0.14f, 0f);
            main.transform.position = new Vector3(c.x - halfW * 0.80f, root.transform.position.y + 0.14f, 0f);

            TextMesh sub = MakeText(status, root.transform.position, 0.26f, accent, 31, TextAnchor.MiddleRight);
            sub.transform.SetParent(root.transform, false);
            sub.transform.localPosition = new Vector3(halfW * 0.78f, -0.10f, 0f);
            sub.transform.position = new Vector3(c.x + halfW * 0.78f, root.transform.position.y - 0.10f, 0f);

            cardRoots.Add(root);
        }

        // Footer hint.
        TextMesh foot = MakeText("click a level to play   ·   locked levels open as you clear earlier ones",
            new Vector3(c.x, c.y - halfH + 0.5f, 0f), 0.28f, new Color(1f, 1f, 1f, 0.55f), 31, TextAnchor.MiddleCenter);
        cardRoots.Add(foot.gameObject);
    }

    /// <summary>Called by a row's LevelCardInput on click.</summary>
    public void SelectLevel(int index) {
        LevelData[] levels = LevelCatalog.GetLevels();
        if (index < 0 || index >= levels.Length) return;

        if (!LevelCatalog.IsUnlocked(index)) {
            LevelData lvl = levels[index];
            string why = string.IsNullOrEmpty(lvl.lockReason)
                ? "clear earlier levels first"
                : lvl.lockReason;
            if (GameDirector.Instance != null) {
                GameDirector.Instance.Popup(why, new Vector3(0f, -2.2f, 0f),
                    new Color(1f, 0.4f, 0.35f), 0.45f);
            }
            SoundFx.Reject();
            return;
        }

        SoundFx.Select();
        Hide();
        if (GameDirector.Instance != null) {
            GameDirector.Instance.StartLevel(levels[index], index);
        }
    }

    static TextMesh MakeText(string txt, Vector3 pos, float height, Color color, int order, TextAnchor anchor) {
        GameObject go = new GameObject("select_text");
        go.transform.position = pos;
        TextMesh tm = go.AddComponent<TextMesh>();
        tm.font = OperatorPickup.BuiltinFont();
        tm.text = txt;
        tm.fontSize = 64;
        tm.characterSize = height / (64f * 0.1f);
        tm.anchor = anchor;
        tm.alignment = (anchor == TextAnchor.MiddleLeft) ? TextAlignment.Left
                     : (anchor == TextAnchor.MiddleRight) ? TextAlignment.Right
                     : TextAlignment.Center;
        tm.color = color;
        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = tm.font.material;
        mr.sortingOrder = order;
        return tm;
    }
}

/// <summary>Click handler for one level row (OnMouseDown needs a Collider2D on the row).</summary>
public class LevelCardInput : MonoBehaviour {
    LevelSelectManager owner;
    int index;

    public void Init(LevelSelectManager o, int i) {
        owner = o;
        index = i;
    }

    void OnMouseDown() {
        if (owner != null) owner.SelectLevel(index);
    }
}
