/// <summary>
/// Appathon CSE-EJUST Challenge Summer 2025
/// 
/// Client for sending biology questions to a server and processing responses.
/// /// It monitors a TextMeshProUGUI component for new questions prefixed with the player's name in brackets.
/// /// When a new question is detected, it sends the question to a specified server and displays the response.
/// /// It also integrates with PiperDriver to speak the response aloud.
/// 
/// Notes:
/// /// - Ensure the server address and port are correctly set for your backend.
/// /// - The player's display name is retrieved from PlayerPrefs; ensure it is set appropriately. *from login menu*
/// /// - The script uses a coroutine for network communication to avoid blocking the main thread.
/// </summary>
using UnityEngine;
using System.Net.Sockets;
using System.Text;
using System;
using System.IO;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using Piper.Samples;

public class BiologyQuestionClient : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI responseText;
    [SerializeField] private float checkInterval = 1f;
    [SerializeField] private string serverAddress = "26.235.96.91";
    [SerializeField] private PiperDriver piperDriver;

    private HashSet<string> sentPrompts = new HashSet<string>();
    private float lastCheckTime;
    private string lastProcessedText = "";

    // Cached display name from PlayerPrefs
    [SerializeField] private string playerPrefsDisplayNameKey = "displayName";
    [SerializeField] private string teacherName = "Mr. Rashed";
    private string displayName;
    private string requiredBracketedPrefix;

    public delegate void ResponseReceivedHandler(string response);
    public event ResponseReceivedHandler OnResponseReceived;

    public void AskBiologyQuestion(string prompt)
    {
        if (!sentPrompts.Contains(prompt))
        {
            StartCoroutine(SendQuestionCoroutine(prompt));
            sentPrompts.Add(prompt);
        }
    }

    private void Awake()
    {
        RunDecoderState.Reload();

        displayName = PlayerPrefs.GetString(playerPrefsDisplayNameKey, "You")?.Trim();
        if (string.IsNullOrEmpty(displayName))
            displayName = "You";

        requiredBracketedPrefix = "[" + displayName + "]";
        Debug.Log($"[BiologyQuestionClient] Using display name: '{displayName}' with prefix '{requiredBracketedPrefix}'");
    }

    private void Update()
    {
        if (Time.time - lastCheckTime >= checkInterval && responseText != null)
        {
            ProcessBoardText();
            lastCheckTime = Time.time;
        }
    }

    private void ProcessBoardText()
    {
        if (responseText == null || string.IsNullOrEmpty(responseText.text))
            return;

        if (responseText.text == lastProcessedText)
            return;

        string[] sentences = responseText.text.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (sentences.Length == 0)
            return;

        string latestSentence = sentences[sentences.Length - 1].Trim();
        lastProcessedText = responseText.text;

        if (string.IsNullOrEmpty(latestSentence))
            return;

        int colonIndex = latestSentence.IndexOf(':');
        if (colonIndex < 0 || colonIndex >= latestSentence.Length - 1)
            return;

        string prefix = latestSentence.Substring(0, colonIndex).Trim();
        string question = latestSentence.Substring(colonIndex + 1).Trim();

        if (!prefix.Equals(requiredBracketedPrefix, StringComparison.Ordinal) || string.IsNullOrEmpty(question))
            return;

        AskBiologyQuestion(question);
    }

    private IEnumerator SendQuestionCoroutine(string prompt)
    {
        TcpClient client = null;
        NetworkStream stream = null;

        client = new TcpClient();
        yield return StartCoroutine(ConnectToServer(client, serverAddress, 8000));

        if (!client.Connected)
        {
            UpdateResponseText("Failed to connect to server.");
            yield break;
        }

        try
        {
            string jsonData = "{\"query\": \"" + prompt.Replace("\"", "\\\"") + "\"}";
            byte[] data = Encoding.UTF8.GetBytes(jsonData);

            stream = client.GetStream();

            byte[] lengthBytes = BitConverter.GetBytes((uint)data.Length);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(lengthBytes);
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

            if (BitConverter.IsLittleEndian)
                Array.Reverse(lengthBuffer);
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
            var jsonResponse = JsonUtility.FromJson<BiologyResponse>(responseJson);

            UpdateResponseText("[" + teacherName + "]: " + jsonResponse.assistant.content);
            OnResponseReceived?.Invoke(jsonResponse.assistant.content);
            piperDriver?.Speak(jsonResponse.assistant.content);
        }
        catch (Exception e)
        {
            UpdateResponseText("Error: " + e.Message);
            OnResponseReceived?.Invoke("Error: " + e.Message);
        }
        finally
        {
            stream?.Close();
            client?.Close();
        }
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

    private void UpdateResponseText(string text)
    {
        if (responseText != null)
        {
            responseText.text += (string.IsNullOrEmpty(responseText.text) ? "" : "\n") + text;
        }
        else
        {
            Debug.LogWarning("TextMeshProUGUI component is not assigned.");
        }
    }

    [System.Serializable]
    private class BiologyResponse
    {
        public AssistantData assistant;
    }

    [System.Serializable]
    private class AssistantData
    {
        public string content;
    }
}
