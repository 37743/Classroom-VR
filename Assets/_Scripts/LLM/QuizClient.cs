/// <summary>
/// Appathon CSE-EJUST Challenge Summer 2025
/// 
/// Client for handling quiz interactions, including requesting quizzes from a server,
/// displaying questions, submitting answers, and sending results to a MySQL database.
/// /// It integrates with the Piper text-to-speech system for audio feedback.
/// /// The quiz is triggered by a specific phrase in the response text and supports multiple-choice questions.
/// /// Questions and answers are managed through UI elements, with a submit button for answers.
/// /// The script also handles quiz selection based on saved preferences and ensures quizzes are taken within specified time windows.
/// /// The quiz is answered using voice commands recognized by the Whisper STT system.
///
/// Usage:
/// - Configure server and database connection settings in the Unity Inspector.
///     *TODO: Share SQL connection settings with BiologyQuestionClient.cs somehow.*
/// - The quiz starts automatically when a specific trigger phrase is detected in the response text.
/// - Questions and answers are managed through UI elements, with a submit button for answers.
/// - Results are submitted to a MySQL database, with minimal error handling for duplicate submissions.
/// 
/// Notes:
/// - Ensure the PiperDriver component is assigned for text-to-speech functionality.
/// - The script assumes a constant of 10 questions for database submission. *generated from LLM/RAG*
/// / </summary>
using UnityEngine;
using System.Net.Sockets;
using System.Text;
using System;
using System.IO;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine.UI;
using Piper.Samples;
using System.Globalization;
using MySqlConnector;
using System.Threading.Tasks;

public class QuizClient : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI responseText;
    [SerializeField] private Button submitAnswerButton;

    [Header("Answer UI")]
    [SerializeField] private TextMeshProUGUI choiceText;

    [Header("Server")]
    [SerializeField] private string serverAddress = "26.235.96.91";
    [SerializeField] private int serverPort = 8000;

    [Header("MySQL Connection")]
    [SerializeField] private string host;
    [SerializeField] private string database;
    [SerializeField] private string username;
    [SerializeField] private string password;
    [SerializeField] private uint port;

    [Header("Quiz Trigger")]
    [SerializeField] private string startTrigger = "Starting quiz...";
    [SerializeField] private string fallbackTopic = "Support in Living Organisms";

    [Header("Voice")]
    [SerializeField] private PiperDriver piperDriver;

    [Header("Button Safety")]
    [SerializeField] private float clickDebounce = 0.25f;

    private bool quizRequested = false;
    private List<Question> questions = new List<Question>();
    private Dictionary<int, string> answerKey = new Dictionary<int, string>();
    private int currentIndex = -1;
    private int correctCount = 0;
    private bool submitHooked = false;
    private float _lastClickTime = -1f;
    private string lastProcessedText;
    private Dictionary<int, int> questionGrades = new Dictionary<int, int>();

    private SelectedQuiz _selected;
    private bool _hasSelected;

    public delegate void ResponseReceivedHandler(string response);
    public event ResponseReceivedHandler OnResponseReceived;

    [Serializable]
    private class SelectedQuiz
    {
        public int quizId;
        public string title;
        public string notes;
        public DateTime startLocal;
        public DateTime endLocal;
        public int teacherId;
    }

    private class SubmissionResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
    }

    private string ConnectionString =>
        $"Server={host};Database={database};User ID={username};Password={password};Port={port};";

    private void Awake()
    {
        RunDecoderState.Reload();
        RunDecoderState.SetQuizMode(true);

        if (submitAnswerButton != null)
        {
            submitAnswerButton.gameObject.SetActive(false);
            submitAnswerButton.onClick.RemoveAllListeners();
        }

        if (choiceText != null)
        {
            choiceText.gameObject.SetActive(false);
            choiceText.text = string.Empty;
        }

        LoadAndSelectQuizFromPrefs();
        FillBoardHeader();
    }

    private void Update()
    {
        if (!quizRequested && responseText != null && !string.IsNullOrEmpty(responseText.text))
        {
            int idx = responseText.text.IndexOf(startTrigger, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                if (_hasSelected)
                {
                    var now = DateTime.Now;
                    if (now < _selected.startLocal || now > _selected.endLocal)
                    {
                        var msg = BuildBlockedStartMessage(now);
                        AppendBoardLine(msg);
                        piperDriver?.Speak("The quiz is not open yet.");
                        return;
                    }
                }

                string extractedTopic = ExtractTopic(responseText.text, idx + startTrigger.Length);
                string topicToUse = _hasSelected && !string.IsNullOrWhiteSpace(_selected.title)
                    ? _selected.title
                    : (string.IsNullOrWhiteSpace(extractedTopic) ? fallbackTopic : extractedTopic);

                if (choiceText != null)
                {
                    choiceText.gameObject.SetActive(true);
                    choiceText.text = string.Empty;
                }

                StartCoroutine(RequestQuizCoroutine(topicToUse));
                quizRequested = true;
            }
        }
    }

    private void LoadAndSelectQuizFromPrefs()
    {
        try
        {
            var rows = QuizPrefs.LoadQuizzes();
            if (rows == null || rows.Count == 0)
            {
                _hasSelected = false;
                return;
            }

            DateTime now = DateTime.Now;
            MySqlConnectorClient.QuizRowDTO activeBest = null;
            MySqlConnectorClient.QuizRowDTO upcomingBest = null;

            foreach (var r in rows)
            {
                DateTime startLocal = QuizPrefs.ParseIso(r.startTimeIso).ToLocalTime();
                DateTime endLocal = QuizPrefs.ParseIso(r.endTimeIso).ToLocalTime();

                if (now >= startLocal && now <= endLocal)
                {
                    if (activeBest == null || startLocal < QuizPrefs.ParseIso(activeBest.startTimeIso).ToLocalTime())
                        activeBest = r;
                }
                else if (startLocal > now)
                {
                    if (upcomingBest == null || startLocal < QuizPrefs.ParseIso(upcomingBest.startTimeIso).ToLocalTime())
                        upcomingBest = r;
                }
            }

            var chosen = activeBest ?? upcomingBest ?? rows[0];
            var st = QuizPrefs.ParseIso(chosen.startTimeIso).ToLocalTime();
            var en = QuizPrefs.ParseIso(chosen.endTimeIso).ToLocalTime();

            _selected = new SelectedQuiz
            {
                quizId = chosen.quizId,
                title = chosen.title ?? "",
                notes = chosen.notes ?? "",
                startLocal = st,
                endLocal = en,
                teacherId = chosen.teacherId
            };
            _hasSelected = true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[QuizClient] Failed to load/parse PlayerPrefs quiz data: " + ex.Message);
            _hasSelected = false;
        }
    }

    private void FillBoardHeader()
    {
        string header = $"Current Time: {DateTime.Now:g}\n\n";
        string topicLine, timeLine, notesLine;

        if (_hasSelected)
        {
            string topic = string.IsNullOrWhiteSpace(_selected.title) ? "N/A" : _selected.title;
            topicLine = $"Topic: {topic}\n";
            timeLine = $"Start Time: {_selected.startLocal:g}\t\tEnd Time: {_selected.endLocal:g}";
            notesLine = string.IsNullOrWhiteSpace(_selected.notes) ? "" : $"\nNotes: {_selected.notes}";
        }
        else
        {
            topicLine = "Topic: N/A";
            timeLine = "Start Time: N/A\t\tEnd Time: N/A";
            notesLine = "";
        }

        string tailInstruction = _hasSelected && DateTime.Now >= _selected.startLocal && DateTime.Now <= _selected.endLocal
            ? "\n\nSay \"Start Quiz\" to start.\nEnable your microphone first! (on your left wrist)"
            : "\n\nThe quiz is not open yet. Please wait until the start time.";

        string full = header + topicLine + timeLine + "\n" + notesLine + tailInstruction;

        if (responseText != null)
            responseText.text = full;
    }

    private static string FormatFriendly(TimeSpan ts)
    {
        if (ts.TotalSeconds <= 1) return "now";
        if (ts.TotalDays >= 1) return $"{ts.Days}d {ts.Hours}h";
        if (ts.TotalHours >= 1) return $"{ts.Hours}h {ts.Minutes}m";
        return $"{ts.Minutes}m {ts.Seconds}s";
    }

    private string BuildBlockedStartMessage(DateTime now)
    {
        if (!_hasSelected) return "No quiz window available.";
        if (now < _selected.startLocal)
            return $"Quiz not open yet. Opens at {_selected.startLocal:g} ({FormatFriendly(_selected.startLocal - now)} left).";
        if (now > _selected.endLocal)
            return $"Quiz closed at {_selected.endLocal:g}.";
        return "Quiz not available.";
    }

    private string ExtractTopic(string fullText, int startPos)
    {
        string tail = fullText.Substring(Mathf.Clamp(startPos, 0, fullText.Length)).Trim();
        int newlineIdx = tail.IndexOf('\n');
        if (newlineIdx >= 0) tail = tail.Substring(0, newlineIdx).Trim();
        return string.IsNullOrEmpty(tail) ? fallbackTopic : tail;
    }

    private void ClearBoard()
    {
        if (responseText != null) responseText.text = string.Empty;
    }

    public void NextQuestion()
    {
        if (questions == null || questions.Count == 0)
        {
            AppendBoardLine("No quiz loaded.");
            return;
        }

        currentIndex++;
        if (currentIndex >= questions.Count)
        {
            StartCoroutine(SubmitAndDisplayResults());
            return;
        }

        ShowQuestion(currentIndex);
    }

    private IEnumerator SubmitAndDisplayResults()
    {
        var submissionResult = new SubmissionResult();
        yield return StartCoroutine(SubmitQuizResultsCoroutine(submissionResult));

        int total = questions.Count;
        string msg;
        if (submissionResult.Success)
        {
            msg = $"Thank you! Your final grade is {correctCount}/{total} and has been submitted to your teacher.";
            piperDriver?.Speak("Thank you! Your final grade has been submitted to your teacher.");
        }
        else
        {
            msg = submissionResult.Message ?? $"Failed to submit quiz results. Your score: {correctCount}/{total}.";
            piperDriver?.Speak("There was an issue submitting your quiz results.");
        }

        if (responseText != null) responseText.text = msg;
        if (submitAnswerButton != null) submitAnswerButton.gameObject.SetActive(false);
        if (choiceText != null) choiceText.gameObject.SetActive(false);
        OnResponseReceived?.Invoke(msg);
    }

    private async Task<SubmissionResult> SubmitQuizResultsAsync()
    {
        var result = new SubmissionResult();

        object[] gradesArray = new object[10];
        for (int i = 0; i < 10; i++)
        {
            gradesArray[i] = i < questions.Count && questionGrades.ContainsKey(questions[i].id)
                ? questionGrades[questions[i].id]
                : DBNull.Value;
        }

        await using var conn = new MySqlConnection(ConnectionString);
        await conn.OpenAsync();

        string studentId = PlayerPrefs.GetString("UserID", "");
        if (string.IsNullOrEmpty(studentId))
        {
            result.Success = false;
            result.Message = "Error: Student ID not found in PlayerPrefs.";
            return result;
        }

        string takenAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        await using var transaction = await conn.BeginTransactionAsync();
        try
        {
            await using var cmd = new MySqlCommand("insert_quiz_attempt", conn)
            {
                CommandType = System.Data.CommandType.StoredProcedure,
                Transaction = transaction
            };

            cmd.Parameters.AddWithValue("p_quiz_id", _selected.quizId);
            cmd.Parameters.AddWithValue("p_student_id", studentId);
            cmd.Parameters.AddWithValue("p_taken_at", takenAt);
            for (int i = 0; i < 10; i++)
            {
                cmd.Parameters.AddWithValue($"p_q{i + 1}_grade", gradesArray[i]);
            }

            await cmd.ExecuteNonQueryAsync();
            await transaction.CommitAsync();

            result.Success = true;
            result.Message = "Quiz results submitted successfully.";
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            result.Success = false;
            result.Message = ex is MySqlException mse && mse.Number == 1062
                ? "Error: You have already submitted this quiz."
                : $"Failed to submit quiz results: {ex.Message}";
        }

        return result;
    }

    private IEnumerator SubmitQuizResultsCoroutine(SubmissionResult submissionResult)
    {
        Task<SubmissionResult> submitTask = SubmitQuizResultsAsync();
        yield return new WaitUntil(() => submitTask.IsCompleted);

        submissionResult.Success = submitTask.Result.Success;
        submissionResult.Message = submitTask.Result.Message;

        if (responseText != null)
        {
            AppendBoardLine(submissionResult.Message);
        }
        OnResponseReceived?.Invoke(submissionResult.Message);

        yield return null;
    }

    private void ShowQuestion(int index)
    {
        var q = questions[index];
        var sb = new StringBuilder();

        sb.AppendLine($"(Question #{index + 1}: {q.text})\n");

        for (int i = 0; i < q.options.Length && i < 4; i++)
        {
            string opt = StripLeadingMarker(q.options[i]);
            sb.AppendLine($"({i + 1}) {opt}");
        }

        if (responseText != null) responseText.text = sb.ToString().TrimEnd();

        if (choiceText != null)
        {
            choiceText.text = "Choice: None";
        }

        piperDriver?.Speak(q.text);
    }

    private string StripLeadingMarker(string option)
    {
        return Regex.Replace(option ?? "", @"^\s*(?:[A-Da-d]|[1-4])\s*[\)\.\:]\s*", "").Trim();
    }

    private IEnumerator RequestQuizCoroutine(string topicToUse)
    {
        TcpClient client = null;
        NetworkStream stream = null;

        client = new TcpClient();
        yield return StartCoroutine(ConnectToServer(client, serverAddress, serverPort));

        if (!client.Connected)
        {
            AppendBoardLine("Failed to connect to server.");
            yield break;
        }

        try
        {
            string title = _hasSelected && !string.IsNullOrWhiteSpace(_selected.title) ? _selected.title : topicToUse;
            string notes = _hasSelected ? (_selected.notes ?? "") : "";
            string jsonData = "{\"title\":\"" + EscapeJson(title) + "\",\"notes\":\"" + EscapeJson(notes) + "\"}";
            byte[] data = Encoding.UTF8.GetBytes(jsonData);

            stream = client.GetStream();

            byte[] lengthBytes = BitConverter.GetBytes((uint)data.Length);
            if (BitConverter.IsLittleEndian) Array.Reverse(lengthBytes);
            stream.Write(lengthBytes, 0, lengthBytes.Length);
            stream.Write(data, 0, data.Length);

            byte[] lengthBuffer = new byte[4];
            int bytesRead = 0;
            while (bytesRead < 4)
            {
                int read = stream.Read(lengthBuffer, bytesRead, 4 - bytesRead);
                if (read == 0) throw new IOException("Connection closed by server.");
                bytesRead += read;
            }
            if (BitConverter.IsLittleEndian) Array.Reverse(lengthBuffer);
            uint responseLength = BitConverter.ToUInt32(lengthBuffer, 0);

            byte[] responseBytes = new byte[responseLength];
            bytesRead = 0;
            while (bytesRead < responseLength)
            {
                int read = stream.Read(responseBytes, bytesRead, (int)responseLength - bytesRead);
                if (read == 0) throw new IOException("Connection closed by server.");
                bytesRead += read;
            }

            string responseJson = Encoding.UTF8.GetString(responseBytes);
            ParseQuizPayload(responseJson, out var parsedQuestions, out var parsedAnswers);

            if (parsedQuestions == null || parsedQuestions.Count == 0)
            {
                AppendBoardLine("No questions received.");
                yield break;
            }

            questions = parsedQuestions;
            answerKey = parsedAnswers ?? new Dictionary<int, string>();
            currentIndex = -1;
            correctCount = 0;
            questionGrades.Clear();

            if (submitAnswerButton != null && !submitHooked)
            {
                submitAnswerButton.onClick.RemoveAllListeners();
                submitAnswerButton.onClick.AddListener(OnSubmitClick);
                submitHooked = true;
            }

            if (submitAnswerButton != null) submitAnswerButton.gameObject.SetActive(true);

            NextQuestion();

            OnResponseReceived?.Invoke($"Loaded quiz: {title} ({questions.Count} questions)");
        }
        catch (Exception e)
        {
            AppendBoardLine("Error: " + e.Message);
            OnResponseReceived?.Invoke("Error: " + e.Message);
        }
        finally
        {
            stream?.Close();
            client?.Close();
        }
    }

    private void OnSubmitClick()
    {
        if (Time.unscaledTime - _lastClickTime < clickDebounce) return;
        _lastClickTime = Time.unscaledTime;

        if (submitAnswerButton != null) submitAnswerButton.interactable = false;

        SubmitCurrentAnswerAndAdvance();

        if (submitAnswerButton != null) submitAnswerButton.interactable = true;
    }

    private void SubmitCurrentAnswerAndAdvance()
    {
        if (currentIndex < 0 || currentIndex >= questions.Count)
            return;

        var q = questions[currentIndex];
        string chosenRaw = choiceText != null ? choiceText.text.Replace("Choice:", "").Trim() : null;
        string chosen = NormalizeChoice(chosenRaw);

        bool isCorrect = false;
        if (!string.IsNullOrEmpty(chosen) && answerKey != null && answerKey.TryGetValue(q.id, out string correct))
        {
            isCorrect = string.Equals(correct, chosen, StringComparison.OrdinalIgnoreCase);
        }

        questionGrades[q.id] = isCorrect ? 1 : 0;
        if (isCorrect) correctCount++;

        NextQuestion();
    }

    private IEnumerator ConnectToServer(TcpClient client, string host, int port)
    {
        IAsyncResult asyncResult = client.BeginConnect(host, port, null, null);
        yield return new WaitUntil(() => asyncResult.IsCompleted);

        try
        {
            client.EndConnect(asyncResult);
        }
        catch (Exception ex)
        {
            Debug.LogError("Connection error: " + ex.Message);
        }
    }

    private void ParseQuizPayload(string raw, out List<Question> outQuestions, out Dictionary<int, string> outAnswers)
    {
        outQuestions = new List<Question>();
        outAnswers = new Dictionary<int, string>();

        if (string.IsNullOrWhiteSpace(raw)) return;

        string cleaned = raw.Trim();
        int lbl = cleaned.IndexOf("Model raw output:", StringComparison.OrdinalIgnoreCase);
        if (lbl >= 0)
            cleaned = cleaned.Substring(lbl + "Model raw output:".Length).Trim();

        string firstObj = null, secondObj = null;
        int boundary = FindJsonBoundary(cleaned);
        if (boundary > 0)
        {
            firstObj = cleaned.Substring(0, boundary + 1).Trim();
            secondObj = cleaned.Substring(boundary + 1).Trim();
        }

        if (firstObj != null && secondObj != null && firstObj.StartsWith("{") && secondObj.StartsWith("{"))
        {
            TryParseQuestions(firstObj, outQuestions);
            TryParseAnswers(secondObj, outAnswers);
            return;
        }

        if (!TryParseQuestions(cleaned, outQuestions))
        {
            var qArray = ExtractJsonArray(cleaned, "\"questions\"");
            if (!string.IsNullOrEmpty(qArray))
                TryParseQuestions("{\"questions\":" + qArray + "}", outQuestions);
        }

        if (!TryParseAnswers(cleaned, outAnswers))
        {
            var aObj = ExtractJsonObject(cleaned, "\"answers\"");
            if (!string.IsNullOrEmpty(aObj))
                TryParseAnswers("{\"answers\":" + aObj + "}", outAnswers);
        }
    }

    private bool TryParseQuestions(string json, List<Question> collector)
    {
        try
        {
            var wrapper = JsonUtility.FromJson<QuestionsWrapper>(json);
            if (wrapper != null && wrapper.questions != null && wrapper.questions.Length > 0)
            {
                collector.AddRange(wrapper.questions);
                return true;
            }
        }
        catch { }
        return false;
    }

    private bool TryParseAnswers(string json, Dictionary<int, string> answersOut)
    {
        try
        {
            string answersObj = ExtractJsonObject(json, "\"answers\"");
            string src = string.IsNullOrEmpty(answersObj) ? json : answersObj;

            foreach (Match m in Regex.Matches(src, "\"(\\d+)\"\\s*:\\s*\"([A-Da-d1-4])\""))
            {
                int id = int.Parse(m.Groups[1].Value);
                string rawVal = m.Groups[2].Value.Trim();
                answersOut[id] = NormalizeChoice(rawVal);
            }
            return answersOut.Count > 0;
        }
        catch { }
        return false;
    }

    private int FindJsonBoundary(string s)
    {
        int depth = 0;
        bool inString = false;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '"' && (i == 0 || s[i - 1] != '\\')) inString = !inString;
            if (inString) continue;
            if (c == '{') depth++;
            else if (c == '}') depth--;
            if (depth == 0 && c == '}')
            {
                int j = i + 1;
                while (j < s.Length && char.IsWhiteSpace(s[j])) j++;
                if (j < s.Length && s[j] == '{')
                    return i;
            }
        }
        return -1;
    }

    private string ExtractJsonArray(string src, string keyLiteral)
    {
        int k = src.IndexOf(keyLiteral, StringComparison.OrdinalIgnoreCase);
        if (k < 0) return null;
        int lb = src.IndexOf('[', k);
        if (lb < 0) return null;
        int rb = MatchBracket(src, lb, '[', ']');
        if (rb < 0) return null;
        return src.Substring(lb, rb - lb + 1);
    }

    private string ExtractJsonObject(string src, string keyLiteral)
    {
        int k = src.IndexOf(keyLiteral, StringComparison.OrdinalIgnoreCase);
        if (k < 0) return null;
        int lb = src.IndexOf('{', k);
        if (lb < 0) return null;
        int rb = MatchBracket(src, lb, '{', '}');
        if (rb < 0) return null;
        return src.Substring(lb, rb - lb + 1);
    }

    private int MatchBracket(string s, int start, char open, char close)
    {
        int depth = 0;
        bool inString = false;
        for (int i = start; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '"' && (i == 0 || s[i - 1] != '\\')) inString = !inString;
            if (inString) continue;
            if (c == open) depth++;
            else if (c == close) depth--;
            if (depth == 0) return i;
        }
        return -1;
    }

    private string EscapeJson(string s)
    {
        return (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private void AppendBoardLine(string text)
    {
        if (responseText != null)
        {
            if (string.IsNullOrEmpty(responseText.text)) responseText.text = text;
            else responseText.text += "\n" + text;
        }
        else
        {
            Debug.LogWarning("TextMeshProUGUI component is not assigned.");
        }
    }

    private string NormalizeChoice(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        string s = raw.Trim();
        if (Regex.IsMatch(s, @"^[1-4]$")) return s;
        s = s.ToUpperInvariant();
        switch (s)
        {
            case "A": return "1";
            case "B": return "2";
            case "C": return "3";
            case "D": return "4";
            default: return null;
        }
    }

    [Serializable]
    private class QuestionsWrapper
    {
        public Question[] questions;
        public AnswersWrapper answers;
    }

    [Serializable]
    private class Question
    {
        public int id;
        public string text;
        public string[] options;
    }

    [Serializable]
    private class AnswersWrapper { }
}