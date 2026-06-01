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
    private DeviceSynHandler _handler;

    void OnEnable()
    {
        ResolveHandler();
        //OnHelpButtonClicked();
    }

    private void ResolveHandler()
    {
        if (_handler != null) return;
        _handler = FindObjectOfType<DeviceSynHandler>();
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
    }
}
