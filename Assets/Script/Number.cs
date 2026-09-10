using UnityEngine;

/// <summary>
/// A falling alien number (0-9). Frozen while selected in the player's
/// expression chain; flies up and vanishes when the expression is fired.
/// Falling past the bottom of the screen costs the player a life
/// (reported to GameDirector).
/// </summary>
public class Number : MonoBehaviour {

    public int value;

    bool missReported;
    bool isOperated;

    Vector2 startPoint;
    static readonly Vector2 endPoint = new Vector2(0f, 5f);
    Vector3 startScale;
    const float duration = 1.0f;
    float startTime;
    int rotateDirection;

    void Start() {
        if (transform.position.x > 0) rotateDirection = -1;
        else rotateDirection = 1;
    }

    void Update() {
        if (!isOperated && transform.position.y < KillLineY()) {
            if (!missReported) {
                missReported = true;
                if (GameDirector.Instance != null) GameDirector.Instance.RegisterMiss();
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

    /// <summary>World Y below which an alien counts as having fallen past you (just off the bottom of the view).</summary>
    static float KillLineY() {
        Camera cam = Camera.main;
        if (cam != null && cam.orthographic) {
            return cam.transform.position.y - cam.orthographicSize - 0.3f;
        }
        return -0.2f;
    }

    /// <summary>Called (via SendMessage) when the expression this number belongs to is fired.</summary>
    void operated() {
        startPoint = transform.position;
        startScale = transform.localScale;
        startTime = Time.time;
        isOperated = true;
    }
}
