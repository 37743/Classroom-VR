/// <summary>
/// Appathon CSE-EJUST Challenge Summer 2025
/// 
/// Simple automatic blinking behavior for characters with blendshapes.
/// /// - Attach to a GameObject with a SkinnedMeshRenderer that has a blendshape named "eyesClosed".
/// /// - Configurable parameters for blink intervals, duration, and weight.
/// /// - Optionally suppress blinking while the character is talking.
/// /// - Can combine with existing blendshape weights to avoid conflicts.
/// 
/// Important Notes:
/// - Ensure the SkinnedMeshRenderer and blendshape name are correctly set.
/// - The script uses a coroutine for timing to avoid blocking the main thread.
/// </summary>
using System.Collections;
using UnityEngine;

public class SimpleAutoBlink : MonoBehaviour
{
    [SerializeField] private SkinnedMeshRenderer faceRenderer;

    [SerializeField] private float minInterval = 2.5f;
    [SerializeField] private float maxInterval = 6.0f;
    [SerializeField] private float blinkDuration = 0.20f;
    [SerializeField] private float holdClosed = 0.04f;

    [Range(0, 100)] [SerializeField] private float maxBlinkWeight = 100f;

    [SerializeField] private bool enableBlinking = true;
    [SerializeField] private bool suppressWhileTalking = false;
    [SerializeField] private bool combineWithExisting = true;
    [SerializeField] private bool debugLog = false;

    public bool IsTalking { get; set; } = false;

    private const string BlinkShapeName = "eyesClosed";
    private int blinkIdx = -1;

    private float desiredWeight = 0f;
    private Coroutine loop;

    void Awake()
    {
        if (!faceRenderer) faceRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
        if (!faceRenderer || !faceRenderer.sharedMesh)
        {
            enabled = false;
            Debug.LogWarning($"{name}: No valid SkinnedMeshRenderer or mesh found.");
            return;
        }

        ResolveBlinkIndex();
        if (blinkIdx < 0)
        {
            enabled = false;
            return;
        }

        desiredWeight = 0f;
        faceRenderer.SetBlendShapeWeight(blinkIdx, 0f);
        if (debugLog) Debug.Log($"{name}: Using {faceRenderer.sharedMesh.name} -> {BlinkShapeName} index {blinkIdx}");
    }

    void OnEnable()
    {
        if (blinkIdx < 0) return;
        if (loop == null) loop = StartCoroutine(BlinkLoop());
    }

    void OnDisable()
    {
        if (loop != null) StopCoroutine(loop);
        loop = null;

        desiredWeight = 0f;
        if (blinkIdx >= 0) faceRenderer.SetBlendShapeWeight(blinkIdx, 0f);
    }

    void OnValidate()
    {
        if (faceRenderer && faceRenderer.sharedMesh && !Application.isPlaying)
        {
            ResolveBlinkIndex();
        }
    }

    public void BlinkNow(float duration = -1f)
    {
        if (!gameObject.activeInHierarchy || blinkIdx < 0) return;
        if (duration <= 0f) duration = blinkDuration;
        StartCoroutine(BlinkOnce(duration, holdClosed));
    }

    private IEnumerator BlinkLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(minInterval, maxInterval));
            if (!enableBlinking) continue;
            if (suppressWhileTalking && IsTalking) continue;

            yield return BlinkOnce(blinkDuration, holdClosed);

            if (Random.value < 0.15f)
            {
                yield return new WaitForSeconds(Random.Range(0.05f, 0.12f));
                yield return BlinkOnce(blinkDuration * 0.9f, holdClosed * 0.5f);
            }
        }
    }

    private IEnumerator BlinkOnce(float duration, float closedHold)
    {
        float half = Mathf.Max(0.0001f, duration * 0.5f);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / half;
            float w = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI * 0.5f);
            desiredWeight = w * maxBlinkWeight;
            yield return null;
        }

        if (closedHold > 0f) yield return new WaitForSeconds(closedHold);

        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / half;
            float w = 1f - Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI * 0.5f);
            desiredWeight = w * maxBlinkWeight;
            yield return null;
        }

        desiredWeight = 0f;
    }

    void LateUpdate()
    {
        if (blinkIdx < 0) return;
        float final = desiredWeight;
        if (combineWithExisting)
            final = Mathf.Max(faceRenderer.GetBlendShapeWeight(blinkIdx), desiredWeight);
        faceRenderer.SetBlendShapeWeight(blinkIdx, final);
        if (debugLog) Debug.Log($"{name}: Blink weight set to {final} for index {blinkIdx}");
    }

    private void ResolveBlinkIndex()
    {
        blinkIdx = -1;
        var mesh = faceRenderer.sharedMesh;
        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            if (mesh.GetBlendShapeName(i).Equals(BlinkShapeName, System.StringComparison.OrdinalIgnoreCase))
            {
                blinkIdx = i;
                break;
            }
        }
        if (blinkIdx < 0) Debug.LogWarning($"{name}: Blendshape \"{BlinkShapeName}\" not found on {mesh.name}.");
    }
}