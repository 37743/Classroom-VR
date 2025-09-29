/// <summary>
/// Appathon CSE-EJUST Challenge Summer 2025
/// 
/// Animates a character to look at a specified target using Unity's Animation Rigging system.
/// /// The target can be set dynamically, and the character will smoothly transition its head and eyes to focus on the target.
/// /// If no target is specified, the character will look straight ahead at a default position.
/// /// The look-at behavior can be started and stopped via public methods.
/// </summary>
using UnityEngine;
using UnityEngine.Animations.Rigging;

public class LookAtObjAnim : MonoBehaviour
{
    [SerializeField] private Rig rig;
    [SerializeField] private Transform headLookAtTarget;
    [SerializeField] private float lookLerp = 2f;
    [SerializeField] private float eyeHeight = 1.5f;

    private bool isLooking = false;
    private Transform liveTarget;

    void Update()
    {
        float targetWeight = isLooking ? 1f : 0f;
        rig.weight = Mathf.Lerp(rig.weight, targetWeight, Time.deltaTime * lookLerp);

        Vector3 wanted = (isLooking && liveTarget != null) ? liveTarget.position : transform.position + transform.forward * 2f + Vector3.up * eyeHeight;

        headLookAtTarget.position = Vector3.Lerp(headLookAtTarget.position, wanted, Time.deltaTime * lookLerp);
    }

    public void StartLookingAtTarget(Transform target)
    {
        isLooking = true;
        Debug.Log("Start looking at target: " + target.position);
        liveTarget = target;
    }

    public void StopLookingAtTarget()
    {
        isLooking = false;
        liveTarget = null;
    }
}