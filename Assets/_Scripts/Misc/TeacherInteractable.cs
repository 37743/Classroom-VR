/// <summary>
/// Appathon CSE-EJUST Challenge Summer 2025
/// 
/// Class managing a teacher character that can explain topics to an interactor.
/// /// It handles animations (e.g. explaining, idling) and makes the character look at the interactor during explanations.
/// /// The character stops explaining either when the speech ends (via PiperDriver event) or when the animation finishes.
/// /// If neither event occurs, a timeout ensures the character stops explaining after a set duration.
/// /// The class uses an Animator for character animations and a PiperDriver for speech synthesis.
/// 
/// Important Notes:
/// - Since some of the animations have keyframes for the same mesh, ensure that the animations that you need
/// are given priority in the Animator controller, or by using LateUpdate as we are doing right now.
/// - Failure to use LateUpdate may result in unexpected behavior, such animations not playing correctly as well
/// as LipSync visemes not working as expected.
/// </summary>
using UnityEngine;
using System.Collections;

public class TeacherInteractable : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Piper.Samples.PiperDriver piperDriver;
    [SerializeField] private float lookHoldAfterExplain = 5f;

    private Animator animator;
    private LookAtObjAnim lookAtObjAnim;

    private float lastExplainTime;
    private bool isMonitoring;
    private bool isExplaining;

    private const float EXPLAIN_TIMEOUT = 45f;

    private Coroutine monitorRoutine;
    private Coroutine delayedStopLookRoutine;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        lookAtObjAnim = GetComponent<LookAtObjAnim>();

        lastExplainTime = -EXPLAIN_TIMEOUT;
        isMonitoring = false;
        isExplaining = false;

        if (piperDriver != null)
        {
            piperDriver.OnSpeechEnded += HandleSpeechEnded;
        }
        else
        {
            Debug.LogWarning("[TeacherInteractable] PiperDriver not assigned. Will rely on animation end/timeout.");
        }
    }

    private void OnDestroy()
    {
        if (piperDriver != null)
            piperDriver.OnSpeechEnded -= HandleSpeechEnded;
    }

    public void StartExplaining(Transform interactorTransform)
    {
        // If a previous delayed stop-look is pending, cancel it
        if (delayedStopLookRoutine != null)
        {
            StopCoroutine(delayedStopLookRoutine);
            delayedStopLookRoutine = null;
        }

        string trigger = (Random.value > 0.5f) ? "short_explain" : "long_explain";
        animator.SetTrigger(trigger);

        lookAtObjAnim.StartLookingAtTarget(interactorTransform);

        lastExplainTime = Time.time;
        isExplaining = true;

        if (!isMonitoring)
            monitorRoutine = StartCoroutine(MonitorExplainTimeout(trigger));
    }

    private void HandleSpeechEnded()
    {
        if (isExplaining)
        {
            Debug.Log("[TeacherInteractable] Speech ended → stopping explanation.");
            StopExplaining();
        }
    }

    public void StopExplaining()
    {
        animator.SetTrigger("idle");

        if (delayedStopLookRoutine != null)
        {
            StopCoroutine(delayedStopLookRoutine);
        }
        delayedStopLookRoutine = StartCoroutine(DelayedStopLook());

        // Stop the timeout monitor
        if (monitorRoutine != null)
        {
            StopCoroutine(monitorRoutine);
            monitorRoutine = null;
        }

        isMonitoring = false;
        isExplaining = false;

        Debug.Log("Stopped explaining (idle triggered).");
    }

    private IEnumerator DelayedStopLook()
    {
        yield return new WaitForSeconds(lookHoldAfterExplain);
        lookAtObjAnim.StopLookingAtTarget();
        delayedStopLookRoutine = null;
    }

    private IEnumerator MonitorExplainTimeout(string explainTrigger)
    {
        isMonitoring = true;

        while (Time.time - lastExplainTime < EXPLAIN_TIMEOUT)
        {
            var stateInfo = animator.GetCurrentAnimatorStateInfo(0);

            if (stateInfo.IsName(explainTrigger) && stateInfo.normalizedTime >= 1f)
            {
                break;
            }

            yield return null;
        }

        if (isExplaining)
        {
            Debug.Log("Explain finished or timeout reached → going idle.");
            StopExplaining();
        }

        isMonitoring = false;
        monitorRoutine = null;
    }
}