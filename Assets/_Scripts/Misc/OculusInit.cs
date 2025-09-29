/// <summary>
/// Appathon CSE-EJUST Challenge Summer 2025
/// 
/// Initializes the Oculus Platform SDK and retrieves the logged-in user's name. (Optional)
/// /// If the Platform SDK is not used or user retrieval fails, a fallback username is applied.
/// 
/// Notes:
/// - Ensure the Oculus Platform SDK is integrated into your Unity project for this script to function correctly.
/// - Since RunDecoderState has a static method OverrideUserName(string name) to set the username, disable it
/// so this script can be used to set the username from Oculus Platform or fallback to a default value instead.
/// </summary>
using UnityEngine;
using Oculus.Platform;
using Oculus.Platform.Models;

public class OculusInit : MonoBehaviour
{
    [Header("Oculus Platform (optional)")]
    [Tooltip("Set your Oculus App ID if you use the Platform SDK")]
    public string oculusAppId = "";

    [Header("Username fallback")]
    [Tooltip("Used if Platform SDK is absent or user lookup fails")]
    public string fallbackUserName = "You";

    private void Awake()
    {
        RunDecoderState.OverrideUserName(fallbackUserName);

        try
        {
            if (!Core.IsInitialized())
            {
                if (!string.IsNullOrEmpty(oculusAppId))
                    Core.Initialize(oculusAppId);
                else
                    Debug.LogWarning("[OculusInit] No App ID set; skipping Core.Initialize()");
            }

            // (Optional) entitlement check
            Entitlements.IsUserEntitledToApplication().OnComplete(_ => {});

            Users.GetLoggedInUser().OnComplete(r =>
            {
                if (!r.IsError && r.Data != null)
                {
                    var name = string.IsNullOrWhiteSpace(r.Data.OculusID)
                        ? r.Data.DisplayName
                        : r.Data.OculusID;

                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        RunDecoderState.OverrideUserName(name.Trim());
                        Debug.Log("[OculusInit] Username set to: " + name);
                    }
                }
                else
                {
                    Debug.LogWarning("[OculusInit] Failed to get user; using fallback.");
                }
            });
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[OculusInit] Platform init failed: " + ex.Message);
        }
    }
}