/// <summary>
/// Appathon CSE-EJUST Challenge Summer 2025
/// 
/// State responsible for running the spectrogram model on the loaded audio data.
/// /// It processes the audio samples to generate a spectrogram, which is then used as input for the encoder model.
/// /// - A spectrogram is a visual representation of the spectrum of frequencies in a sound signal as they vary with time.
/// /// - This state uses the SpectroEngine to perform inference on the audio data.
/// /// Upon completion, it transitions to the RunEncoder state.
/// </summary>
using UnityEngine;
using System.Collections;
using StateAsm;

public class RunSpectroState : SentisWhisperState
{
    private Unity.InferenceEngine.Tensor<float> spectroOutput;

    public RunSpectroState(IStateMachine<WhisperStateID> stateMachine) : base(stateMachine, WhisperStateID.RunSpectro, WhisperStateID.RunEncoder)
    {
    }

    public override void Enter()
    {
        Debug.Log("-> RunSpectroState::Enter()");
        stage = 0;
        RunSpectro();
    }
 
    public override void Update()
    {
        stateMachine.SetState(nextStateId);
    }
       
    private void RunSpectro()
    {
        using var input = new Unity.InferenceEngine.Tensor<float>(new Unity.InferenceEngine.TensorShape(1, whisper.NumSamples), whisper.Data);
        whisper.SpectroEngine.Schedule(input);

        spectroOutput = whisper.SpectroEngine.PeekOutput() as Unity.InferenceEngine.Tensor<float>;

        whisper.SpectroOutput?.Dispose();
        whisper.SpectroOutput = null;

        whisper.SpectroOutput = spectroOutput.ReadbackAndClone();
        spectroOutput?.Dispose();
        spectroOutput = null;
    }

    public override void Exit()
    {
        // Safety net in case anything remained
        spectroOutput?.Dispose(); spectroOutput = null;
        base.Exit();
    }
}
