/// <summary>
/// PiperDriver.cs
/// Unity component to drive Piper TTS and OVRLipSync for lip-synced speech.
/// Specifically modified for Classroom VR from original source: https://github.com/Macoron/piper.unity/tree/master
/// Following the license: GPL v3
/// </summary>

using UnityEngine;
using System;
using System.Threading.Tasks;
using OVR;

namespace Piper.Samples
{
    public class PiperDriver : MonoBehaviour
    {
        public PiperManager piper;
        public OVRLipSyncContext lipSyncContext;

        private AudioSource _source;

        public bool IsSpeaking { get; private set; }
        public event Action OnSpeechEnded;

        private Coroutine _lipSyncRoutine;
        private Coroutine _endMonitorRoutine;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
        }

        public async Task Speak(string text)
        {
            if (_source == null)
            {
                Debug.LogError("AudioSource is not assigned.");
                return;
            }

            if (lipSyncContext == null)
            {
                Debug.LogWarning("OVRLipSyncContext not assigned. Lip sync will be skipped.");
            }

            try
            {
                Debug.Log($"Speaking: {text}");

                InterruptCurrentSpeech();

                var audioClip = await piper.TextToSpeech(text);
                if (audioClip == null)
                {
                    Debug.LogWarning("No audio clip generated to play.");
                    return;
                }

                _source.clip = audioClip;
                _source.Play();

                IsSpeaking = true;

                // Start lip sync processing
                if (lipSyncContext != null)
                    _lipSyncRoutine = StartCoroutine(ProcessLipSyncDuringPlayback());

                _endMonitorRoutine = StartCoroutine(WaitForPlaybackEndAndNotify());
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error in Speak method: {ex.Message}");
            }
        }

        // === New: stop current speech without firing "ended" event ===
        private void InterruptCurrentSpeech()
        {
            if (_endMonitorRoutine != null)
            {
                StopCoroutine(_endMonitorRoutine);
                _endMonitorRoutine = null;
            }

            if (_lipSyncRoutine != null)
            {
                StopCoroutine(_lipSyncRoutine);
                _lipSyncRoutine = null;
            }

            if (_source != null && _source.isPlaying)
                _source.Stop();

            if (lipSyncContext != null)
                lipSyncContext.ResetContext();

            IsSpeaking = false;
        }

        private System.Collections.IEnumerator ProcessLipSyncDuringPlayback()
        {
            if (_source.clip == null) yield break;

            int sampleRate = _source.clip.frequency;
            int channels = _source.clip.channels;
            int samplesPerFrame = Mathf.Max(1, sampleRate / 60);
            float[] sampleBuffer = new float[samplesPerFrame * channels];

            while (_source != null && _source.isPlaying && _source.clip != null)
            {
                if (lipSyncContext != null)
                {
                    int currentSample = _source.timeSamples;
                    if (currentSample + samplesPerFrame <= _source.clip.samples)
                    {
                        _source.clip.GetData(sampleBuffer, currentSample);
                        lipSyncContext.ProcessAudioSamples(sampleBuffer, channels);
                    }
                }
                yield return null;
            }

            _lipSyncRoutine = null;
        }

        private System.Collections.IEnumerator WaitForPlaybackEndAndNotify()
        {
            while (_source != null && _source.isPlaying)
                yield return null;

            if (IsSpeaking)
            {
                IsSpeaking = false;

                if (lipSyncContext != null)
                    lipSyncContext.ResetContext();

                OnSpeechEnded?.Invoke();
            }

            _endMonitorRoutine = null;
        }

        private void OnDestroy()
        {
            InterruptCurrentSpeech();

            if (_source?.clip)
                Destroy(_source.clip);
        }
    }
}
