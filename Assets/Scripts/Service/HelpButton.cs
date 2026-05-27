using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lightweight wrapper that lets a Unity UI Button or any UnityEvent target
/// toggle the student's hand-raise (Help) state on <see cref="DeviceSynHandler"/>.
///
/// Wire up either:
///   • A scene-authored Unity UI Button — set its OnClick() to call
///     <see cref="OnHelpButtonClicked"/> on this component.
///   • Or rely on <see cref="HelpButtonAutoMount"/> which spawns a help
///     button at runtime so no scene editing is required.
/// </summary>
[DisallowMultipleComponent]
public class HelpButton : MonoBehaviour
{
    [Tooltip("Optional — visual that turns RED while hand is raised.")]
    public Image visualTarget;
    public Color raisedColor = new Color(0.94f, 0.27f, 0.27f); // red
    public Color loweredColor = new Color(0.85f, 0.85f, 0.85f); // light gray

    private DeviceSynHandler _handler;

    void OnEnable()
    {
        ResolveHandler();
        ApplyVisual();
    }

    void Update()
    {
        // Cheap polling so visual state stays in sync if the teacher clears
        // the hand remotely (MQTT clearHand). The DeviceSynHandler controls
        // the source of truth.
        ApplyVisual();
    }

    private void ResolveHandler()
    {
        if (_handler != null) return;
        _handler = FindObjectOfType<DeviceSynHandler>();
    }

    private void ApplyVisual()
    {
        if (visualTarget == null) return;
        if (_handler == null) { ResolveHandler(); if (_handler == null) return; }
        visualTarget.color = _handler.IsHandRaised ? raisedColor : loweredColor;
    }

    /// <summary>Toggle the hand-raise state — wire to a Button.onClick.</summary>
    public void OnHelpButtonClicked()
    {
        ResolveHandler();
        if (_handler == null)
        {
            Debug.LogWarning("[HelpButton] DeviceSynHandler not found in scene.");
            return;
        }
        _handler.ToggleHand();
        ApplyVisual();
    }
}
