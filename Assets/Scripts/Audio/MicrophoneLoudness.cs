using System;
using System.Collections;
using UnityEngine;

/// <summary>Coarse loudness buckets, handy for gameplay rules (enemy AI, UI icons...).</summary>
public enum VoiceLoudness
{
    Silent = 0,
    Whisper = 1,
    Talk = 2,
    Scream = 3
}

/// <summary>
/// Captures the microphone chosen in the settings (the system default unless the player picked another one) and
/// converts it to a calibrated 0..1 loudness value.
/// Pipeline: raw samples -> RMS -> dB -> remap (silenceDb..screamDb) -> attack/release smoothing.
/// Local-only for now: the networking layer will later send Level01 (as a byte) to the server.
/// </summary>
public class MicrophoneLoudness : MonoBehaviour, IVoiceLevelSource
{
    [Header("Capture")]
    [Tooltip("Leave empty to use the microphone chosen in the settings (or the system default). A name here always wins.")]
    [SerializeField] private string _deviceName = "";
    [SerializeField] private int _sampleRate = 16000;
    [Tooltip("How many of the most recent samples are analysed each frame (512 @16kHz = 32 ms).")]
    [SerializeField, Range(64, 2048)] private int _sampleWindow = 512;

    [Header("Calibration (dB, 0 dB = full scale)")]
    [Tooltip("Level at or below this counts as silence: put it a few dB above your room's noise floor " +
             "(a quiet headset mic idles around -60 dB, so -55 leaves normal speech at -45..-35 dB well above it).")]
    [SerializeField] private float _silenceDb = -55f;
    [Tooltip("Level at or above this counts as a full scream (Level01 = 1).")]
    [SerializeField] private float _screamDb = -12f;

    [Header("Auto calibration (recommended)")]
    [Tooltip("Learns this microphone's noise floor and this player's normal speech level, so the game feels the same on any " +
             "headset, laptop or room. When off, the two fixed dB values above are used.")]
    [SerializeField] private bool _autoCalibrate = true;
    [Tooltip("Loudness (0..1) that normal speech maps to. With the default echo curve, 0.35 lights about 7 m.")]
    [SerializeField, Range(0.1f, 0.7f)] private float _speechLevel = 0.35f;
    [Tooltip("dB above normal speech that count as a full scream (lights the whole cavern).")]
    [SerializeField, Range(10f, 40f)] private float _screamHeadroomDb = 26f;

    [Header("Smoothing")]
    [Tooltip("How fast Level01 rises when you get louder (higher = snappier).")]
    [SerializeField] private float _attackSpeed = 30f;
    [Tooltip("How fast Level01 falls when you get quieter (lower = lingers longer).")]
    [SerializeField] private float _releaseSpeed = 6f;

    [Header("Loudness thresholds (Level01 needed to ENTER each state)")]
    [SerializeField, Range(0f, 1f)] private float _whisperThreshold = 0.12f;
    [SerializeField, Range(0f, 1f)] private float _talkThreshold = 0.40f;
    [SerializeField, Range(0f, 1f)] private float _screamThreshold = 0.78f;
    [Tooltip("Level must drop this far BELOW a threshold to leave the state (avoids flickering).")]
    [SerializeField, Range(0f, 0.2f)] private float _hysteresis = 0.04f;

    [Header("Debug")]
    [SerializeField] private bool _showDebugBar = true;

    /// <summary>Smoothed, calibrated loudness 0..1.</summary>
    public float Level01 { get; private set; }
    /// <summary>
    /// Calibrated loudness 0..1 of the newest ~32 ms window, before the attack/release smoothing. The smoothing makes
    /// short syllables blur together, so anything that needs the exact timing of the voice reads this instead.
    /// </summary>
    public float RawLevel01 => _clip != null ? _lastMicTarget : 0f;
    /// <summary>Current loudness bucket.</summary>
    public VoiceLoudness State { get; private set; } = VoiceLoudness.Silent;
    /// <summary>Instantaneous (unsmoothed) loudness in dB. Useful for calibration.</summary>
    public float CurrentDb { get; private set; } = -80f;
    /// <summary>The noise floor learned from this microphone (dB). Only meaningful with auto calibration on.</summary>
    public float NoiseFloorDb => _calibrator.FloorDb;
    /// <summary>The normal speech level learned from this player (dB). Only meaningful with auto calibration on.</summary>
    public float SpeechDb => _calibrator.SpeechDb;
    /// <summary>True when the microphone is calibrated automatically.</summary>
    public bool IsAutoCalibrating => _autoCalibrate;
    /// <summary>True once the microphone is actually recording.</summary>
    public bool IsCapturing => _clip != null;
    /// <summary>Samples per second of the capture (and of <see cref="ReadNewSamples"/>).</summary>
    public int CaptureSampleRate => _sampleRate;
    /// <summary>The device being captured (null = the system default).</summary>
    public string Device => _device;
    /// <summary>Whether the debug loudness bar may be drawn (it also needs the debug overlay to be on).</summary>
    public bool ShowDebugBar
    {
        get => _showDebugBar;
        set => _showDebugBar = value;
    }

    /// <summary>Raised whenever the loudness bucket changes (Silent/Whisper/Talk/Scream).</summary>
    public event Action<VoiceLoudness> OnStateChanged;

    private AudioClip _clip;
    private string _device;       // null = default device
    private float[] _buffer;
    private bool _calibrating;
    private MicrophoneCalibrator _calibrator = new MicrophoneCalibrator();
    private float _lastMicTarget;
    private const int VoiceBlockSamples = 256;
    private int _voiceReadPosition = -1;
    private float[] _voiceBuffer;

    // A microphone device is a process-wide resource. Remote avatars carry this component too (it is disabled
    // right after spawn), so Start/End must be reference counted or a remote spawn would kill the local capture.
    private sealed class SharedCapture
    {
        public AudioClip Clip;
        public int Users;
    }

    private static readonly System.Collections.Generic.Dictionary<string, SharedCapture> s_captures =
        new System.Collections.Generic.Dictionary<string, SharedCapture>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCaptures()
    {
        s_captures.Clear();
    }

    private static string CaptureKey(string device) => device ?? string.Empty;

    private void OnEnable()
    {
        StartCapture();
        EchoProtocol.Settings.SettingsManager.Changed += OnSettingsChanged;
    }

    private void OnDisable()
    {
        EchoProtocol.Settings.SettingsManager.Changed -= OnSettingsChanged;
        StopCapture();
    }

    // The player picked another microphone in the settings: capture from it (and learn its levels again).
    private void OnSettingsChanged(EchoProtocol.Settings.GameSettings settings)
    {
        string[] devices = Microphone.devices;
        if (devices.Length == 0) return; // nothing to switch to (and no warning on every slider move)

        string wanted = EchoProtocol.Settings.GameSettings.ResolveMicrophone(_deviceName, settings.MicrophoneDevice, devices);
        if (_clip != null && wanted == _device) return;

        StopCapture();
        StartCapture();
    }

    private void StartCapture()
    {
        if (_clip != null) return;

        if (Microphone.devices.Length == 0)
        {
            Debug.LogWarning("[MicrophoneLoudness] No microphone detected.", this);
            return;
        }

        _device = EchoProtocol.Settings.GameSettings.ResolveMicrophone(
            _deviceName, EchoProtocol.Settings.SettingsManager.Current.MicrophoneDevice, Microphone.devices);
        _buffer = new float[_sampleWindow];

        if (!s_captures.TryGetValue(CaptureKey(_device), out SharedCapture capture) || capture.Users <= 0)
        {
            // 1-second looping clip: we only ever read the newest few ms, so a tiny buffer is enough.
            capture = new SharedCapture { Clip = Microphone.Start(_device, true, 1, _sampleRate) };
            s_captures[CaptureKey(_device)] = capture;
            Debug.Log($"[MicrophoneLoudness] Capturing from '{(_device ?? "default")}' ({string.Join(", ", Microphone.devices)}).", this);
        }

        capture.Users++;
        _clip = capture.Clip;

        // A (re)started device may have a different gain: learn it again.
        _calibrator = new MicrophoneCalibrator();
    }

    /// <summary>
    /// Appends to <paramref name="into"/> every raw sample recorded since the previous call, for voice chat. The first call
    /// only starts the cursor. Returns how many samples were added. Only the local owner should call it.
    /// </summary>
    public int ReadNewSamples(System.Collections.Generic.List<float> into)
    {
        if (_clip == null || !Microphone.IsRecording(_device))
        {
            _voiceReadPosition = -1;
            return 0;
        }

        int position = Microphone.GetPosition(_device);
        if (_voiceReadPosition < 0)
        {
            _voiceReadPosition = position;
            return 0;
        }

        int available = EchoProtocol.Voice.VoiceCodec.SamplesSince(_voiceReadPosition, position, _clip.samples);

        // Fixed-size blocks so nothing is allocated per frame; what is left over waits for the next call (at most 16 ms).
        if (_voiceBuffer == null) _voiceBuffer = new float[VoiceBlockSamples];
        int added = 0;
        while (available >= VoiceBlockSamples)
        {
            // GetData wraps around the end of the looping clip on its own.
            _clip.GetData(_voiceBuffer, _voiceReadPosition);
            into.AddRange(_voiceBuffer);
            _voiceReadPosition = (_voiceReadPosition + VoiceBlockSamples) % _clip.samples;
            available -= VoiceBlockSamples;
            added += VoiceBlockSamples;
        }
        return added;
    }

    private void StopCapture()
    {
        if (_clip == null) return;
        _clip = null;
        _voiceReadPosition = -1;
        _lastMicTarget = 0f;

        if (!s_captures.TryGetValue(CaptureKey(_device), out SharedCapture capture)) return;
        capture.Users--;
        if (capture.Users > 0) return;

        s_captures.Remove(CaptureKey(_device));
        if (Microphone.IsRecording(_device)) Microphone.End(_device);
    }

    private void Update()
    {
        // V is the push-to-talk echo key now (see EchoVisionController), so it no longer fakes a voice burst.
        float target = (_clip != null) ? ReadTargetLevel() : 0f;

        // Fast attack / slow release so shouts register instantly but light fades out naturally.
        float speed = target > Level01 ? _attackSpeed : _releaseSpeed;
        Level01 = Mathf.Lerp(Level01, target, 1f - Mathf.Exp(-speed * Time.deltaTime));

        UpdateState();
    }

    /// <summary>Reads the newest samples from the mic and returns the unsmoothed 0..1 level.</summary>
    private float ReadTargetLevel()
    {
        int pos = Microphone.GetPosition(_device);
        int start = pos - _sampleWindow;
        // Mic not started yet: nothing to read.
        if (pos <= 0 && !Microphone.IsRecording(_device)) return 0f;
        // The write head just looped around the 1 s clip, so the newest window is split across the seam.
        // Hold the previous reading for those ~32 ms instead of dropping to silence once per second.
        if (start < 0) return _lastMicTarget;

        _clip.GetData(_buffer, start);

        float sum = 0f;
        for (int i = 0; i < _buffer.Length; i++) sum += _buffer[i] * _buffer[i];
        float rms = Mathf.Sqrt(sum / _buffer.Length);

        CurrentDb = rms > 1e-6f ? 20f * Mathf.Log10(rms) : -80f;
        if (_autoCalibrate)
        {
            _calibrator.Update(CurrentDb, Time.unscaledDeltaTime);
            _lastMicTarget = _calibrator.ToLevel01(CurrentDb, _speechLevel, _screamHeadroomDb);
            LogCalibrationWhenItChanges();
        }
        else
        {
            _lastMicTarget = DbToLevel01(CurrentDb, _silenceDb, _screamDb);
        }
        return _lastMicTarget;
    }

    /// <summary>Remaps a dB reading to 0..1: at or below <paramref name="silenceDb"/> is 0, at or above <paramref name="screamDb"/> is 1.</summary>
    public static float DbToLevel01(float db, float silenceDb, float screamDb)
    {
        return db <= silenceDb ? 0f : Mathf.Clamp01(Mathf.InverseLerp(silenceDb, screamDb, db));
    }

    /// <summary>
    /// Loudness bucket for <paramref name="level"/> given the bucket the player is currently in.
    /// Entering a bucket needs its threshold; leaving it needs the level to fall <paramref name="hysteresis"/> below it.
    /// </summary>
    public static VoiceLoudness ResolveState(VoiceLoudness current, float level, float whisper, float talk, float scream, float hysteresis)
    {
        VoiceLoudness s = current;

        // Climb while we exceed the next bucket's threshold...
        while (s < VoiceLoudness.Scream && level >= Threshold(s + 1, whisper, talk, scream)) s++;
        // ...and fall while we are clearly below the current bucket's threshold.
        while (s > VoiceLoudness.Silent && level < Threshold(s, whisper, talk, scream) - hysteresis) s--;

        return s;
    }

    private static float Threshold(VoiceLoudness state, float whisper, float talk, float scream)
    {
        switch (state)
        {
            case VoiceLoudness.Whisper: return whisper;
            case VoiceLoudness.Talk: return talk;
            case VoiceLoudness.Scream: return scream;
            default: return 0f;
        }
    }

    private float _loggedFloorDb = float.NaN;
    private float _loggedSpeechDb;

    // One line whenever the learned values move noticeably: enough to see from a player log what a microphone was taught.
    private void LogCalibrationWhenItChanges()
    {
        bool first = float.IsNaN(_loggedFloorDb);
        if (!first && Mathf.Abs(_calibrator.FloorDb - _loggedFloorDb) < 4f && Mathf.Abs(_calibrator.SpeechDb - _loggedSpeechDb) < 4f) return;

        _loggedFloorDb = _calibrator.FloorDb;
        _loggedSpeechDb = _calibrator.SpeechDb;
        Debug.Log($"[MicrophoneLoudness] Calibration: noise floor {_loggedFloorDb:F1} dB, normal speech {_loggedSpeechDb:F1} dB");
    }

    private void UpdateState()
    {
        VoiceLoudness s = ResolveState(State, Level01, _whisperThreshold, _talkThreshold, _screamThreshold, _hysteresis);
        if (s == State) return;
        State = s;
        OnStateChanged?.Invoke(State);
    }

    /// <summary>
    /// Stay quiet and call this (right-click the component -> Calibrate Silence, or from a UI button).
    /// Samples room noise for a few seconds and places the silence floor just above it.
    /// </summary>
    [ContextMenu("Calibrate Silence")]
    public void CalibrateSilence()
    {
        if (!_calibrating && _clip != null) StartCoroutine(CalibrateRoutine(2f));
    }

    private IEnumerator CalibrateRoutine(float seconds)
    {
        _calibrating = true;
        float sum = 0f;
        int count = 0;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            sum += CurrentDb;
            count++;
            yield return null;
        }
        if (count > 0 && _autoCalibrate)
        {
            // Start the automatic calibration again from the measured room noise.
            _calibrator.Seed(sum / count);
            Debug.Log($"[MicrophoneLoudness] Noise floor re-measured: {_calibrator.FloorDb:F1} dB");
        }
        else if (count > 0)
        {
            _silenceDb = Mathf.Min(sum / count + 6f, _screamDb - 10f); // +6 dB margin above the room noise
            Debug.Log($"[MicrophoneLoudness] New silence floor: {_silenceDb:F1} dB");
        }
        _calibrating = false;
    }

    private void OnGUI()
    {
        if (!_showDebugBar || !DebugOverlay.Visible) return;

        const float w = 240f, h = 18f;
        var r = new Rect(10f, 10f, w, h);
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = Color.Lerp(Color.green, Color.red, Level01);
        GUI.DrawTexture(new Rect(r.x, r.y, w * Level01, h), Texture2D.whiteTexture);
        GUI.color = Color.white;
        string learned = _autoCalibrate ? $"  |  floor {_calibrator.FloorDb:F0}  speech {_calibrator.SpeechDb:F0}" : "";
        GUI.Label(new Rect(r.x, r.y + h, w + 220f, 22f), $"{State}  |  {CurrentDb:F1} dB  |  {Level01:F2}{learned}");
    }
}
