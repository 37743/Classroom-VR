/// <summary>
/// Appathon CSE-EJUST Challenge Summer 2025
/// 
/// Base class for states in the Whisper state machine.
/// /// It holds a reference to the RunWhisper state machine and manages the transition to the next state.
/// </summary>
using StateAsm;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;

public class SentisWhisperState : GameObjectState<WhisperStateID>
{
    protected RunWhisper whisper;
    protected int stage = 0; // 0: Loading, 1: Processing
    protected WhisperStateID nextStateId;

    protected const Unity.InferenceEngine.BackendType backend = Unity.InferenceEngine.BackendType.GPUCompute;

    public SentisWhisperState(IStateMachine<WhisperStateID> stateMachine, WhisperStateID id, WhisperStateID nextStateId) : base(stateMachine, id)
    {
        whisper = stateMachine as RunWhisper;
        this.nextStateId = nextStateId;
    }

    public override void Enter() { }
    public override void Update() { }
    public override void Exit() { }
}
