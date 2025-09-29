/// <summary>
/// Appathon CSE-EJUST Challenge Summer 2025
/// 
/// Manages multiple UI menus, allowing switching between them with optional delays.
/// /// - Menus are defined in the Unity Inspector with a name and corresponding GameObject.
/// /// - The script supports an optional delay when switching from a specified "Login" menu to another menu.
/// 
/// Usage:
/// - Use the SwitchMenu(string menuName) method to change menus by name.
/// - Use the HideAllMenus() method to hide all menus.
/// </summary>
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MenuHandler : MonoBehaviour
{
    [System.Serializable]
    public class Menu
    {
        public string menuName;
        public GameObject menuObject;
    }

    [Header("Menus")]
    [SerializeField] private List<Menu> menus = new List<Menu>();

    [Header("Delay Settings")]
    [SerializeField] private string loginMenuName = "Login";
    [SerializeField] private float switchDelayFromLoginSeconds = 3f;

    private readonly Dictionary<string, GameObject> menuLookup = new Dictionary<string, GameObject>();
    private GameObject currentMenu;
    private string currentMenuName;
    private bool isSwitching;

    private void Awake()
    {
        foreach (var m in menus)
        {
            if (m != null && !string.IsNullOrEmpty(m.menuName) && m.menuObject != null)
            {
                menuLookup[m.menuName] = m.menuObject;
                m.menuObject.SetActive(false);
            }
        }

        if (menus.Count > 0 && menus[0]?.menuObject != null)
        {
            currentMenu = menus[0].menuObject;
            currentMenuName = menus[0].menuName;
            currentMenu.SetActive(true);
        }
    }

    public void SwitchMenu(string menuName)
    {
        if (!menuLookup.ContainsKey(menuName))
        {
            Debug.LogError($"MenuHandler: No menu found with name '{menuName}'");
            return;
        }
        if (isSwitching || currentMenu == menuLookup[menuName]) return;

        StartCoroutine(SwitchRoutine(menuName));
    }

    public void HideAllMenus()
    {
        foreach (var kvp in menuLookup)
            kvp.Value.SetActive(false);

        currentMenu = null;
        currentMenuName = null;
    }

    private IEnumerator SwitchRoutine(string targetMenuName)
    {
        isSwitching = true;
        
        // Login special case
        if (!string.IsNullOrEmpty(currentMenuName) &&
            currentMenuName == loginMenuName &&
            switchDelayFromLoginSeconds > 0f)
        {
            float t = 0f;
            while (t < switchDelayFromLoginSeconds)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        if (currentMenu != null)
            currentMenu.SetActive(false);

        currentMenu = menuLookup[targetMenuName];
        currentMenuName = targetMenuName;
        currentMenu.SetActive(true);

        isSwitching = false;
    }
}
