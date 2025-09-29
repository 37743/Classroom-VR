/// <summary>
/// Appathon CSE-EJUST Challenge Summer 2025
/// 
/// Handles player interactions, including asking biology questions and interacting with teacher NPCs.
/// /// It uses the BiologyQuestionClient to send questions and receive responses. (Requires setup in the Unity Inspector)
/// /// When a response is received, it checks for nearby TeacherInteractable objects in proximity and initiates their explanation behavior.
/// </summary>
using UnityEngine;

public class PlayerInteractor : MonoBehaviour
{
    [Header("Biology Question Client")]
    [SerializeField] private BiologyQuestionClient biologyClient;
    
    [Header("Interaction Settings")]
    [SerializeField] private float interactRange = 4f;

    private void Start()
    {
        if (biologyClient != null)
        {
            biologyClient.OnResponseReceived += HandleResponseReceived;
        }
        else
        {
            Debug.LogWarning("BiologyQuestionClient is not assigned.");
        }
    }

    private void OnDestroy()
    {
        if (biologyClient != null)
        {
            biologyClient.OnResponseReceived -= HandleResponseReceived;
        }
    }

    public void AskBiologyQuestionAndInteract(string question)
    {
        Debug.Log("Asking biology question: " + question);

        if (biologyClient != null)
        {
            biologyClient.AskBiologyQuestion(question);
        }
        else
        {
            Debug.LogWarning("BiologyQuestionClient is not assigned.");
        }
    }

    private void HandleResponseReceived(string response)
    {
        Debug.Log("Received response: " + response);

        Collider[] hitColliders = Physics.OverlapSphere(transform.position, interactRange);
        foreach (var hitCollider in hitColliders)
        {
            if (hitCollider.TryGetComponent<TeacherInteractable>(out TeacherInteractable teacher))
            {
                Debug.Log("Teacher found, starting explanation");
                teacher.StartExplaining(transform);
                // teacher.StopExplainingAfterSeconds(30f);
            }
        }
    }
}