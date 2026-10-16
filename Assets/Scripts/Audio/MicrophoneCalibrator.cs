using UnityEngine;


public sealed class MicrophoneCalibrator
{
    public const float SilenceMarginDb = 8f;

    public const float VoicedAboveFloorDb = 10f;

    public const float WarmupSeconds = 2f;
    public const float WarmupRiseDbPerSecond = 25f;

    public const float RiseDbPerSecond = 0.5f;

    public const float SpeechTimeConstant = 15f;

    public const float MinSpeechAboveFloorDb = 12f;
    public const float MaxSpeechAboveFloorDb = 38f;
    public const float InitialSpeechAboveFloorDb = 20f;

    public const float MinFloorDb = -75f;
    public const float MaxFloorDb = -35f;

    public const float MaxScreamDb = -3f;

    private bool _initialized;
    private bool _heardSpeech;
    private float _elapsed;

    public bool IsCalibrated => _initialized;

    public float FloorDb { get; private set; }

    public float SpeechDb { get; private set; }

    public float SilenceDb => FloorDb + SilenceMarginDb;

    public float ScreamDb(float headroomDb) => Mathf.Min(SpeechDb + headroomDb, MaxScreamDb);

    public void Seed(float floorDb)
    {
        FloorDb = Mathf.Clamp(floorDb, MinFloorDb, MaxFloorDb);
        SpeechDb = FloorDb + InitialSpeechAboveFloorDb;
        _heardSpeech = false;
        _initialized = true;
        _elapsed = 0f;
    }

  
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


        if (!_heardSpeech) SpeechDb = FloorDb + InitialSpeechAboveFloorDb;

     
        if (_elapsed >= WarmupSeconds && db > FloorDb + VoicedAboveFloorDb)
        {
            _heardSpeech = true;
            float blend = 1f - Mathf.Exp(-deltaSeconds / SpeechTimeConstant);
            SpeechDb = Mathf.Lerp(SpeechDb, db, blend);
        }
        SpeechDb = Mathf.Clamp(SpeechDb, FloorDb + MinSpeechAboveFloorDb, FloorDb + MaxSpeechAboveFloorDb);
    }

  
    public float ToLevel01(float db, float speechLevel, float screamHeadroomDb)
    {
        return Map(db, SilenceDb, SpeechDb, ScreamDb(screamHeadroomDb), speechLevel);
    }

    
    public static float Map(float db, float silenceDb, float speechDb, float screamDb, float speechLevel)
    {
        speechLevel = Mathf.Clamp(speechLevel, 0.05f, 0.95f);
        if (db <= silenceDb) return 0f;

       
        float speech = Mathf.Max(speechDb, silenceDb + 0.5f);
        float scream = Mathf.Max(screamDb, speech + 0.5f);

        if (db <= speech) return speechLevel * Mathf.InverseLerp(silenceDb, speech, db);
        return Mathf.Clamp01(speechLevel + (1f - speechLevel) * Mathf.InverseLerp(speech, scream, db));
    }
}
