/// <summary>
/// Appathon CSE-EJUST Challenge Summer 2025
/// 
/// State responsible for starting the transcription process by loading audio data.
/// /// It checks the audio clip's frequency and length, and prepares the data for further processing.
/// /// Upon successful loading, it transitions to the RunSpectro state.
/// </summary>
using UnityEngine;
using UnityEngine.Rendering;
using StateAsm;

public class StartTranscriptionState : SentisWhisperState
{
    // Maximum size of audioClip (30s at 16kHz)
    const int maxSamples = 30 * 16000;

    public StartTranscriptionState(IStateMachine<WhisperStateID> stateMachine) : base(stateMachine, WhisperStateID.StartTranscription, WhisperStateID.RunSpectro)
    {
    }

    public override void Enter()
    {
        Debug.Log("-> StartTranscriptionState::Enter()");
        Debug.Log("Transcribing audio clip...");
        LoadAudio();
    }

    private void LoadAudio()
    {
        if (whisper.AudioClip.frequency != 16000)
        {
            Debug.Log($"The audio clip should have frequency 16kHz. It has frequency {whisper.AudioClip.frequency / 1000f}kHz");
            return;
        }

        whisper.NumSamples = whisper.AudioClip.samples;

        if (whisper.NumSamples > maxSamples)
        {
            Debug.Log($"The AudioClip is too long. It must be less than 30 seconds. This clip is {whisper.NumSamples / whisper.AudioClip.frequency} seconds.");
            return;
        }

        whisper.Data = new float[maxSamples];
        whisper.NumSamples = maxSamples;

        whisper.AudioClip.GetData(whisper.Data, 0);
    }

    public override void Update()
    {
        stateMachine.SetState(nextStateId);
    }
}
