using UnityEngine;
using System.Linq;

public class OVRLipSyncContextMorphTarget : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Primary Skinned Mesh Renderer target to be driven by Oculus LipSync")]
    public SkinnedMeshRenderer skinnedMeshRendererPrimary = null;

    [SerializeField]
    [Tooltip("Secondary Skinned Mesh Renderer target to be driven by Oculus LipSync")]
    public SkinnedMeshRenderer skinnedMeshRendererSecondary = null;

    [HideInInspector]
    public SkinnedMeshRenderer skinnedMeshRenderer = null; // For backward compatibility with editor script

    [Tooltip("Blendshape index to trigger for each viseme.")]
    public int[] visemeToBlendTargets = Enumerable.Range(0, OVRLipSync.VisemeCount).ToArray();

    [Tooltip("Enable using the test keys defined below to manually trigger each viseme.")]
    public bool enableVisemeTestKeys = false;

    [Tooltip("Test keys used to manually trigger an individual viseme - by default the QWERTY row of a US keyboard.")]
    public KeyCode[] visemeTestKeys =
    {
        KeyCode.BackQuote,
        KeyCode.Tab,
        KeyCode.Q,
        KeyCode.W,
        KeyCode.E,
        KeyCode.R,
        KeyCode.T,
        KeyCode.Y,
        KeyCode.U,
        KeyCode.I,
        KeyCode.O,
        KeyCode.P,
        KeyCode.LeftBracket,
        KeyCode.RightBracket,
        KeyCode.Backslash,
    };

    [Tooltip("Test key used to manually trigger laughter and visualize the results")]
    public KeyCode laughterKey = KeyCode.CapsLock;

    [Tooltip("Blendshape index to trigger for laughter")]
    public int laughterBlendTarget = OVRLipSync.VisemeCount;

    [Range(0.0f, 1.0f)]
    [Tooltip("Laughter probability threshold above which the laughter blendshape will be activated")]
    public float laughterThreshold = 0.5f;

    [Range(0.0f, 3.0f)]
    [Tooltip("Laughter animation linear multiplier, the final output will be clamped to 1.0")]
    public float laughterMultiplier = 1.5f;

    [Range(1, 100)]
    [Tooltip("Smoothing of 1 will yield only the current predicted viseme, 100 will yield an extremely smooth viseme response.")]
    public int smoothAmount = 70;

    [Tooltip("Enable debug logging for viseme and laughter weights")]
    [SerializeField] private bool debugLog = false;

    private OVRLipSyncContextBase lipsyncContext = null;
    private int eyesClosedIndexPrimary = -1; // Index of eyesClosed blendshape for primary renderer
    private int eyesClosedIndexSecondary = -1; // Index of eyesClosed blendshape for secondary renderer

    void Start()
    {
        // Assign skinnedMeshRenderer to primary for editor compatibility
        skinnedMeshRenderer = skinnedMeshRendererPrimary;

        // Check for valid primary SkinnedMeshRenderer
        if (skinnedMeshRendererPrimary == null)
        {
            Debug.LogError("LipSyncContextMorphTarget.Start Error: Please set the primary Skinned Mesh Renderer to be controlled!");
            return;
        }

        // Check for valid secondary SkinnedMeshRenderer
        if (skinnedMeshRendererSecondary == null)
        {
            Debug.LogWarning("LipSyncContextMorphTarget.Start Warning: Secondary Skinned Mesh Renderer is not set. Only primary will be controlled.");
        }

        lipsyncContext = GetComponent<OVRLipSyncContextBase>();
        if (lipsyncContext == null)
        {
            Debug.LogError("LipSyncContextMorphTarget.Start Error: No OVRLipSyncContext component on this object!");
        }
        else
        {
            lipsyncContext.Smoothing = smoothAmount;
        }

        // Find eyesClosed blendshape index for primary renderer
        if (skinnedMeshRendererPrimary != null)
        {
            var meshPrimary = skinnedMeshRendererPrimary.sharedMesh;
            for (int i = 0; i < meshPrimary.blendShapeCount; i++)
            {
                if (meshPrimary.GetBlendShapeName(i).Equals("eyesClosed", System.StringComparison.OrdinalIgnoreCase))
                {
                    eyesClosedIndexPrimary = i;
                    break;
                }
            }
            if (eyesClosedIndexPrimary >= 0 && debugLog)
                Debug.Log($"Primary renderer: Found eyesClosed blendshape at index {eyesClosedIndexPrimary}");
        }

        // Find eyesClosed blendshape index for secondary renderer
        if (skinnedMeshRendererSecondary != null)
        {
            var meshSecondary = skinnedMeshRendererSecondary.sharedMesh;
            for (int i = 0; i < meshSecondary.blendShapeCount; i++)
            {
                if (meshSecondary.GetBlendShapeName(i).Equals("eyesClosed", System.StringComparison.OrdinalIgnoreCase))
                {
                    eyesClosedIndexSecondary = i;
                    break;
                }
            }
            if (eyesClosedIndexSecondary >= 0 && debugLog)
                Debug.Log($"Secondary renderer: Found eyesClosed blendshape at index {eyesClosedIndexSecondary}");
        }
    }

    void LateUpdate()
    {
        // Ensure skinnedMeshRenderer stays in sync with primary for editor compatibility
        if (skinnedMeshRenderer != skinnedMeshRendererPrimary)
        {
            skinnedMeshRenderer = skinnedMeshRendererPrimary;
        }

        if (lipsyncContext != null && (skinnedMeshRendererPrimary != null || skinnedMeshRendererSecondary != null))
        {
            // Preserve eyesClosed weights before applying visemes/laughter
            float eyesClosedWeightPrimary = eyesClosedIndexPrimary >= 0 && skinnedMeshRendererPrimary != null
                ? skinnedMeshRendererPrimary.GetBlendShapeWeight(eyesClosedIndexPrimary)
                : 0f;
            float eyesClosedWeightSecondary = eyesClosedIndexSecondary >= 0 && skinnedMeshRendererSecondary != null
                ? skinnedMeshRendererSecondary.GetBlendShapeWeight(eyesClosedIndexSecondary)
                : 0f;

            // Get the current viseme frame
            OVRLipSync.Frame frame = lipsyncContext.GetCurrentPhonemeFrame();
            if (frame != null)
            {
                // Apply visemes and laughter to both renderers
                if (skinnedMeshRendererPrimary != null)
                {
                    SetVisemeToMorphTarget(skinnedMeshRendererPrimary, eyesClosedIndexPrimary, frame);
                    SetLaughterToMorphTarget(skinnedMeshRendererPrimary, eyesClosedIndexPrimary, frame);
                    // Restore eyesClosed weight for primary
                    if (eyesClosedIndexPrimary >= 0)
                    {
                        skinnedMeshRendererPrimary.SetBlendShapeWeight(eyesClosedIndexPrimary, eyesClosedWeightPrimary);
                    }
                }
                if (skinnedMeshRendererSecondary != null)
                {
                    SetVisemeToMorphTarget(skinnedMeshRendererSecondary, eyesClosedIndexSecondary, frame);
                    SetLaughterToMorphTarget(skinnedMeshRendererSecondary, eyesClosedIndexSecondary, frame);
                    // Restore eyesClosed weight for secondary
                    if (eyesClosedIndexSecondary >= 0)
                    {
                        skinnedMeshRendererSecondary.SetBlendShapeWeight(eyesClosedIndexSecondary, eyesClosedWeightSecondary);
                    }
                }

                if (debugLog)
                {
                    Debug.Log($"Visemes: {string.Join(", ", frame.Visemes.Select(v => v.ToString("F2")))}");
                    if (eyesClosedIndexPrimary >= 0)
                        Debug.Log($"Primary renderer: eyesClosed weight preserved: {eyesClosedWeightPrimary}");
                    if (eyesClosedIndexSecondary >= 0)
                        Debug.Log($"Secondary renderer: eyesClosed weight preserved: {eyesClosedWeightSecondary}");
                }
            }

            // Update smoothing value
            if (smoothAmount != lipsyncContext.Smoothing)
            {
                lipsyncContext.Smoothing = smoothAmount;
            }

            // Check for test keys
            if (enableVisemeTestKeys)
            {
                CheckForKeys();
            }
        }
    }

    void CheckForKeys()
    {
        for (int i = 0; i < OVRLipSync.VisemeCount; ++i)
        {
            CheckVisemeKey(visemeTestKeys[i], i, 100);
        }
        CheckLaughterKey();
    }

    void SetVisemeToMorphTarget(SkinnedMeshRenderer renderer, int eyesClosedIndex, OVRLipSync.Frame frame)
    {
        for (int i = 0; i < visemeToBlendTargets.Length; i++)
        {
            if (visemeToBlendTargets[i] != -1 && visemeToBlendTargets[i] != eyesClosedIndex)
            {
                renderer.SetBlendShapeWeight(
                    visemeToBlendTargets[i],
                    frame.Visemes[i] * 100.0f);
            }
        }
    }

    void SetLaughterToMorphTarget(SkinnedMeshRenderer renderer, int eyesClosedIndex, OVRLipSync.Frame frame)
    {
        if (laughterBlendTarget != -1 && laughterBlendTarget != eyesClosedIndex)
        {
            float laughterScore = frame.laughterScore;
            laughterScore = laughterScore < laughterThreshold ? 0.0f : laughterScore - laughterThreshold;
            laughterScore = Mathf.Min(laughterScore * laughterMultiplier, 1.0f);
            laughterScore *= 1.0f / laughterThreshold;

            renderer.SetBlendShapeWeight(
                laughterBlendTarget,
                laughterScore * 100.0f);
        }
    }

    void CheckVisemeKey(KeyCode key, int viseme, int amount)
    {
        if (Input.GetKeyDown(key))
        {
            lipsyncContext.SetVisemeBlend(visemeToBlendTargets[viseme], amount);
            if (debugLog) Debug.Log($"Viseme {viseme} triggered via key {key}");
        }
        if (Input.GetKeyUp(key))
        {
            lipsyncContext.SetVisemeBlend(visemeToBlendTargets[viseme], 0);
        }
    }

    void CheckLaughterKey()
    {
        if (Input.GetKeyDown(laughterKey))
        {
            lipsyncContext.SetLaughterBlend(100);
            if (debugLog) Debug.Log($"Laughter triggered via key {laughterKey}");
        }
        if (Input.GetKeyUp(laughterKey))
        {
            lipsyncContext.SetLaughterBlend(0);
        }
    }
}