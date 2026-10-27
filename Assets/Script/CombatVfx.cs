using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime procedural particle and visual effects system.
/// Requires zero pre-made prefabs or external texture assets.
/// </summary>
public class CombatVfx : MonoBehaviour {

    public static CombatVfx Instance { get; private set; }

    static Material particleMat;
    static Texture2D sparkTex;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap() {
        if (Instance == null) {
            new GameObject("~CombatVfx").AddComponent<CombatVfx>();
        }
    }

    void Awake() {
        if (Instance == null) Instance = this;
    }

    public static Material GetParticleMaterial() {
        if (particleMat != null) return particleMat;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null) shader = Shader.Find("UI/Default");

        particleMat = new Material(shader);
        particleMat.mainTexture = GetSparkTexture();
        return particleMat;
    }

    public static Texture2D GetSparkTexture() {
        if (sparkTex != null) return sparkTex;
        int size = 32;
        sparkTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                float dx = (x - center) / center;
                float dy = (y - center) / center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1.0f - dist);
                a = a * a; // quadratic soft falloff
                sparkTex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        sparkTex.Apply();
        return sparkTex;
    }

    /// <summary>Explosion of particles at boss hit location.</summary>
    public void SpawnBossHit(Vector3 pos, int damage, bool isCrit) {
        GameObject go = new GameObject("hit_vfx");
        go.transform.position = pos;

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ParticleSystemRenderer psr = go.GetComponent<ParticleSystemRenderer>();
        psr.material = GetParticleMaterial();
        psr.sortingOrder = 50;

        var main = ps.main;
        main.loop = false;
        main.duration = 0.5f;
        main.startLifetime = isCrit ? 0.65f : 0.45f;
        main.startSpeed = isCrit ? new ParticleSystem.MinMaxCurve(3f, 8f) : new ParticleSystem.MinMaxCurve(2f, 5f);
        main.startSize = isCrit ? new ParticleSystem.MinMaxCurve(0.2f, 0.45f) : new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
        main.startColor = isCrit ? new Color(1f, 0.85f, 0.25f) : new Color(1f, 0.45f, 0.25f);
        main.stopAction = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] {
            new ParticleSystem.Burst(0.0f, isCrit ? (short)45 : (short)20)
        });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.3f;

        var velOverTime = ps.velocityOverLifetime;
        velOverTime.enabled = true;
        velOverTime.space = ParticleSystemSimulationSpace.World;
        velOverTime.orbitalZ = new ParticleSystem.MinMaxCurve(-2f, 2f);

        ps.Play();
    }

    /// <summary>Sparkle effect when clicking numbers / operators.</summary>
    public void SpawnPickupSparkle(Vector3 pos, Color color) {
        GameObject go = new GameObject("pickup_sparkle");
        go.transform.position = pos;

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ParticleSystemRenderer psr = go.GetComponent<ParticleSystemRenderer>();
        psr.material = GetParticleMaterial();
        psr.sortingOrder = 45;

        var main = ps.main;
        main.loop = false;
        main.duration = 0.35f;
        main.startLifetime = 0.4f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.22f);
        main.startColor = color;
        main.stopAction = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 12) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.2f;

        ps.Play();
    }

    /// <summary>Shockwave ring expanding outward (e.g., from Bomb powerup).</summary>
    public void SpawnShockwave(Vector3 center, Color color, float maxRadius = 5.0f) {
        StartCoroutine(ShockwaveCo(center, color, maxRadius));
    }

    IEnumerator ShockwaveCo(Vector3 center, Color color, float maxRadius) {
        GameObject ring = new GameObject("shockwave");
        ring.transform.position = center;
        LineRenderer lr = ring.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = true;
        lr.positionCount = 36;
        lr.material = GetParticleMaterial();
        lr.startColor = color;
        lr.endColor = color;
        lr.sortingOrder = 48;

        float duration = 0.45f;
        float elapsed = 0f;

        while (elapsed < duration) {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float r = Mathf.Lerp(0.2f, maxRadius, t);
            float alpha = Mathf.Clamp01(1f - t);
            Color c = new Color(color.r, color.g, color.b, alpha);
            lr.startColor = c;
            lr.endColor = c;
            lr.startWidth = Mathf.Lerp(0.35f, 0.05f, t);
            lr.endWidth = lr.startWidth;

            for (int i = 0; i < 36; i++) {
                float angle = (i / 36f) * Mathf.PI * 2f;
                lr.SetPosition(i, center + new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r, 0f));
            }
            yield return null;
        }

        Destroy(ring);
    }

    /// <summary>Laser projectile line from start to target.</summary>
    public void SpawnLaserBeam(Vector3 start, Vector3 end, Color color, float duration = 0.25f) {
        StartCoroutine(LaserCo(start, end, color, duration));
    }

    IEnumerator LaserCo(Vector3 start, Vector3 end, Color color, float duration) {
        GameObject beam = new GameObject("laser_beam");
        LineRenderer lr = beam.AddComponent<LineRenderer>();
        lr.material = GetParticleMaterial();
        lr.startWidth = 0.35f;
        lr.endWidth = 0.15f;
        lr.positionCount = 2;
        lr.SetPosition(0, start);
        lr.SetPosition(1, end);
        lr.startColor = Color.white;
        lr.endColor = color;
        lr.sortingOrder = 49;

        float elapsed = 0f;
        while (elapsed < duration) {
            elapsed += Time.deltaTime;
            float alpha = 1f - (elapsed / duration);
            lr.startColor = new Color(1f, 1f, 1f, alpha);
            lr.endColor = new Color(color.r, color.g, color.b, alpha);
            yield return null;
        }
        Destroy(beam);
    }
}
