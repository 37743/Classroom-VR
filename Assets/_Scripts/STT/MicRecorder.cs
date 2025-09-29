/// <summary>
/// Appathon CSE-EJUST Challenge Summer 2025
/// 
/// Microphone recorder with robust Voice Activity Detection (VAD) and integration with RunWhisper.
/// Captures microphone input in a looped AudioClip, processes audio chunks to detect speech segments
/// 
/// Usage:
/// - Attach this script to a GameObject in your scene.
/// - Assign a RunWhisper component to the RunWhisper field.
/// - Optionally assign an AudioSource component (e.g., on the Main Camera) to hear playback of captured segments.
/// - Use the StartListening, StopListening, and ToggleListening methods to control recording.
/// - The script will automatically finalize and send speech segments to RunWhisper for transcription.
/// 
/// Known Issues:
/// - Sampling issue with AudioClip when audio length is not a multiple of 1 second at 16000Hz.
///   To avoid glitches, ensure your microphone input length is a multiple of 1 second (e.g., 5s, 10s, 15s).
/// </summary>
using System.Collections.Generic;
using UnityEngine;

public class MicRecorder : MonoBehaviour
{
    private const int CLIP_FREQUENCY = 16000;
    [SerializeField, Range(5, 120)] private int loopClipLengthSecs = 30;

    [Header("Voice Activity Detection (VAD)")]
    [SerializeField, Range(0f, 0.1f)] private float vadSilenceThreshold = 0.02f;
    [SerializeField, Range(0f, 0.2f)] private float vadStartThreshold = 0.03f;
    [SerializeField, Range(0.1f, 3f)] private float silenceHangSeconds = 1.0f;
    [SerializeField, Range(0f, 1f)] private float minSpeechSeconds = 0.35f;
    [SerializeField, Range(0f, 0.5f)] private float preRollSeconds = 0.20f;

    [Header("Trim Settings (post-capture)")]
    [SerializeField, Range(0.0f, 0.1f)] private float trimSilenceThreshold = 0.02f;
    [SerializeField, Range(0.0f, 1.0f)] private float trimMinSilenceLength = 0.5f;

    [Header("Whisper Hook")]
    [SerializeField] private RunWhisper RunWhisper;

    [Header("Auto Stop")]
    [Tooltip("Automatically stop the microphone after each successful transcription.")]
    [SerializeField] private bool autoStopAfterTranscribe = true;

    private AudioSource audioSource;
    private AudioClip micLoopClip;
    private string deviceName;
    private bool isListening;

    private int lastReadPos;

    private readonly List<float> currentSegment = new List<float>(64 * 1024);
    private float secondsOfContinuousSilence;

    private bool inSpeech = false;
    private bool segmentHasVoice = false;
    private readonly List<float> preRollBuffer = new List<float>(8192);

    public bool IsListening => isListening;

    private void Start()
    {
        audioSource = Camera.main ? Camera.main.GetComponent<AudioSource>() : null;

        if (Microphone.devices.Length == 0) { Debug.LogError("No microphone found."); return; }

        deviceName = Microphone.devices[0];
        for (int i = 0; i < Microphone.devices.Length; i++)
        {
            string up = Microphone.devices[i].ToUpperInvariant();
            if (up.Contains("ANDROID") || up.Contains("OCULUS")) { deviceName = Microphone.devices[i]; break; }
        }
        Debug.Log($"Using Microphone: {deviceName}");
    }

    private void OnDestroy()
    {
        StopListening();
    }

    private void Update()
    {
        if (!isListening || micLoopClip == null) return;

        int micPos = Microphone.GetPosition(deviceName);
        if (micPos < 0) return;

        int channels = micLoopClip.channels;
        int totalFrames = micLoopClip.samples;
        int totalInterleaved = totalFrames * channels;

        int writePos = micPos * channels;
        int newCount = (writePos - lastReadPos + totalInterleaved) % totalInterleaved;

        if (newCount > 0)
        {
            // tail (from lastReadPos to end)
            int tail = Mathf.Min(newCount, totalInterleaved - lastReadPos);
            if (tail > 0)
            {
                float[] chunk1 = new float[tail];
                micLoopClip.GetData(chunk1, lastReadPos / channels);
                AppendAndVAD(chunk1, channels);
            }

            // wrap (from start to writePos)
            int remaining = newCount - tail;
            if (remaining > 0)
            {
                float[] chunk2 = new float[remaining];
                micLoopClip.GetData(chunk2, 0);
                AppendAndVAD(chunk2, channels);
            }

            lastReadPos = writePos;
        }

        // Finalize only if we actually had speech in this segment
        if (segmentHasVoice && secondsOfContinuousSilence >= silenceHangSeconds)
        {
            if (TryFinalizeSegment(forceEvenIfShort: false, allowAutoStop: true))
            {
                secondsOfContinuousSilence = 0f;
                inSpeech = false;
                segmentHasVoice = false;
                preRollBuffer.Clear();
            }
        }
    }

    [ContextMenu("Start Listening")]
    public void StartListening()
    {
        if (isListening) return;
        if (!RunWhisper || !RunWhisper.IsReady) { Debug.LogWarning("Whisper not ready; cannot start listening yet."); return; }

        micLoopClip = Microphone.Start(deviceName, true, loopClipLengthSecs, CLIP_FREQUENCY);
        if (micLoopClip == null) { Debug.LogError("Failed to start microphone."); return; }

        isListening = true;
        lastReadPos = 0;

        currentSegment.Clear();
        secondsOfContinuousSilence = 0f;
        inSpeech = false;
        segmentHasVoice = false;
        preRollBuffer.Clear();

        Debug.Log("Listening...");
    }

    [ContextMenu("Stop Listening")]
    public void StopListening()
    {
        if (!isListening) return;

        TryFinalizeSegment(forceEvenIfShort: false, allowAutoStop: false);

        StopMicOnly();
        Debug.Log("Stopped listening.");
    }

    [ContextMenu("Toggle Listening")]
    public void ToggleListening()
    {
        if (isListening) StopListening();
        else StartListening();
    }

    public bool FinalizePendingSegment(bool forceEvenIfShort = false)
    {
        return TryFinalizeSegment(forceEvenIfShort, allowAutoStop: true);
    }

    private void AppendAndVAD(float[] samples, int channels)
    {
        float chunkMax = 0f;
        for (int i = 0; i < samples.Length; i += channels)
        {
            float a = Mathf.Abs(samples[i]);
            if (a > chunkMax) chunkMax = a;
        }

        float secondsInChunk = (float)samples.Length / (channels * CLIP_FREQUENCY);

        if (!inSpeech)
        {
            if (chunkMax >= vadStartThreshold)
            {
                inSpeech = true;
                segmentHasVoice = true;
                if (preRollBuffer.Count > 0)
                {
                    currentSegment.AddRange(preRollBuffer);
                    preRollBuffer.Clear();
                }
                currentSegment.AddRange(samples);
                secondsOfContinuousSilence = 0f;
            }
            else
            {
                preRollBuffer.AddRange(samples);
                int cap = Mathf.Max(1, Mathf.RoundToInt(preRollSeconds * CLIP_FREQUENCY * channels));
                int excess = preRollBuffer.Count - cap;
                if (excess > 0) preRollBuffer.RemoveRange(0, excess);
            }
        }
        else
        {
            currentSegment.AddRange(samples);
            if (chunkMax < vadSilenceThreshold) secondsOfContinuousSilence += secondsInChunk;
            else secondsOfContinuousSilence = 0f;
        }
    }

    private bool TryFinalizeSegment(bool forceEvenIfShort, bool allowAutoStop = true)
    {
        int channels = micLoopClip ? micLoopClip.channels : 1;

        if (currentSegment.Count == 0) return false;

        float dur = (float)currentSegment.Count / (channels * CLIP_FREQUENCY);

        if (!segmentHasVoice && !forceEvenIfShort)
        {
            currentSegment.Clear();
            return false;
        }

        if (!forceEvenIfShort && dur < minSpeechSeconds)
        {
            currentSegment.Clear();
            return false;
        }

        var segmentArray = currentSegment.ToArray();
        currentSegment.Clear();

        AudioClip clip = AudioClip.Create("MicSegment", segmentArray.Length / channels, channels, CLIP_FREQUENCY, false);
        clip.SetData(segmentArray, 0);

        clip = TrimSilence(clip, trimSilenceThreshold, trimMinSilenceLength);
        if (clip == null) return false;
        if (clip.length < minSpeechSeconds && !forceEvenIfShort) return false;

        if (clip.channels > 1) clip = ConvertToMono(clip);

        if (audioSource != null) { audioSource.clip = clip; audioSource.Play(); }

        if (RunWhisper && RunWhisper.IsReady)
        {
            RunWhisper.Transcribe(clip);

            if (autoStopAfterTranscribe && allowAutoStop && isListening)
            {
                StopMicOnly();
                Debug.Log("Stopped listening (auto after transcribe).");
            }
        }

        return true;
    }

    private void StopMicOnly()
    {
        if (!string.IsNullOrEmpty(deviceName))
        {
            // Guard against double-end
            try { Microphone.End(deviceName); } catch { /* ignore */ }
        }

        isListening = false;
        micLoopClip = null;
        lastReadPos = 0;

        currentSegment.Clear();
        secondsOfContinuousSilence = 0f;
        inSpeech = false;
        segmentHasVoice = false;
        preRollBuffer.Clear();
    }

    private static AudioClip TrimSilence(AudioClip source, float silenceThreshold, float minSilenceLen)
    {
        if (source == null) return null;

        int channels = source.channels;
        int frequency = source.frequency;
        int samples = source.samples;

        float[] data = new float[samples * channels];
        source.GetData(data, 0);

        bool inSilence = false;
        float silenceStart = 0f;
        var trimmed = new List<float>(data.Length);

        for (int i = 0; i < data.Length; i += channels)
        {
            float volume = Mathf.Abs(data[i]);
            if (volume < silenceThreshold)
            {
                if (!inSilence)
                {
                    inSilence = true;
                    silenceStart = i / (float)(frequency * channels);
                }
            }
            else
            {
                if (inSilence)
                {
                    float silenceDur = i / (float)(frequency * channels) - silenceStart;
                    if (silenceDur < minSilenceLen)
                    {
                        int start = Mathf.FloorToInt(silenceStart * frequency * channels);
                        for (int j = start; j < i; j++) trimmed.Add(data[j]);
                    }
                    inSilence = false;
                }
                for (int c = 0; c < channels; c++) trimmed.Add(data[i + c]);
            }
        }

        if (trimmed.Count == 0) return null;

        AudioClip trimmedClip = AudioClip.Create(source.name + "_Trimmed", trimmed.Count / channels, channels, frequency, false);
        trimmedClip.SetData(trimmed.ToArray(), 0);
        return trimmedClip;
    }

    private static AudioClip ConvertToMono(AudioClip source)
    {
        if (source.channels == 1) return source;

        int channels = source.channels;
        int samples = source.samples;
        float[] src = new float[samples * channels];
        source.GetData(src, 0);

        float[] mono = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float sum = 0f;
            for (int c = 0; c < channels; c++) sum += src[i * channels + c];
            mono[i] = sum / channels;
        }

        AudioClip monoClip = AudioClip.Create(source.name + "_Mono", samples, 1, source.frequency, false);
        monoClip.SetData(mono, 0);
        return monoClip;
    }
}
