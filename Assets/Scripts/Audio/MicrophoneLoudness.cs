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
/// Captures the default microphone and converts it to a calibrated 0..1 loudness value.
/// Pipeline: raw samples -> RMS -> dB -> remap (silenceDb..screamDb) -> attack/release smoothing.
/// Local-only for now: the networking layer will later send Level01 (as a byte) to the server.
/// </summary>
public class MicrophoneLoudness : MonoBehaviour, IVoiceLevelSource
{
    [Header("Capture")]
    [Tooltip("Leave empty to use the system default microphone.")]
    [SerializeField] private string _deviceName = "";
    [SerializeField] private int _sampleRate = 16000;
    [Tooltip("How many of the most recent samples are analysed each frame (512 @16kHz = 32 ms).")]
    [SerializeField, Range(64, 2048)] private int _sampleWindow = 512;

    [Header("Calibration (dB, 0 dB = full scale)")]
    [Tooltip("Level at or below this counts as silence (your room noise floor).")]
    [SerializeField] private float _silenceDb = -50f;
    [Tooltip("Level at or above this counts as a full scream (Level01 = 1).")]
    [SerializeField] private float _screamDb = -12f;

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
    /// <summary>Current loudness bucket.</summary>
    public VoiceLoudness State { get; private set; } = VoiceLoudness.Silent;
    /// <summary>Instantaneous (unsmoothed) loudness in dB. Useful for calibration.</summary>
    public float CurrentDb { get; private set; } = -80f;
    /// <summary>True once the microphone is actually recording.</summary>
    public bool IsCapturing => _clip != null;

    /// <summary>Raised whenever the loudness bucket changes (Silent/Whisper/Talk/Scream).</summary>
    public event Action<VoiceLoudness> OnStateChanged;

    private AudioClip _clip;
    private string _device;       // null = default device
    private float[] _buffer;
    private bool _calibrating;

    private void OnEnable()
    {
        StartCapture();
    }

    private void OnDisable()
    {
        StopCapture();
    }

    private void StartCapture()
    {
        if (Microphone.devices.Length == 0)
        {
            Debug.LogWarning("[MicrophoneLoudness] No microphone detected.", this);
            return;
        }

        _device = string.IsNullOrEmpty(_deviceName) ? null : _deviceName;
        _buffer = new float[_sampleWindow];

        // 1-second looping clip: we only ever read the newest few ms, so a tiny buffer is enough.
        _clip = Microphone.Start(_device, true, 1, _sampleRate);
        Debug.Log($"[MicrophoneLoudness] Capturing from '{(_device ?? "default")}' ({string.Join(", ", Microphone.devices)}).", this);
    }

    private void StopCapture()
    {
        if (_clip == null) return;
        if (Microphone.IsRecording(_device)) Microphone.End(_device);
        _clip = null;
    }

    private void Update()
    {
        if (_clip == null) return;

        float target = ReadTargetLevel();
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
        // Mic not started yet, or we just looped around the clip: keep the previous value this frame.
        if (start < 0) return Level01;

        _clip.GetData(_buffer, start);

        float sum = 0f;
        for (int i = 0; i < _buffer.Length; i++) sum += _buffer[i] * _buffer[i];
        float rms = Mathf.Sqrt(sum / _buffer.Length);

        CurrentDb = rms > 1e-6f ? 20f * Mathf.Log10(rms) : -80f;
        return Mathf.InverseLerp(_silenceDb, _screamDb, CurrentDb);
    }

    private void UpdateState()
    {
        VoiceLoudness s = State;

        // Climb while we exceed the next bucket's threshold...
        while (s < VoiceLoudness.Scream && Level01 >= ThresholdFor(s + 1)) s++;
        // ...and fall while we are clearly below the current bucket's threshold.
        while (s > VoiceLoudness.Silent && Level01 < ThresholdFor(s) - _hysteresis) s--;

        if (s == State) return;
        State = s;
        OnStateChanged?.Invoke(State);
    }

    private float ThresholdFor(VoiceLoudness state)
    {
        switch (state)
        {
            case VoiceLoudness.Whisper: return _whisperThreshold;
            case VoiceLoudness.Talk: return _talkThreshold;
            case VoiceLoudness.Scream: return _screamThreshold;
            default: return 0f;
        }
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
        if (count > 0)
        {
            _silenceDb = Mathf.Min(sum / count + 6f, _screamDb - 10f); // +6 dB margin above the room noise
            Debug.Log($"[MicrophoneLoudness] New silence floor: {_silenceDb:F1} dB");
        }
        _calibrating = false;
    }

    private void OnGUI()
    {
        if (!_showDebugBar) return;

        const float w = 240f, h = 18f;
        var r = new Rect(10f, 10f, w, h);
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = Color.Lerp(Color.green, Color.red, Level01);
        GUI.DrawTexture(new Rect(r.x, r.y, w * Level01, h), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(r.x, r.y + h, w + 100f, 22f), $"{State}  |  {CurrentDb:F1} dB  |  {Level01:F2}");
    }
}
