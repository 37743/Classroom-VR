/// <summary>
/// Appathon CSE-EJUST Challenge Summer 2025
/// 
/// OPENAI's Whisper model is a neural network for automatic speech recognition (ASR).
/// This script runs the Whisper-tiny model for speech-to-text transcription. 
/// 
/// /// The model consists of three parts: a spectrogram model, an encoder model, and a decoder model. *also shown in the repo as a diagram*
/// /// - The spectrogram model converts audio data into a spectrogram representation.
/// /// - The encoder model processes the spectrogram to produce encoded audio features.
/// /// - The decoder model takes the encoded audio features and generates text output.
///
/// /// IMPORTANT: The state machine manages the loading and execution of these models in sequence:
/// - LoadDecoderState: Loads the decoder model.
/// - LoadEncoderState: Loads the encoder model.
/// - LoadSpectroState: Loads the spectrogram model.
/// - WhisperReadyState: Indicates that the system is ready for transcription.
/// - StartTranscriptionState: Prepares the audio data for transcription.
/// - RunSpectroState: Runs the spectrogram model on the audio data.
/// - RunEncoderState: Runs the encoder model on the spectrogram data.
/// - RunDecoderState: Runs the decoder model to generate text output.
/// 
/// Usage:
/// - Call the Transcribe() method to start the transcription process on a given AudioClip. (generated from MicRecorder.cs)
/// - The transcription result is displayed in the SpeechText TMP_Text field.
/// </summary>
using UnityEngine;
using StateAsm;
using TMPro;
using System.Collections.Generic;

public enum WhisperStateID
{
    LoadDecoder,
    LoadEncoder,
    LoadSpectro,
    Ready,
    StartTranscription,
    RunSpectro,
    RunEncoder,
    RunDecoder
}

public class RunWhisper : GameObjectStateMachine<WhisperStateID>
{
    public Unity.InferenceEngine.ModelAsset decoderAsset;
    public Unity.InferenceEngine.ModelAsset encoderAsset;
    public Unity.InferenceEngine.ModelAsset spectroAsset;
    public TextAsset vocabJson;

    public Unity.InferenceEngine.Worker DecoderEngine { get; set; }
    public Unity.InferenceEngine.Worker EncoderEngine { get; set; }
    public Unity.InferenceEngine.Worker SpectroEngine { get; set; }

    private AudioClip audioClip;
    public AudioClip AudioClip { get { return audioClip; } }

    public int NumSamples { get; set; }
    public float[] Data { get; set; }
    public string[] Tokens { get; set; }

    public int[] WhiteSpaceCharacters { get; set; }

    public Unity.InferenceEngine.Tensor<float> SpectroOutput { get; set; }
    public Unity.InferenceEngine.Tensor<float> EncodedAudio { get; set; }

    public TMP_Text SpeechText;
    public TMP_Text ChoiceText;

    public bool IsReady { get; set; }

    protected override void Awake()
    {
        IsReady = false;
        WhiteSpaceCharacters = new int[256];
        SetupWhiteSpaceShifts();
        SetTokens();
        base.Awake();
    }

    protected override void Start()
    {
        base.Start();
    }

    protected override void InitStates()
    {
        base.InitStates();
        AddState(new LoadDecoderState(this));
        AddState(new LoadEncoderState(this));
        AddState(new LoadSpectroState(this));
        AddState(new WhisperReadyState(this));
        AddState(new StartTranscriptionState(this));
        AddState(new RunSpectroState(this));
        AddState(new RunEncoderState(this));
        AddState(new RunDecoderState(this));

        SetState(WhisperStateID.LoadDecoder);
    }

    private void SetTokens()
    {
        var vocab = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, int>>(vocabJson.text);
        Tokens = new string[vocab.Count];

        foreach (var item in vocab)
        {
            Tokens[item.Value] = item.Key;
        }
    }

    public void Transcribe(AudioClip clip)
    {
        Debug.Log("-> Transcribe() ...");

        DisposeIntermediateTensors();

        IsReady = false;
        audioClip = clip;

        SetState(WhisperStateID.StartTranscription);
    }

    public void DisposeIntermediateTensors()
    {
        if (SpectroOutput != null)
        {
            try { SpectroOutput.Dispose(); } catch {}
            SpectroOutput = null;
        }

        if (EncodedAudio != null)
        {
            try { EncodedAudio.Dispose(); } catch {}
            EncodedAudio = null;
        }
    }

    public void MarkReady()
    {
        IsReady = true;
    }

    protected override void Update()
    {
        base.Update();
    }

    void SetupWhiteSpaceShifts()
    {
        for (int i = 0, n = 0; i < 256; i++)
        {
            if (IsWhiteSpace((char)i)) WhiteSpaceCharacters[n++] = i;
        }
    }

    bool IsWhiteSpace(char c)
    {
        return !(('!' <= c && c <= '~') || ('\u00A0' <= c && c <= '\u00FF') || ('\u0100' <= c && c <= '\u017F'));
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        DisposeIntermediateTensors();

        DecoderEngine?.Dispose();
        EncoderEngine?.Dispose();
        SpectroEngine?.Dispose();
    }
}
