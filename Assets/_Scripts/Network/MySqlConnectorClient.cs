/// <summary>
/// Appathon CSE-EJUST Challenge Summer 2025
/// 
/// MySQL Connector client for Unity.
/// Handles user login and quiz availability based on teacher assignments.
/// /// Requires the MySqlConnector package *available on NuGet*.
/// 
/// Setup:
/// 1. Create a MySQL database with the required schema and stored procedures. *See README for details.*
/// 2. Assign the MySqlConnectorClient script to a GameObject in your scene.
/// 3. Configure the connection parameters in the Unity inspector.
/// 4. Link the UI elements (input fields, buttons, text) to the script.
/// 5. Optionally, handle teacher buttons and alert icons for quiz availability.
/// 
/// Usage:
/// - Call AttemptLoginFromUI() when the user clicks the login button.
/// - The script will manage the login process and update the UI accordingly.
/// - After a successful login, it can automatically fetch and display quiz availability *assuming the database schema is correct.*
/// 
/// TODO:
/// - Add registeration support.
/// - Add teacher mode support.
/// 
/// Note for users other than our developer team:
/// - Ensure your MySQL server allows remote connections if not hosted locally.
/// - Use secure practices for handling database credentials. *salting and hashing*
/// - This script is designed for educational purposes and may require adjustments for production use.
/// </summary>
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;
using MySqlConnector;

public class MySqlConnectorClient : MonoBehaviour
{
    [Header("MySQL Connection")]
    [SerializeField] private string host;
    [SerializeField] private string database;
    [SerializeField] private string username;
    [SerializeField] private string password;
    [SerializeField] private uint port;

    [Header("UI References")]
    [SerializeField] private TMP_InputField emailField;
    [SerializeField] private TMP_InputField passwordField;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private Button loginButton;

    [Header("Events")]
    public UnityEvent onLoginSuccess;
    public UnityEvent<string> onLoginFailure;

    public enum LoginResult { Success = 0, InvalidCredentials = 1, Error = -1 }

    [Header("Quiz Availability (after login)")]
    [Tooltip("When ON, the controller will automatically fetch and gate teacher buttons once login succeeds.")]
    [SerializeField] private bool autoStartAvailabilityAfterLogin = true;

    [Tooltip("If your DB DATETIME is stored as UTC, keep this ON so times are converted to local time.")]
    [SerializeField] private bool dbTimesAreUtc = true;

    [Tooltip("Refreshes quiz availability automatically every N seconds (0 to disable auto-refresh).")]
    [SerializeField] private float refreshSeconds = 60f;

    [Tooltip("Optional label to show the earliest quiz across all teachers.")]
    [SerializeField] private TMP_Text globalNextQuizLabel;

    [Tooltip("Map each teacher_id to a button and optional notes label.")]
    [SerializeField] private List<TeacherButtonEntry> teacherButtons = new();

    [Header("Alert Icon")]
    [Tooltip("Prefab (UI GameObject) to spawn as the alert icon. Will be parented under the button.")]
    [SerializeField] private RectTransform alertIconPrefab;

    [Tooltip("Optional: a general 'Quiz' button that should also get an alert icon when any quiz is open.")]
    [SerializeField] private Button globalQuizButton;

    [Serializable]
    public class TeacherButtonEntry
    {
        public string teacherId;
        public Button button;
        public TMP_Text notesLabel;

        [NonSerialized] public RectTransform spawnedAlertIcon; // runtime instance
    }

    private class QuizRow
    {
        public int quizId;
        public string title;
        public string notes;
        public DateTime startLocal;
        public DateTime endLocal;
        public int teacherId;
    }

    [Serializable]
    public class QuizRowDTO
    {
        public int quizId;
        public string title;
        public string notes;
        public string startTimeIso; // stored as UTC ISO 8601
        public string endTimeIso;   // stored as UTC ISO 8601
        public int teacherId;
    }

    [Serializable]
    private class QuizListWrapper { public List<QuizRowDTO> items; }

    private readonly List<QuizRow> _rows = new();
    private bool _isRefreshing;

    private RectTransform _globalQuizAlertIcon;

    private string ConnStr =>
        $"Server={host};Database={database};User ID={username};Password={password};Port={port};";

    public void AttemptLoginFromUI()
    {
        string email = emailField ? emailField.text : "";
        string pass = passwordField ? passwordField.text : "";

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(pass))
        {
            ShowResult("Kindly enter both email and password.", false);
            onLoginFailure?.Invoke("Missing email or password.");
            return;
        }

        _ = AttemptLoginAsync(email, pass);
    }

    public void ManualRefreshAvailability()
    {
        _ = RefreshAvailabilityNow();
    }

    private void OnEnable()
    {
        // Lock teacher buttons until we have data (useful if scene starts already logged in)
        foreach (var e in teacherButtons)
            if (e?.button) e.button.interactable = false;
    }

    private void OnDisable()
    {
        if (refreshSeconds > 0f)
            CancelInvoke(nameof(RefreshTick));
    }

    private async Task AttemptLoginAsync(string email, string pass)
    {
        SetInteractable(false);

        try
        {
            var result = await LoginUserAsync(email, pass);

            switch (result)
            {
                case LoginResult.Success:
                    string displayName = await GetNameAsync(email);
                    string user_id = await GetUserIdAsync(email);
                    if (!string.IsNullOrWhiteSpace(displayName))
                        RunDecoderState.OverrideUserName(displayName);
                    else
                        RunDecoderState.OverrideUserName(email);

                    ShowResult($"Login successful! Welcome, {displayName}", true);
                    Debug.Log($"User {email} logged in successfully as '{displayName}'.");
                    PlayerPrefs.SetString("PlayerEmail", email);
                    PlayerPrefs.SetString("PlayerName", string.IsNullOrWhiteSpace(displayName) ? email : displayName);
                    PlayerPrefs.SetString("UserID", user_id ?? "");
                    Debug.Log($"User ID: {user_id}");
                    PlayerPrefs.Save();

                    onLoginSuccess?.Invoke();

                    if (autoStartAvailabilityAfterLogin)
                    {
                        await RefreshAvailabilityNow();
                        if (refreshSeconds > 0f)
                        {
                            CancelInvoke(nameof(RefreshTick));
                            InvokeRepeating(nameof(RefreshTick), refreshSeconds, refreshSeconds);
                        }
                    }
                    break;

                case LoginResult.InvalidCredentials:
                    ShowResult("Wrong email or password.", false);
                    onLoginFailure?.Invoke("Wrong email or password.");
                    break;

                default:
                    ShowResult("Unexpected error.", false);
                    onLoginFailure?.Invoke("Unexpected error.");
                    break;
            }
        }
        catch (Exception ex)
        {
            ShowResult("Login error: " + ex.Message, false);
            onLoginFailure?.Invoke(ex.Message);
        }
        finally
        {
            SetInteractable(true);
        }
    }

    private async Task<LoginResult> LoginUserAsync(string email, string pass)
    {
        await using var conn = new MySqlConnection(ConnStr);
        await conn.OpenAsync();

        const string sql = "SELECT login_user(@p_email, @p_password);";
        await using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@p_email", email);
        cmd.Parameters.AddWithValue("@p_password", pass);

        var result = await cmd.ExecuteScalarAsync();
        int value = Convert.ToInt32(result ?? -1);

        Debug.Log($"login_user returned: {value}");

        return value switch
        {
            0 => LoginResult.Success,
            1 => LoginResult.InvalidCredentials,
            _ => LoginResult.Error
        };
    }

    private async Task<string> GetNameAsync(string email)
    {
        await using var conn = new MySqlConnection(ConnStr);
        await conn.OpenAsync();

        const string sql = "SELECT get_name(@p_email);";
        await using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@p_email", email);

        var result = await cmd.ExecuteScalarAsync();
        return result?.ToString();
    }

    private async Task<string> GetUserIdAsync(string email)
    {
        await using var conn = new MySqlConnection(ConnStr);
        await conn.OpenAsync();

        const string sql = "SELECT get_user_id(@p_email);";
        await using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@p_email", email);

        var result = await cmd.ExecuteScalarAsync();
        return result?.ToString();
    }

    private void SetInteractable(bool interactable)
    {
        if (loginButton != null)
            loginButton.interactable = interactable;
    }

    private void ShowResult(string message, bool success)
    {
        Debug.Log(message);
        if (resultText != null)
        {
            resultText.text = message;
            resultText.color = success ? Color.green : Color.red;
        }
    }

    private void RefreshTick()
    {
        if (!_isRefreshing)
            _ = RefreshAvailabilityNow();
    }

    private async Task RefreshAvailabilityNow()
    {
        _isRefreshing = true;
        try
        {
            string userIdStr = PlayerPrefs.GetString("UserID", string.Empty);
            if (!int.TryParse(userIdStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var studentId))
            {
                OverwriteAllNotes("No student ID. Please login first.");
                SetAllTeacherButtons(false);
                UpdateGlobalNext(null);
                UpdateGlobalQuizAlert(false);
                QuizPrefs.ClearQuizzes();
                return;
            }

            await FetchStudentQuizDetails(studentId);

            SaveQuizzesToPrefs(_rows, studentId);

            EvaluateAvailability();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Availability] Refresh error: {ex.Message}");
            OverwriteAllNotes("Connection error. Retrying…");
            SetAllTeacherButtons(false);
            UpdateGlobalQuizAlert(false);
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private async Task FetchStudentQuizDetails(int studentId)
    {
        _rows.Clear();

        await using var conn = new MySqlConnection(ConnStr);
        await conn.OpenAsync();

        const string sql = "CALL get_student_quiz_details(@p_student_id);";
        await using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@p_student_id", studentId);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            // Columns: quiz_id, title, notes, start_time, end_time, teacher_id
            int quizId    = reader["quiz_id"]   is DBNull ? 0 : Convert.ToInt32(reader["quiz_id"], CultureInfo.InvariantCulture);
            string title  = reader["title"]     as string ?? "";
            string notes  = reader["notes"]     as string ?? "";
            DateTime st   = reader["start_time"] is DBNull ? DateTime.MinValue : (DateTime)reader["start_time"];
            DateTime et   = reader["end_time"]   is DBNull ? DateTime.MinValue : (DateTime)reader["end_time"];
            int tId       = reader["teacher_id"] is DBNull ? 0 : Convert.ToInt32(reader["teacher_id"], CultureInfo.InvariantCulture);

            // Normalize to local time if DB is UTC
            if (dbTimesAreUtc)
            {
                if (st.Kind == DateTimeKind.Unspecified) st = DateTime.SpecifyKind(st, DateTimeKind.Utc);
                if (et.Kind == DateTimeKind.Unspecified) et = DateTime.SpecifyKind(et, DateTimeKind.Utc);
                st = st.ToLocalTime();
                et = et.ToLocalTime();
            }
            else
            {
                if (st.Kind == DateTimeKind.Unspecified) st = DateTime.SpecifyKind(st, DateTimeKind.Local);
                if (et.Kind == DateTimeKind.Unspecified) et = DateTime.SpecifyKind(et, DateTimeKind.Local);
            }

            _rows.Add(new QuizRow
            {
                quizId = quizId,
                title = title,
                notes = notes,
                startLocal = st,
                endLocal = et,
                teacherId = tId
            });
        }
    }

    private void EvaluateAvailability()
    {
        DateTime now = DateTime.Now;

        var byTeacher = new Dictionary<string, List<QuizRow>>();
        foreach (var r in _rows)
        {
            string tid = r.teacherId.ToString(CultureInfo.InvariantCulture);
            if (!byTeacher.TryGetValue(tid, out var list))
            {
                list = new List<QuizRow>();
                byTeacher[tid] = list;
            }
            list.Add(r);
        }

        bool anyActiveQuiz = false;

        // Per-teacher gate
        foreach (var entry in teacherButtons)
        {
            if (entry == null || entry.button == null) continue;

            bool has = byTeacher.TryGetValue(entry.teacherId ?? "", out var list);
            if (!has || list == null || list.Count == 0)
            {
                SetEntry(entry, false, "No quizzes found.");
                continue;
            }

            QuizRow active = null;
            QuizRow nextUpcoming = null;

            foreach (var q in list)
            {
                if (now >= q.startLocal && now <= q.endLocal)
                {
                    active = q; break;
                }
                if (q.startLocal > now)
                {
                    if (nextUpcoming == null || q.startLocal < nextUpcoming.startLocal)
                        nextUpcoming = q;
                }
            }

            if (active != null)
            {
                anyActiveQuiz = true;
                SetEntry(entry, true, $"Quiz open now: \"{active.title}\" (ends {active.endLocal:g})");
                EnsureAlertIcon(entry.button.transform as RectTransform, ref entry.spawnedAlertIcon, true);
            }
            else if (nextUpcoming != null)
            {
                var (friendly, _) = FormatRemaining(nextUpcoming.startLocal - now);
                SetEntry(entry, false, $"Next quiz: \"{nextUpcoming.title}\" — opens {nextUpcoming.startLocal:g} ({friendly} left)");
                EnsureAlertIcon(entry.button.transform as RectTransform, ref entry.spawnedAlertIcon, false);
            }
            else
            {
                SetEntry(entry, false, "No upcoming quizzes.");
                EnsureAlertIcon(entry.button.transform as RectTransform, ref entry.spawnedAlertIcon, false);
            }
        }

        QuizRow earliestUpcoming = null;
        foreach (var r in _rows)
            if (r.startLocal > now && (earliestUpcoming == null || r.startLocal < earliestUpcoming.startLocal))
                earliestUpcoming = r;

        UpdateGlobalNext(earliestUpcoming);

        UpdateGlobalQuizAlert(anyActiveQuiz);
    }

    private void SetEntry(TeacherButtonEntry entry, bool interactable, string note)
    {
        if (entry.button) entry.button.interactable = interactable;
        if (entry.notesLabel)
        {
            entry.notesLabel.text = "Teacher Notes:\n" + note;
            entry.notesLabel.color = interactable ? Color.green : Color.yellow;
        }
    }

    private void SetAllTeacherButtons(bool interactable)
    {
        foreach (var e in teacherButtons)
            if (e?.button) e.button.interactable = interactable;
    }

    private void OverwriteAllNotes(string message)
    {
        foreach (var e in teacherButtons)
            if (e?.notesLabel) e.notesLabel.text = message;
    }

    private void UpdateGlobalNext(QuizRow next)
    {
        if (!globalNextQuizLabel) return;

        DateTime now = DateTime.Now;

        bool anyActive = false;
        foreach (var r in _rows)
        {
            if (now >= r.startLocal && now <= r.endLocal)
            {
                anyActive = true;
                break;
            }
        }

        if (next == null)
        {
            globalNextQuizLabel.text = anyActive
                ? "A quiz is ongoing right now \nNo upcoming quizzes."
                : "No upcoming quizzes.";
            globalNextQuizLabel.color = anyActive ? Color.green : Color.yellow;
            return;
        }

        var (friendly, _) = FormatRemaining(next.startLocal - now);
        string header = anyActive ? "A quiz is ongoing right now \n" : "";

        globalNextQuizLabel.text =
            header + $"Next quiz:\n\"{next.title}\" (Teacher {next.teacherId}) - opens {next.startLocal:g} ({friendly} left)";

        globalNextQuizLabel.color = anyActive ? Color.green : Color.cyan;
    }

    private (string friendly, string exact) FormatRemaining(TimeSpan ts)
    {
        if (ts.TotalSeconds <= 1) return ("now", "0s");

        int d = ts.Days, h = ts.Hours, m = ts.Minutes, s = ts.Seconds;
        string friendly = d > 0 ? $"{d}d {h}h" : (h > 0 ? $"{h}h {m}m" : $"{m}m {s}s");
        string exact = $"{d}d {h}h {m}m {s}s";
        return (friendly, exact);
    }

    private void EnsureAlertIcon(RectTransform parent, ref RectTransform instance, bool visible)
    {
        if (parent == null || alertIconPrefab == null) return;

        if (instance == null)
        {
            instance = Instantiate(alertIconPrefab);
            instance.gameObject.name = "AlertIcon";
            instance.SetParent(parent, false);

            instance.anchorMin = new Vector2(0.5f, 0.5f);
            instance.anchorMax = new Vector2(0.5f, 0.5f);
            instance.pivot = new Vector2(0.5f, 0.5f);
            instance.anchoredPosition = new Vector2(125f, 0f);
            instance.sizeDelta = new Vector2(37.5f, 37.5f);
            instance.SetAsLastSibling();
        }

        if (instance) instance.gameObject.SetActive(visible);
    }

    private void UpdateGlobalQuizAlert(bool anyActiveQuiz)
    {
        if (globalQuizButton == null || alertIconPrefab == null)
        {
            return;
        }

        var parent = globalQuizButton.transform as RectTransform;
        EnsureAlertIcon(parent, ref _globalQuizAlertIcon, anyActiveQuiz);
    }

    private void SaveQuizzesToPrefs(List<QuizRow> rows, int studentId)
    {
        var list = new List<QuizRowDTO>(rows.Count);
        foreach (var r in rows)
        {
            // Persist in UTC to be consistent across scenes/machines
            var startUtc = r.startLocal.Kind == DateTimeKind.Utc ? r.startLocal : r.startLocal.ToUniversalTime();
            var endUtc   = r.endLocal.Kind   == DateTimeKind.Utc ? r.endLocal   : r.endLocal.ToUniversalTime();

            list.Add(new QuizRowDTO
            {
                quizId = r.quizId,
                title = r.title,
                notes = r.notes,
                startTimeIso = startUtc.ToString("o"),
                endTimeIso = endUtc.ToString("o"),
                teacherId = r.teacherId
            });
        }

        var wrapper = new QuizListWrapper { items = list };
        string json = JsonUtility.ToJson(wrapper);

        PlayerPrefs.SetString(QuizPrefs.QUIZ_DATA_KEY, json);
        PlayerPrefs.SetString(QuizPrefs.QUIZ_DATA_SAVED_AT, DateTime.UtcNow.ToString("o"));
        PlayerPrefs.SetInt(QuizPrefs.QUIZ_DATA_STUDENT_ID, studentId);
        PlayerPrefs.Save();

        Debug.Log($"[Availability] Saved {list.Count} quizzes for student {studentId}.");
    }
}

public static class QuizPrefs
{
    public const string QUIZ_DATA_KEY = "QuizData";
    public const string QUIZ_DATA_SAVED_AT = "QuizDataSavedAtUtc";
    public const string QUIZ_DATA_STUDENT_ID = "QuizDataStudentId";

    [Serializable]
    private class QuizListWrapper { public List<MySqlConnectorClient.QuizRowDTO> items; }

    public static List<MySqlConnectorClient.QuizRowDTO> LoadQuizzes()
    {
        string json = PlayerPrefs.GetString(QUIZ_DATA_KEY, "");
        if (string.IsNullOrEmpty(json)) return new List<MySqlConnectorClient.QuizRowDTO>();

        var wrapper = JsonUtility.FromJson<QuizListWrapper>(json);
        return wrapper?.items ?? new List<MySqlConnectorClient.QuizRowDTO>();
    }

    public static DateTime ParseIso(string iso)
        => DateTime.Parse(iso, null, DateTimeStyles.RoundtripKind);

    public static DateTime? GetSavedAtUtc()
    {
        string iso = PlayerPrefs.GetString(QUIZ_DATA_SAVED_AT, "");
        if (string.IsNullOrEmpty(iso)) return null;
        return DateTime.Parse(iso, null, DateTimeStyles.RoundtripKind);
    }

    public static int GetStudentId() => PlayerPrefs.GetInt(QUIZ_DATA_STUDENT_ID, -1);

    public static void ClearQuizzes()
    {
        PlayerPrefs.DeleteKey(QUIZ_DATA_KEY);
        PlayerPrefs.DeleteKey(QUIZ_DATA_SAVED_AT);
        PlayerPrefs.DeleteKey(QUIZ_DATA_STUDENT_ID);
        PlayerPrefs.Save();
    }
}
