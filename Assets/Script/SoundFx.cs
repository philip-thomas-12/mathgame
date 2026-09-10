using System;
using UnityEngine;

/// <summary>
/// Tiny procedural sound engine — synthesizes all SFX at runtime with
/// sine tones / chirps, so the project needs no audio files.
/// Clips are built lazily once and cached.
/// </summary>
public static class SoundFx {

    static AudioClip select, reject, fire, hit, miss, levelUp, win, lose, go;
    static AudioClip musicLoop;
    static GameObject musicGo;
    static AudioSource musicSource;
    static bool musicEnabled = true;

    public static void Select()  { Play(Cached(ref select,  delegate { return Tone(880f,  0.05f, 0.22f, 1.5f); })); }
    public static void Reject()  { Play(Cached(ref reject,  delegate { return Tone(140f,  0.16f, 0.40f, 2.0f); })); }
    public static void Fire()    { Play(Cached(ref fire,    delegate { return Chirp(900f,  300f,  0.18f, 0.35f); })); }
    public static void Hit()     { Play(Cached(ref hit,     delegate { return Tone(140f,  0.15f, 0.55f, 3.0f); })); }
    public static void Miss()    { Play(Cached(ref miss,    delegate { return Chirp(320f,  110f,  0.35f, 0.40f); })); }
    public static void LevelUp() { Play(Cached(ref levelUp, delegate { return Chirp(440f,  990f,  0.20f, 0.30f); })); }
    public static void Go()      { Play(Cached(ref go,      delegate { return Tone(540f,  0.14f, 0.30f, 2.0f); })); }
    public static void Win()     { Play(Cached(ref win,     delegate { return Seq(new float[] { 523f, 659f, 784f, 1047f }, 0.12f, 0.30f); })); }
    public static void Lose()    { Play(Cached(ref lose,    delegate { return Seq(new float[] { 330f, 262f, 196f, 131f }, 0.20f, 0.35f); })); }

    static AudioClip Cached(ref AudioClip slot, Func<AudioClip> build) {
        if (slot == null) slot = build();
        return slot;
    }

    static void Play(AudioClip clip) {
        Camera cam = Camera.main;
        if (clip != null && cam != null) {
            AudioSource.PlayClipAtPoint(clip, cam.transform.position, 1f);
        }
    }

    // =====================================================================
    //  Background music
    // =====================================================================
    // A mellow space-arcade loop, synthesized from a slow chord arpeggio
    // (Am - F - C - G feel) with soft triangle-ish tones and gentle echo.
    // No audio files needed. Toggle with the M key (handled here).

    public static void ToggleMusic() {
        musicEnabled = !musicEnabled;
        if (musicSource != null) musicSource.mute = !musicEnabled;
    }

    public static bool MusicOn { get { return musicEnabled; } }

    /// <summary>Starts the loop if not already playing. Call once when the game starts.</summary>
    public static void StartMusic() {
        Camera cam = Camera.main;
        if (cam == null) return;

        if (musicLoop == null) musicLoop = BuildMusicLoop();

        if (musicGo == null) {
            musicGo = new GameObject("~music");
            UnityEngine.Object.DontDestroyOnLoad(musicGo);
            musicSource = musicGo.AddComponent<AudioSource>();
            musicSource.clip = musicLoop;
            musicSource.loop = true;
            musicSource.volume = 0.32f;
            musicSource.Play();
        }
        musicSource.mute = !musicEnabled;
    }

    static AudioClip BuildMusicLoop() {
        const int sampleRate = 22050;
        float beat = 0.75f;                     // seconds per note
        int[] bass =  { 45, 41, 48, 43 };       // MIDI: A2  F2  C3  G2
        int[] chords = {
            57, 60, 64,   53, 57, 60,   48, 52, 55,   55, 59, 62   // Am  F  C  G
        };
        int notesPerChord = 3;
        int totalNotes = bass.Length * notesPerChord;
        int noteSamples = (int)(sampleRate * beat);
        int n = totalNotes * noteSamples;
        float[] data = new float[n];

        for (int k = 0; k < totalNotes; k++) {
            int chordIdx = k / notesPerChord;
            int noteInChord = k % notesPerChord;

            // Bass note on the first beat of each chord, soft pad notes otherwise.
            bool isBass = (noteInChord == 0);
            int midi = isBass ? bass[chordIdx] : chords[k];
            float freq = 440f * Mathf.Pow(2f, (midi - 69) / 12f);
            float vol = isBass ? 0.30f : 0.16f;

            int start = k * noteSamples;
            int len = isBass ? noteSamples * 2 : noteSamples + noteSamples / 2; // let pads ring into the next beat
            for (int i = 0; i < len && start + i < n; i++) {
                float t = (float)i / sampleRate;
                float env = Mathf.Min(1f, i / (sampleRate * 0.02f))
                          * Mathf.Pow(1f - (float)i / len, 1.6f);
                // triangle-ish timbre: fundamental + quiet octave
                float s = Mathf.Sin(2f * Mathf.PI * freq * t)
                        + 0.25f * Mathf.Sin(2f * Mathf.PI * freq * 2f * t);
                data[start + i] += s * vol * env * 0.5f;
            }
        }

        // Normalize to a safe level.
        float peak = 0f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        if (peak > 0f) {
            float g = 0.85f / peak;
            for (int i = 0; i < n; i++) data[i] *= g;
        }

        AudioClip clip = AudioClip.Create("sfx_music", n, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    const int SampleRate = 22050;

    static AudioClip Tone(float freq, float dur, float vol, float decayPow) {
        int n = Mathf.Max(1, (int)(SampleRate * dur));
        float[] data = new float[n];
        for (int i = 0; i < n; i++) {
            float t = (float)i / SampleRate;
            float env = Mathf.Pow(1f - (float)i / n, decayPow);
            data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * vol * env;
        }
        AudioClip clip = AudioClip.Create("sfx_tone", n, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip Chirp(float f0, float f1, float dur, float vol) {
        int n = Mathf.Max(1, (int)(SampleRate * dur));
        float[] data = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++) {
            float t = (float)i / n;
            float f = Mathf.Lerp(f0, f1, t);
            phase += 2f * Mathf.PI * f / SampleRate;
            data[i] = Mathf.Sin(phase) * vol * (1f - t);
        }
        AudioClip clip = AudioClip.Create("sfx_chirp", n, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip Seq(float[] freqs, float noteDur, float vol) {
        int note = (int)(SampleRate * noteDur);
        int gap = (int)(SampleRate * 0.02f);
        int n = freqs.Length * (note + gap);
        float[] data = new float[n];
        for (int k = 0; k < freqs.Length; k++) {
            int start = k * (note + gap);
            for (int i = 0; i < note; i++) {
                float t = (float)i / SampleRate;
                float env = Mathf.Pow(1f - (float)i / note, 1.8f);
                data[start + i] = Mathf.Sin(2f * Mathf.PI * freqs[k] * t) * vol * env;
            }
        }
        AudioClip clip = AudioClip.Create("sfx_seq", n, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
