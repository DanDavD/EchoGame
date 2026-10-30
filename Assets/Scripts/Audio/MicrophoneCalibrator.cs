using UnityEngine;

/// <summary>
/// Learns what "quiet", "normal speech" and "a scream" mean on THIS microphone, so the game feels the same on every
/// headset, laptop and room without any manual setup. Pure maths (no Unity objects), fed with one decibel reading per frame.
///
///  * The noise floor follows the quietest recent readings: it drops at once and rises slowly, so speech is never mistaken
///    for the room, while a room that really gets noisier is followed within a minute.
///  * The speech level is a slow average of the readings that clearly rise above the floor: it learns how loudly this
///    player talks.
///  * The silence point sits a few dB above the floor and a scream is a fixed headroom above normal speech. Speech always
///    maps to the same loudness, whatever the microphone's gain.
/// </summary>
public sealed class MicrophoneCalibrator
{
    /// <summary>dB above the noise floor where silence ends (covers the floor's natural fluctuation).</summary>
    public const float SilenceMarginDb = 8f;

    /// <summary>A reading counts as voiced when it is this many dB above the noise floor.</summary>
    public const float VoicedAboveFloorDb = 10f;

    /// <summary>Seconds at the start during which the floor may rise fast, to find the real room noise.</summary>
    public const float WarmupSeconds = 2f;
    public const float WarmupRiseDbPerSecond = 25f;

    /// <summary>How fast the floor may rise afterwards (dB per second).</summary>
    public const float RiseDbPerSecond = 0.5f;

    /// <summary>Time constant (seconds) of the speech-level average.</summary>
    public const float SpeechTimeConstant = 15f;

    /// <summary>Bounds of the speech level above the floor, and the guess before any speech has been heard.</summary>
    public const float MinSpeechAboveFloorDb = 12f;
    public const float MaxSpeechAboveFloorDb = 38f;
    public const float InitialSpeechAboveFloorDb = 20f;

    /// <summary>The floor never leaves this range (a muted mic reads about -80 dB; a very hot one never goes above it).</summary>
    public const float MinFloorDb = -75f;
    public const float MaxFloorDb = -35f;

    /// <summary>The scream point never goes above this (digital full scale is 0 dB).</summary>
    public const float MaxScreamDb = -3f;

    private bool _initialized;
    private bool _heardSpeech;
    private float _elapsed;

    /// <summary>True once the first reading has been seen.</summary>
    public bool IsCalibrated => _initialized;

    /// <summary>Current estimate of the room's noise floor (dB).</summary>
    public float FloorDb { get; private set; }

    /// <summary>Current estimate of this player's normal speech level (dB).</summary>
    public float SpeechDb { get; private set; }

    /// <summary>Readings at or below this are silence.</summary>
    public float SilenceDb => FloorDb + SilenceMarginDb;

    /// <summary>The scream point for a headroom of <paramref name="headroomDb"/> above normal speech.</summary>
    public float ScreamDb(float headroomDb) => Mathf.Min(SpeechDb + headroomDb, MaxScreamDb);

    /// <summary>Starts again from a known noise floor (for example after the player asked for a manual silence calibration).</summary>
    public void Seed(float floorDb)
    {
        FloorDb = Mathf.Clamp(floorDb, MinFloorDb, MaxFloorDb);
        SpeechDb = FloorDb + InitialSpeechAboveFloorDb;
        _heardSpeech = false;
        _initialized = true;
        _elapsed = 0f;
    }

    /// <summary>Feeds one reading (<paramref name="db"/>) taken <paramref name="deltaSeconds"/> after the previous one.</summary>
    public void Update(float db, float deltaSeconds)
    {
        if (deltaSeconds <= 0f) return;

        if (!_initialized) Seed(db);
        _elapsed += deltaSeconds;

        // Noise floor: down at once, up slowly (fast during the warm-up).
        if (db <= FloorDb)
        {
            FloorDb = db;
        }
        else
        {
            float rise = _elapsed < WarmupSeconds ? WarmupRiseDbPerSecond : RiseDbPerSecond;
            FloorDb = Mathf.Min(db, FloorDb + rise * deltaSeconds);
        }
        FloorDb = Mathf.Clamp(FloorDb, MinFloorDb, MaxFloorDb);

        // Speech level: until the player has said something, the guess simply follows the floor (a floor that is still
        // settling during the warm-up must not leave the guess stuck at a bound). Then a slow average of the voiced readings.
        if (!_heardSpeech) SpeechDb = FloorDb + InitialSpeechAboveFloorDb;

        // Nothing counts as speech during the warm-up: the floor is still climbing from its first reading, and the room's
        // own noise would look like a voice.
        if (_elapsed >= WarmupSeconds && db > FloorDb + VoicedAboveFloorDb)
        {
            _heardSpeech = true;
            float blend = 1f - Mathf.Exp(-deltaSeconds / SpeechTimeConstant);
            SpeechDb = Mathf.Lerp(SpeechDb, db, blend);
        }
        SpeechDb = Mathf.Clamp(SpeechDb, FloorDb + MinSpeechAboveFloorDb, FloorDb + MaxSpeechAboveFloorDb);
    }

    /// <summary>
    /// Loudness 0..1 of <paramref name="db"/>: 0 at or below silence, exactly <paramref name="speechLevel"/> at normal speech,
    /// 1 at the scream point and above. Linear in dB between those points.
    /// </summary>
    public float ToLevel01(float db, float speechLevel, float screamHeadroomDb)
    {
        return Map(db, SilenceDb, SpeechDb, ScreamDb(screamHeadroomDb), speechLevel);
    }

    /// <summary>The piecewise mapping itself, exposed for tests and tools.</summary>
    public static float Map(float db, float silenceDb, float speechDb, float screamDb, float speechLevel)
    {
        speechLevel = Mathf.Clamp(speechLevel, 0.05f, 0.95f);
        if (db <= silenceDb) return 0f;

        // Keep the points in order even if the estimates collide (a very quiet or very hot microphone).
        float speech = Mathf.Max(speechDb, silenceDb + 0.5f);
        float scream = Mathf.Max(screamDb, speech + 0.5f);

        if (db <= speech) return speechLevel * Mathf.InverseLerp(silenceDb, speech, db);
        return Mathf.Clamp01(speechLevel + (1f - speechLevel) * Mathf.InverseLerp(speech, scream, db));
    }
}
