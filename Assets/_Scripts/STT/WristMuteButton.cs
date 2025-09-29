/// <summary>
/// Appathon CSE-EJUST Challenge Summer 2025
/// 
/// A toggle button to mute/unmute the microphone, designed for use on a VR wrist UI
/// Usage:
/// - Assign a Unity UI Toggle component to the _toggle field (or let it auto-assign from the same GameObject)
/// /// - Assign a MicRecorder component to the micRecorder field
/// /// - Optionally assign onToggleOn and onToggleOff UnityEvents to respond to actual mic state changes
/// /// - Optionally assign onSprite and offSprite for visual feedback, along with targetImage
/// /// - Optionally adjust minInterval to debounce rapid toggles
/// /// The button will keep the toggle state in sync with the MicRecorder's IsListening property,
/// 
/// /// firing events only when the actual mic state changes.
/// </summary>
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Oculus.Interaction.Samples
{
    public class WristMuteButton : MonoBehaviour
    {
        [Header("UI Toggle on the wrist")]
        [SerializeField] private Toggle _toggle;

        [Header("Events (fire ONLY when actual mic state changes)")]
        public UnityEvent onToggleOn;
        public UnityEvent onToggleOff;

        [Header("Debounce")]
        [Tooltip("Minimum time between accepted toggle changes (seconds).")]
        [SerializeField] private float minInterval = 0.15f;
        private float _lastToggleTime;

        [Header("Visuals (Sprite Swap)")]
        [SerializeField] private Sprite onSprite;
        [SerializeField] private Sprite offSprite;
        [SerializeField] private Image targetImage;
        [SerializeField] private bool useNativeSize = false;

        [Header("Mic (source of truth)")]
        [Tooltip("Assign your MicRecorder here.")]
        [SerializeField] private MicRecorder micRecorder;
        [SerializeField, Range(0.02f, 1f)] private float pollInterval = 0.1f;

        private bool _bound;
        private float _pollTimer;
        private bool _lastMicState;

        private void Awake()
        {
            if (_toggle == null)
            {
                _toggle = GetComponent<Toggle>();
                if (_toggle == null)
                {
                    Debug.LogError("[WristMuteButton] No Toggle assigned or found on this GameObject.");
                    return;
                }
            }

            var nav = _toggle.navigation; nav.mode = Navigation.Mode.None; _toggle.navigation = nav;

            _lastMicState = micRecorder ? micRecorder.IsListening : _toggle.isOn;
            _toggle.SetIsOnWithoutNotify(_lastMicState);
            ApplySprite(_lastMicState);
        }

        private void OnEnable()
        {
            if (_toggle != null && !_bound)
            {
                _toggle.onValueChanged.AddListener(OnToggleValueChanged);
                _bound = true;
            }
            _pollTimer = 0f;
        }

        private void OnDisable()
        {
            if (_toggle != null && _bound)
            {
                _toggle.onValueChanged.RemoveListener(OnToggleValueChanged);
                _bound = false;
            }
        }

        private void Update()
        {
            if (!micRecorder) return;

            _pollTimer -= Time.unscaledDeltaTime;
            if (_pollTimer > 0f) return;
            _pollTimer = pollInterval;

            bool micState = micRecorder.IsListening;
            if (micState != _lastMicState)
            {
                _lastMicState = micState;

                _toggle.SetIsOnWithoutNotify(micState);
                ApplySprite(micState);

                if (micState) onToggleOn?.Invoke();
                else onToggleOff?.Invoke();
            }
        }

        private void OnToggleValueChanged(bool requested)
        {
            float now = Time.unscaledTime;
            if (now - _lastToggleTime < minInterval)
            {
                _toggle.SetIsOnWithoutNotify(_lastMicState);
                ApplySprite(_lastMicState);
                ClearSelectionIfNeeded();
                return;
            }
            _lastToggleTime = now;

            if (!micRecorder)
            {
                Debug.LogWarning("[WristMuteButton] MicRecorder not assigned; reverting toggle.");
                _toggle.SetIsOnWithoutNotify(_lastMicState);
                ApplySprite(_lastMicState);
                ClearSelectionIfNeeded();
                return;
            }

            if (requested && !micRecorder.IsListening)      micRecorder.StartListening();
            else if (!requested && micRecorder.IsListening) micRecorder.StopListening();

            bool actual = micRecorder.IsListening;

            _toggle.SetIsOnWithoutNotify(actual);
            ApplySprite(actual);

            if (actual != _lastMicState)
            {
                _lastMicState = actual;
                if (actual) onToggleOn?.Invoke();
                else onToggleOff?.Invoke();
            }

            if (actual != requested)
            {
                Debug.LogWarning($"[WristMuteButton] Mic did not accept requested state ({requested}). Actual={actual}.");
            }

            ClearSelectionIfNeeded();
        }

        private void ClearSelectionIfNeeded()
        {
            if (EventSystem.current != null &&
                EventSystem.current.currentSelectedGameObject == _toggle.gameObject)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        public void SetOn(bool isOn)
        {
            OnToggleValueChanged(isOn);
        }

        public bool IsOn() => _toggle != null && _toggle.isOn;

        private void ApplySprite(bool isOn)
        {
            if (!targetImage) return;
            var s = isOn ? onSprite : offSprite;
            if (s != null)
            {
                targetImage.sprite = s;
                if (useNativeSize) targetImage.SetNativeSize();
            }
        }
    }
}
