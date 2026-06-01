using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Spawns a world-space "🆘 Help" button in front of the student at runtime so
/// no scene editing is required. The button toggles the hand-raise state on
/// <see cref="DeviceSynHandler"/>, which publishes the change over MQTT to the
/// teacher dashboard.
///
/// Drop this component on ANY GameObject (e.g. an empty bootstrapper in the
/// classroom default scene) and it will:
///   1. Find or wait for <see cref="DeviceSynHandler"/>.
///   2. Create a world-space Canvas parented to the main camera so the button
///      follows the student's view.
///   3. Build a simple square Button with the label "HELP" and wire it to
///      <see cref="HelpButton.OnHelpButtonClicked"/>.
///
/// Adjust position / scale via the inspector to taste.
/// </summary>
[DisallowMultipleComponent]
public class HelpButtonAutoMount : MonoBehaviour
{
    [Header("Placement (relative to main camera)")]
    [Tooltip("Local offset from the camera. Default puts the button slightly down-right of center, ~1.5m away.")]
    public Vector3 localPosition = new Vector3(0.35f, -0.25f, 1.2f);
    public Vector3 localEuler = Vector3.zero;
    public Vector2 sizeMeters = new Vector2(0.16f, 0.16f);

    [Header("Visuals")]
    public string label = "HELP";
    public Color buttonColor = new Color(0.94f, 0.27f, 0.27f); // red
    public Color loweredColor = new Color(0.36f, 0.45f, 0.56f); // slate gray
    public Color textColor = Color.white;

    [Tooltip("If true the button is hidden until a DeviceSynHandler is found in the scene.")]
    public bool requireDeviceSyncHandler = true;

    private GameObject _spawned;

    void Start()
    {
        if (_spawned != null) return;
        if (requireDeviceSyncHandler)
        {
            // Try a few times — DeviceSynHandler may be created slightly after
            // scene load by another bootstrapper.
            StartCoroutine(WaitAndSpawn());
        }
        else
        {
            Spawn();
        }
    }

    System.Collections.IEnumerator WaitAndSpawn()
    {
        const float timeout = 5f;
        float elapsed = 0f;
        while (elapsed < timeout && FindObjectOfType<DeviceSynHandler>() == null)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        Spawn();
    }

    void Spawn()
    {
        if (_spawned != null) return;
        var cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[HelpButtonAutoMount] No Camera.main — cannot place help button.");
            return;
        }

        // World-space Canvas parented to the camera so it follows gaze.
        var canvasGo = new GameObject("HelpButtonCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(cam.transform, false);
        canvasGo.transform.localPosition = localPosition;
        canvasGo.transform.localEulerAngles = localEuler;

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var rt = canvasGo.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(sizeMeters.x * 1000f, sizeMeters.y * 1000f);
        canvasGo.transform.localScale = Vector3.one * 0.001f; // 1 unit = 1 mm

        // Background button.
        var buttonGo = new GameObject("HelpButton", typeof(Image), typeof(Button), typeof(HelpButton));
        buttonGo.transform.SetParent(canvasGo.transform, false);
        var brt = buttonGo.GetComponent<RectTransform>();
        brt.anchorMin = Vector2.zero;
        brt.anchorMax = Vector2.one;
        brt.offsetMin = Vector2.zero;
        brt.offsetMax = Vector2.zero;

        var img = buttonGo.GetComponent<Image>();
        img.color = loweredColor;

        var helpButton = buttonGo.GetComponent<HelpButton>();

        var btn = buttonGo.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(helpButton.OnHelpButtonClicked);

        // Label.
        var textGo = new GameObject("Label", typeof(Text));
        textGo.transform.SetParent(buttonGo.transform, false);
        var trt = textGo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;

        var text = textGo.GetComponent<Text>();
        text.text = label;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = textColor;
        text.fontSize = 28;
        text.fontStyle = FontStyle.Bold;
        text.raycastTarget = false;
        // Use a guaranteed-available font.
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                    ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

        _spawned = canvasGo;
    }
}
