using UnityEngine;

public enum Sfx
{
    Click,
    Correct,
    Wrong,
    DoorOpen,
    Pickup,
    Hint,
    Damage
}

/// <summary>
/// Persistent audio hub (music, ambient, effects). Created automatically before the first scene.
/// Until real soundtrack files exist, effects and the dark drone are synthesised in code.
/// The radio is a separate system (RadioController) with its own volume.
/// </summary>
public class AudioManager : MonoBehaviour
{
    private const int SampleRate = 44100;

    public static AudioManager Instance { get; private set; }

    private AudioSource musicSource;
    private AudioSource ambientSource;
    private AudioSource sfxSource;
    private AudioClip[] sfxClips;
    private float musicDuck = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        new GameObject("AudioManager").AddComponent<AudioManager>();
    }

    public static void Play(Sfx sfx)
    {
        if (Instance != null) Instance.PlayClip(sfx);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        GameSettings.ApplyDisplay();

        musicSource = CreateSource("Music", true);
        ambientSource = CreateSource("Ambient", true);
        sfxSource = CreateSource("Sfx", false);

        musicSource.clip = BuildDrone("MusicPad", new[] { 55f, 110f, 131f, 165f }, 0.25f, 2f, 0.11f);
        ambientSource.clip = BuildDrone("AmbientRumble", new[] { 41f, 62f }, 0.125f, 1f, 0.2f);
        musicSource.Play();
        ambientSource.Play();

        BuildEffects();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        // The built-in soundtrack steps aside while the player's own radio is playing.
        var radio = RadioController.Instance;
        float target = radio != null && radio.State == RadioState.Playing ? 0f : 1f;
        musicDuck = Mathf.MoveTowards(musicDuck, target, Time.unscaledDeltaTime * 1.5f);

        musicSource.volume = GameSettings.Music * musicDuck;
        ambientSource.volume = GameSettings.Ambient * 0.6f;
    }

    private void PlayClip(Sfx sfx)
    {
        var clip = sfxClips[(int)sfx];
        if (clip != null) sfxSource.PlayOneShot(clip, GameSettings.Sfx);
    }

    private AudioSource CreateSource(string sourceName, bool loop)
    {
        var go = new GameObject(sourceName);
        go.transform.SetParent(transform, false);
        var source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f;
        source.ignoreListenerPause = true;
        return source;
    }

    // ------------------------------------------------------- synthesis

    private void BuildEffects()
    {
        var noise = new System.Random(7);
        float Noise() => (float)(noise.NextDouble() * 2.0 - 1.0);
        const float Tau = Mathf.PI * 2f;

        sfxClips = new AudioClip[7];

        sfxClips[(int)Sfx.Click] = Synth("Click", 0.05f, (t, p) =>
            Mathf.Sin(Tau * 1400f * t) * (1f - p) * (1f - p) * 0.4f);

        sfxClips[(int)Sfx.Correct] = Synth("Correct", 0.36f, (t, p) =>
        {
            bool second = t >= 0.16f;
            float f = second ? 784f : 523.25f;
            float lt = second ? t - 0.16f : t;
            return Mathf.Sin(Tau * f * t) * Mathf.Exp(-lt * 8f) * 0.45f;
        });

        sfxClips[(int)Sfx.Wrong] = Synth("Wrong", 0.35f, (t, p) =>
            ((t * 110f) % 1f * 2f - 1f) * 0.3f * (1f - p));

        sfxClips[(int)Sfx.DoorOpen] = Synth("DoorOpen", 0.5f, (t, p) =>
            (Mathf.Sin(Tau * (80f + 60f * (1f - p)) * t) * 0.5f + Noise() * 0.15f) * (1f - p));

        float[] arp = { 440f, 554.37f, 659.25f, 880f };
        sfxClips[(int)Sfx.Pickup] = Synth("Pickup", 0.42f, (t, p) =>
        {
            int idx = Mathf.Min(3, (int)(t / 0.1f));
            float lt = t - idx * 0.1f;
            return Mathf.Sin(Tau * arp[idx] * t) * Mathf.Exp(-lt * 9f) * 0.45f;
        });

        sfxClips[(int)Sfx.Hint] = Synth("Hint", 0.3f, (t, p) =>
            (Mathf.Sin(Tau * 880f * t) + 0.6f * Mathf.Sin(Tau * 1320f * t)) * 0.22f * (1f - p));

        sfxClips[(int)Sfx.Damage] = Synth("Damage", 0.2f, (t, p) =>
            (Noise() * 0.4f + Mathf.Sin(Tau * 110f * t) * 0.4f) * (1f - p));
    }

    /// <summary>A loopable chord. Every frequency completes whole cycles in 8 seconds, so there is no click.</summary>
    private static AudioClip BuildDrone(string clipName, float[] freqs, float tremoloHz, float tremoloDepthScale, float amplitude)
    {
        const float Length = 8f;
        const float Tau = Mathf.PI * 2f;

        return Synth(clipName, Length, (t, p) =>
        {
            float v = 0f;
            foreach (float f in freqs) v += Mathf.Sin(Tau * f * t);
            float trem = 0.75f + 0.25f * Mathf.Sin(Tau * tremoloHz * t) * tremoloDepthScale * 0.5f;
            return v / freqs.Length * trem * amplitude * 2f;
        });
    }

    private static AudioClip Synth(string clipName, float seconds, System.Func<float, float, float> sample)
    {
        int count = Mathf.CeilToInt(seconds * SampleRate);
        var data = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)SampleRate;
            data[i] = Mathf.Clamp(sample(t, t / seconds), -1f, 1f);
        }

        var clip = AudioClip.Create(clipName, count, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
