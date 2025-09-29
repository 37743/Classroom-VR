/// <summary>
/// Appathon CSE-EJUST Challenge Summer 2025
/// 
/// State responsible for loading the spectrogram model used in the Whisper transcription process.
/// It is essential for converting audio data into a format suitable for further processing by the encoder.
/// Upon successful loading, it transitions to the Ready state, indicating that the system is prepared for transcription tasks.
/// </summary>
using UnityEngine;
using StateAsm;

public class LoadSpectroState : SentisWhisperState
{

    public LoadSpectroState(IStateMachine<WhisperStateID> stateMachine) : base(stateMachine, WhisperStateID.LoadSpectro, WhisperStateID.Ready)
    {

    }

    public override void Enter()
    {
        //Debug.Log("-> LoadSpectroState::Enter()");
        stage = 0;
    }

    public override void Update()
    {
        switch (stage)
        {
            case 0:
                LoadSpectro();
                stage = 1;
                break;
            default:
                stateMachine.SetState(nextStateId);
                break;
        }
    }


    private void LoadSpectro()
    {
        Unity.InferenceEngine.Model spectro = Unity.InferenceEngine.ModelLoader.Load(whisper.spectroAsset);
        //whisper.SpectroEngine = WorkerFactory.CreateWorker(backend, spectro);
        whisper.SpectroEngine = new Unity.InferenceEngine.Worker(spectro, backend);
    }


    public override void Exit()
    {
        base.Exit();
    }
}