/// <summary>
/// Appathon CSE-EJUST Challenge Summer 2025
/// 
/// State indicating that the Whisper service is ready to process audio input.
/// /// This state is entered after the model components have been successfully loaded.
/// </summary>
using UnityEngine;
using StateAsm;

public class WhisperReadyState : SentisWhisperState
{
    public WhisperReadyState(IStateMachine<WhisperStateID> stateMachine) : base(stateMachine, WhisperStateID.Ready, WhisperStateID.Ready)
    {      
    }

    public override void Enter()
    {
        Debug.Log("-> WhisperReadyState::Enter()");
        whisper.IsReady = true;
    }
}
