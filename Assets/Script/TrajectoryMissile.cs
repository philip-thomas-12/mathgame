using System.Collections;
using UnityEngine;

/// <summary>
/// Level 5: Trajectory Curve Interceptor artillery shell.
/// Moves along a real parabolic flight path:
///   y(t) = 4 * apexHeight * (t - t^2)
/// Renders a glowing trajectory trail using LineRenderer.
/// When neutralized by the player's correct intercept coordinate x,
/// explodes in mid-air with a particle shockwave.
/// </summary>
public class TrajectoryMissile : MonoBehaviour {

    Vector3 startPos;
    Vector3 endPos;
    float apexHeight = 4.5f;
    float flightDuration = 8.0f;
    float elapsed;
    bool neutralized;

    LineRenderer trail;
    static Texture2D missileTex;

    public void Launch(Vector3 start, Vector3 end, float apex, float duration) {
        startPos = start;
        endPos = end;
        apexHeight = apex;
        flightDuration = Mathf.Max(duration, 4.0f);
        elapsed = 0f;
        transform.position = start;

        BuildVisual();
    }

    void BuildVisual() {
        SpriteRenderer sr = gameObject.AddComponent<SpriteRenderer>();
        if (missileTex == null) {
            int sz = 32;
            missileTex = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
            for (int y = 0; y < sz; y++) {
                for (int x = 0; x < sz; x++) {
                    float dx = (x - 15.5f) / 15.5f;
                    float dy = (y - 15.5f) / 15.5f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1.0f - r);
                    missileTex.SetPixel(x, y, new Color(1f, 0.45f, 0.2f, a));
                }
            }
            missileTex.Apply();
        }

        sr.sprite = Sprite.Create(missileTex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 24f);
        sr.sortingOrder = 30;

        // Glowing trajectory line
        trail = gameObject.AddComponent<LineRenderer>();
        trail.material = CombatVfx.GetParticleMaterial();
        trail.startWidth = 0.22f;
        trail.endWidth = 0.05f;
        trail.startColor = new Color(1f, 0.6f, 0.2f, 0.8f);
        trail.endColor = new Color(1f, 0.2f, 0.1f, 0.1f);
        trail.sortingOrder = 29;
        trail.positionCount = 0;
    }

    void Update() {
        if (neutralized) return;

        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / flightDuration);

        // Parabolic arc interpolation
        Vector3 groundPos = Vector3.Lerp(startPos, endPos, t);
        float arch = 4.0f * apexHeight * (t - t * t);
        Vector3 currentPos = groundPos + new Vector3(0f, arch, 0f);
        transform.position = currentPos;

        // Update trajectory trail points
        if (trail != null && trail.positionCount < 60) {
            trail.positionCount++;
            trail.SetPosition(trail.positionCount - 1, currentPos);
        }

        // Reached end of flight without being intercepted
        if (t >= 1.0f) {
            OnImpactGround();
        }
    }

    public void Intercept(Vector3 laserOrigin) {
        if (neutralized) return;
        neutralized = true;

        if (CombatVfx.Instance != null) {
            CombatVfx.Instance.SpawnLaserBeam(laserOrigin, transform.position, Color.cyan, 0.3f);
            CombatVfx.Instance.SpawnShockwave(transform.position, new Color(0.2f, 0.85f, 1f), 6.0f);
            CombatVfx.Instance.SpawnBossHit(transform.position, 100, true);
        }

        SoundFx.Explosion(true);
        Destroy(gameObject, 0.05f);
    }

    void OnImpactGround() {
        neutralized = true;
        if (CombatVfx.Instance != null) {
            CombatVfx.Instance.SpawnBossHit(transform.position, 50, false);
        }
        SoundFx.Miss();

        if (GameDirector.Instance != null) {
            GameDirector.Instance.RegisterMiss();
        }
        Destroy(gameObject);
    }
}
