/// <summary>
/// Appathon CSE-EJUST Challenge Summer 2025
/// 
/// State responsible for running the decoder model to generate text tokens from encoded audio features.
/// /// It iteratively predicts the next token based on the previously generated tokens and the encoded audio
/// 
/// Usage:
/// - The state starts with a predefined set of tokens indicating the start of transcription.
/// - It continues to predict tokens until it encounters the end-of-text token or reaches the maximum token limit.
/// - The generated tokens are converted to text and appended to the transcript display.
/// 
/// This code is has custom logic for quiz mode, where it listens for a "start quiz" command.
/// /// In quiz mode, it can recognize simple numeric answers (1-4) and display them as choices.
/// /// Quizmode can be toggled on/off via SetQuizMode(bool), which also resets the armed state, and is refreshed on scene change.
/// /// It also supports overriding the user name label via OverrideUserName(string) once the user has logged in.
/// </summary>
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using StateAsm;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.InferenceEngine;
using System.Text.RegularExpressions;

public class RunDecoderState : SentisWhisperState
{
    const int END_OF_TEXT = 50257;
    const int START_OF_TRANSCRIPT = 50258;
    const int ENGLISH = 50259;
    // const int GERMAN = 50261;
    // const int FRENCH = 50265;
    const int TRANSCRIBE = 50359;
    const int TRANSLATE = 50358;
    const int NO_TIME_STAMPS = 50363;
    const int START_TIME = 50364;

    const int maxTokens = 100;
    private int currentToken = 3;
    private int[] outputTokens = new int[maxTokens];
    private string outputString = "";

    Unity.InferenceEngine.Tensor<int> tokensPredictions;
    Unity.InferenceEngine.Tensor<int> cpuTokensPredictions;

    // User label
    private static string sUserName = "You";
    private static bool sRequestedUserName = false;

    // Clear-once flag (no header)
    private static bool sClearedBoardOnce = false;

    private static bool sQuizMode = false;
    private static bool sQuizArmed = false;

    private static int sLastSceneHandle = -1;

    public static void SetQuizMode(bool enabled)
    {
        sQuizMode = enabled;
        if (!enabled) sQuizArmed = false;
    }

    public static bool IsQuizMode() => sQuizMode;

    public static void OverrideUserName(string name)
    {
        if (!string.IsNullOrWhiteSpace(name)) sUserName = name.Trim();
    }

    private static string GetUserLabel() => sUserName;

    private static void EnsureUserName()
    {
        if (sRequestedUserName) return;
        sRequestedUserName = true;

        if (string.IsNullOrWhiteSpace(sUserName))
            sUserName = "You";
    }

    private static bool IsStartQuizCommand(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;

        string norm = text.Trim().ToLowerInvariant();
        norm = Regex.Replace(norm, @"[\""'’\s]+$", "");
        norm = Regex.Replace(norm, @"[.!?]+$", "");
        norm = norm.Trim();
        return norm == "start quiz";
    }

    private static string CleanTranscript(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        var rxBlankAudio = new Regex(@"\s*\[(?:blank_audio|blank audio)\]\s*", RegexOptions.IgnoreCase);
        var rxYouOnly    = new Regex(@"^\s*you\s*[.!?\""]?\s*$", RegexOptions.IgnoreCase);
        var rxMrRashed   = new Regex(@"\bM(?:r|ister)\.?\s*R[a-zA-Z][a-zA-Z\-']{0,20}(?<poss>'s|’s|s')?",
                                     RegexOptions.IgnoreCase);

        string s = input;
        s = rxBlankAudio.Replace(s, " ");
        s = rxMrRashed.Replace(s, m => "Mr. Rashed" + m.Groups["poss"].Value);
        s = Regex.Replace(s, @"\s{2,}", " ").Trim();
        if (rxYouOnly.IsMatch(s)) return string.Empty;

        return s;
    }

    public RunDecoderState(IStateMachine<WhisperStateID> stateMachine)
        : base(stateMachine, WhisperStateID.RunDecoder, WhisperStateID.Ready) { }

    public override void Enter()
    {
        Debug.Log("-> RunDecoderState::Enter()");
        stage = 0;

        outputTokens[0] = START_OF_TRANSCRIPT;
        outputTokens[1] = ENGLISH;
        outputTokens[2] = TRANSCRIBE;
        outputTokens[3] = NO_TIME_STAMPS;
        currentToken = 3;

        outputString = "";

        if (!sQuizMode) ClearBoardOnce();
        EnsureUserName();
    }

    public override void Update()
    {
        switch (stage)
        {
            case 0:
                if (currentToken < outputTokens.Length - 1)
                {
                    ExecuteDecoder();
                }
                break;
            default:
                stateMachine.SetState(nextStateId);
                break;
        }
    }

    public static void Reload()
    {
        sQuizMode = false;
        Debug.Log("RunDecoderState reset.");
    }

    private void ExecuteDecoder()
    {
        using var tokensSoFar =
            new Unity.InferenceEngine.Tensor<int>(new Unity.InferenceEngine.TensorShape(1, outputTokens.Length), outputTokens);

        var inputs = new Dictionary<string, Tensor>
        {
            { "input_0", tokensSoFar },
            { "input_1", whisper.EncodedAudio }
        };

        whisper.DecoderEngine.Schedule(inputs.Values.ToArray());

        // Dispose any previous predictions before overwriting
        tokensPredictions?.Dispose(); tokensPredictions = null;
        cpuTokensPredictions?.Dispose(); cpuTokensPredictions = null;

        tokensPredictions = whisper.DecoderEngine.PeekOutput() as Unity.InferenceEngine.Tensor<int>;
        cpuTokensPredictions = tokensPredictions.ReadbackAndClone();

        tokensPredictions?.Dispose(); tokensPredictions = null;

        int ID = cpuTokensPredictions[currentToken];
        outputTokens[++currentToken] = ID;

        if (ID == END_OF_TEXT)
        {
            stage = 1;

            AppendTranscriptLine(outputString);

            if (whisper.SpeechText == null)
            {
                Debug.LogError("-> RunDecoderState::Update() - SpeechText is NULL! :(");
            }
        }
        else if (ID >= whisper.Tokens.Length)
        {
            outputString += $"(time={(ID - START_TIME) * 0.02f})";
        }
        else
        {
            outputString += GetUnicodeText(whisper.Tokens[ID]);
        }

        // Done with CPU predictions for this step
        cpuTokensPredictions?.Dispose(); cpuTokensPredictions = null;
    }

    private void AppendTranscriptLine(string line)
    {
        if (whisper?.SpeechText == null) return;

        EnsureUserName();

        string cleaned = CleanTranscript(line);
        if (string.IsNullOrEmpty(cleaned)) return;

        if (sQuizMode)
        {
            if (!sQuizArmed && IsStartQuizCommand(cleaned))
            {
                whisper.SpeechText.text = "Starting quiz...";
                sQuizArmed = true;
                Debug.Log("[QUIZ] Start command received. Board cleared. Quiz mode armed.");
                return;
            }

            if (sQuizArmed)
            {
                string cleanedLower = cleaned.ToLowerInvariant();
                cleanedLower = Regex.Replace(cleanedLower, @"^\s*[\p{P}\p{S}]+", "");
                cleanedLower = Regex.Replace(cleanedLower, @"[\p{P}\p{S}]+\s*$", "");
                cleanedLower = cleanedLower.Trim();

                var m = Regex.Match(
                    cleanedLower,
                    @"^\s*(?:(?<num>[1-4])|(?<word>one|two|three|four))\s*$"
                );

                Debug.Log($"[QUIZ] Choice regex match: {m.Success}, '{cleanedLower}'");
                if (m.Success)
                {
                    string chosen = null;

                    if (m.Groups["num"].Success)
                    {
                        // Already 1-4
                        chosen = m.Groups["num"].Value;
                    }
                    else // word matched
                    {
                        switch (m.Groups["word"].Value.ToLowerInvariant())
                        {
                            case "one":   chosen = "1"; break;
                            case "two":   chosen = "2"; break;
                            case "three": chosen = "3"; break;
                            case "four":  chosen = "4"; break;
                        }
                    }

                    if (chosen != null && whisper?.ChoiceText != null)
                    {
                        whisper.ChoiceText.text = $"Choice: {chosen}";
                        Debug.Log($"[QUIZ] Choice: {chosen}");
                    }
                }
            }

            return;
        }

        string label = "[" + GetUserLabel() + "]: " + cleaned;

        if (string.IsNullOrEmpty(whisper.SpeechText.text))
            whisper.SpeechText.text = label;
        else
            whisper.SpeechText.text += "\n" + label;
    }

    private void ClearBoardOnce()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.handle != sLastSceneHandle)
        {
            sLastSceneHandle = scene.handle;
            sClearedBoardOnce = false;
        }

        if (sClearedBoardOnce) return;
        if (whisper?.SpeechText == null) return;

        whisper.SpeechText.text = string.Empty;
        sClearedBoardOnce = true;
    }

    string GetUnicodeText(string text)
    {
        var bytes = Encoding.GetEncoding("ISO-8859-1").GetBytes(ShiftCharacterDown(text));
        return Encoding.UTF8.GetString(bytes);
    }

    string ShiftCharacterDown(string text)
    {
        string outText = "";
        foreach (char letter in text)
        {
            outText += ((int)letter <= 256) ? letter :
                (char)whisper.WhiteSpaceCharacters[(int)(letter - 256)];
        }
        return outText;
    }

    public override void Exit()
    {
        tokensPredictions?.Dispose(); tokensPredictions = null;
        cpuTokensPredictions?.Dispose(); cpuTokensPredictions = null;

        whisper.EncodedAudio?.Dispose(); whisper.EncodedAudio = null;

        GC.Collect();
        base.Exit();
    }
}
