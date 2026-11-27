using System;
using System.Collections;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
#if KLEVER_DPN
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.UI;
#endif
using Vuplex.WebView;
using static ChangeClassRoom;

public class WebManager : MonoBehaviour
{
    private const float LoadTimeout = 90f;
    private const string PageReadyCheck =
        "(function(){var ready=document.documentElement.getAttribute('data-klever-ready');" +
        "if(document.readyState!=='complete')return false;" +
        "if(ready!==null)return ready==='true';" +
        "if(location.href==='about:blank')return false;" +
        "if(window.AdobeAn&&window.createjs&&document.querySelector('canvas'))" +
        "return !!(window.stage&&window.stage.getNumChildren&&window.stage.getNumChildren()>0);" +
        "return true;})()";
#if KLEVER_DPN
    private const int MaxBoardBrowserDimension = 800;
    private const float BoardCaptureInterval = 0.2f;
    private const long MinimumJavaHeapHeadroom = 24L * 1024 * 1024;
    private const string ObjectXLauncherUrl = "https://klever-edu.com/objectx-launcher.html";
    private const string ElectricCircuitOriginal = "https://d3vdjegbtyvp.cloudfront.net/Electric_Circuit/index.html";
    private bool isDpvrElectricCircuit;
    private bool boardSimulationActive;
    private Coroutine browserFrameRoutine;
    private Texture2D browserFrame;
    private bool browserFrameShown;
    private Klever.DPVR.DpvrBoardWebPointer boardWebPointer;
    private BoxCollider nextCollider;
    private bool nextInteractableBound;
#endif

    public GameObject webView;
    public GameObject[] disableitems;
    public CanvasWebViewPrefab _webViewPrefab;
    public TextMeshProUGUI timer;
    public GameObject buttonNext;
    public UnityEvent startingevent;
    public UnityEvent endevent;
    public GameObject simulation;
    public GameObject temp;
    public bool isobjectXR;

    private string url;
    private string simulationScript;
    private float simulationTimer;
    private Coroutine simulationRoutine;
    private IWebView embeddedPage;
    private bool stageEntered;
    private bool stageEnded;
    private bool contentShown;
    private bool applicationPaused;

    public float ConvertTimeToSeconds(string displayTime)
    {
        var parts = displayTime?.Split(':');
        if (parts == null || parts.Length != 2 || !int.TryParse(parts[0], out int minutes) ||
            !int.TryParse(parts[1], out int seconds) || minutes < 0 || seconds < 0 || seconds >= 60)
            return 0f;
        return minutes * 60f + seconds;
    }

    public void AssignSimulationAsync(string time, string newUrl, string script)
    {
        simulationScript = script;
        url = newUrl?.Trim();
        simulationTimer = ConvertTimeToSeconds(time);
#if KLEVER_DPN
        isDpvrElectricCircuit = !isobjectXR && Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
            uri.GetLeftPart(UriPartial.Path) == ElectricCircuitOriginal;
#endif
    }

#if KLEVER_DPN
    private void Awake()
    {
        PrepareDpvrBoardUi();
    }
#endif

    public void StartSimulation()
    {
        if (stageEntered || stageEnded) return;
        stageEntered = true;
        if (string.IsNullOrEmpty(url))
        {
            CheckTheStatus();
            return;
        }

        var room = ChangeClassRoom.Instance;
        if (room != null) ChangeSkyBox.SetSkybox(ChangeSkyBox.skyBoxMaterialNew[room.classRoom]);
        var hands = GamificationControllerHandSwitcher.Instance;
        if (hands != null)
        {
            hands.leftHandRaycast?.SetActive(true);
            hands.rightHandRaycast?.SetActive(true);
            hands.leftControllerRaycast?.SetActive(true);
            hands.rightControllerRaycast?.SetActive(true);
        }
#if KLEVER_DPN
        if (isobjectXR && simulation != null && temp != null)
#else
        if (simulation != null && temp != null)
#endif
            simulation.transform.SetPositionAndRotation(temp.transform.position, temp.transform.rotation);

        startingevent.Invoke();
        ToggleItem(false);
        GameManager.Instance.googleTextToSpeech?.StopAudio();
        buttonNext.SetActive(false);
        if (isobjectXR)
        {
            BeginContent();
            OpenExternalSimulation();
            return;
        }

        timer.text = "Loading simulation…";
#if KLEVER_DPN && UNITY_ANDROID && !UNITY_EDITOR
        AndroidWebView.SetForceDrawEnabled(true);
        if (_webViewPrefab.transform is RectTransform viewport)
        {
            var size = viewport.rect.size;
            var longestSide = Mathf.Max(size.x, size.y);
            if (longestSide > 0f)
                _webViewPrefab.Resolution = Mathf.Min(_webViewPrefab.Resolution, MaxBoardBrowserDimension / longestSide);
        }
#endif
        webView.SetActive(true);
#if KLEVER_DPN
        boardSimulationActive = true;
        browserFrameShown = false;
        PrepareDpvrBoardUi();
        FitSimulationToBoard(room);
#endif
        _webViewPrefab.gameObject.SetActive(true);
        _webViewPrefab.Visible = true;
        simulationRoutine = StartCoroutine(RunEmbeddedSimulation());
    }

    private IEnumerator RunEmbeddedSimulation()
    {
        // Initialization and navigation have separate limits. Neither consumes
        // the authored activity duration.
        float deadline = Time.realtimeSinceStartup + LoadTimeout;
        var init = _webViewPrefab.WaitUntilInitialized();
        while (!init.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
        if (init.Status != TaskStatus.RanToCompletion || _webViewPrefab.WebView == null)
        {
            yield return FinishUnavailableSimulation();
            yield break;
        }

#if KLEVER_DPN
        EnsureDpvrBoardInteraction();
#endif
        embeddedPage = _webViewPrefab.WebView;
        embeddedPage.UrlChanged += OnSimulationUrlChanged;
        bool ready = false;
        yield return WaitForPlayablePage(value => ready = value);
        if (!ready)
        {
            yield return FinishUnavailableSimulation();
            yield break;
        }
#if KLEVER_DPN && UNITY_ANDROID && !UNITY_EDITOR
        if (embeddedPage is IWithFallbackTextureData)
        {
            // Render live Android WebView frames onto an unmasked board overlay so
            // 2D simulations display reliably on DPVR without external OES issues.
            browserFrameRoutine = StartCoroutine(RenderOriginalBoardPage());
            deadline = Time.realtimeSinceStartup + (isDpvrElectricCircuit ? 15f : 5f);
            while (!browserFrameShown && Time.realtimeSinceStartup < deadline) yield return null;
            if (!browserFrameShown)
                Debug.Log("[DPVR-SIMULATION] Fallback frame pending after page load; continuing with board view.");
        }
#endif
#if KLEVER_DPN
        if (boardWebPointer != null) boardWebPointer.SetPageReady(true);
#endif
        yield return BeginReadySimulation();
    }

    private IEnumerator BeginReadySimulation()
    {
        // Unity can initialize the web view while the application is paused.
        // Start tracking only once the learner can see and use the board.
        yield return new WaitUntil(() => !applicationPaused && !LessonPause.IsPaused);
        BeginContent();
        Debug.Log("[DPVR-SIMULATION] Board ready; authored duration started.");
        if (simulationTimer <= 0f)
        {
            timer.text = string.Empty;
            buttonNext.SetActive(true);
            yield break;
        }
        yield return WebViewDuration(simulationTimer);
    }

    private IEnumerator WaitForPlayablePage(Action<bool> complete)
    {
        var deadline = Time.realtimeSinceStartup +
#if KLEVER_DPN
            (isDpvrElectricCircuit ? 180f : 120f);
#else
            LoadTimeout;
#endif
        Task pageLoad;
        try
        {
            pageLoad = embeddedPage.WaitForNextPageLoadToFinish();
            embeddedPage.LoadUrl(url);
        }
        catch (Exception error)
        {
            Debug.LogWarning("[Simulation] Navigation failed: " + error.GetType().Name);
            complete(false);
            yield break;
        }

        while (!pageLoad.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
        if (pageLoad.Status == TaskStatus.RanToCompletion)
        {
            while (Time.realtimeSinceStartup < deadline)
            {
                var check = embeddedPage.ExecuteJavaScript(PageReadyCheck);
                while (!check.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
                if (check.Status == TaskStatus.RanToCompletion &&
                    string.Equals(check.Result?.Trim('"'), "true", StringComparison.OrdinalIgnoreCase))
                {
                    complete(true);
                    yield break;
                }
                yield return new WaitForSecondsRealtime(0.5f);
            }
        }
        else if (pageLoad.IsFaulted)
        {
            Debug.LogWarning("[Simulation] Page load failed: " + pageLoad.Exception.GetBaseException().GetType().Name);
        }
        complete(false);
    }

    private void BeginContent()
    {
        var details = new SimpleJSON.JSONObject();
        details["isObjectXR"] = isobjectXR;
        details["durationSeconds"] = simulationTimer;
        Klass.Net.LiveViewPublisher.Instance?.ShowContent("simulation", url, url, details);
        contentShown = true;
        if (!string.IsNullOrEmpty(simulationScript))
            GameManager.Instance.googleTextToSpeech?.StartTalking(simulationScript);
    }

    private IEnumerator FinishUnavailableSimulation()
    {
        timer.text = "Simulation unavailable. Continuing shortly…";
        GameManager.Instance.deviceSynHandler?.PublishDownloadError("Simulation did not finish loading.");
        yield return new WaitForSeconds(4f);
        FinishStage(false);
    }

    private IEnumerator WebViewDuration(float duration)
    {
        float elapsed = 0f;
        int displayed = -1;
        while (elapsed < duration)
        {
            int seconds = Mathf.CeilToInt(duration - elapsed);
            if (seconds != displayed)
            {
                displayed = seconds;
                timer.text = $"{seconds / 60}:{seconds % 60:00}";
            }
            if (elapsed >= 60f && !buttonNext.activeSelf) buttonNext.SetActive(true);
            yield return null;
            if (!applicationPaused && !LessonPause.IsPaused) elapsed += Time.deltaTime;
        }
        CheckTheStatus();
    }

    private void OpenExternalSimulation()
    {
        PlayerPrefs.SetInt("InSimulation", 1);
        PlayerPrefs.Save();
        webView.SetActive(false);
        timer.text = "Return to Klever when you finish";
        string launchUrl = url;
#if KLEVER_DPN
        if (!url.Contains("vredumelzo")) launchUrl = ObjectXLauncherUrl + "?src=" + Uri.EscapeDataString(url);
#endif
        Application.OpenURL(launchUrl);
#if !KLEVER_DPN
        StartCoroutine(KleverAppQuit());
#endif
    }

    private void OnSimulationUrlChanged(object sender, UrlChangedEventArgs args)
    {
        if (DeepLinkManager.IsSimulationReturnUrl(args.Url)) CheckTheStatus();
        else if (Uri.TryCreate(args.Url, UriKind.Absolute, out var destination) &&
                 Uri.TryCreate(url, UriKind.Absolute, out var origin) &&
                 string.Equals(destination.Host, origin.Host, StringComparison.OrdinalIgnoreCase) &&
                 destination.AbsolutePath == "/close-simulation")
            CheckTheStatus();
    }

    public void ToggleItem(bool visible)
    {
        foreach (var item in disableitems)
            if (item != null) item.SetActive(visible);
    }

    public void StopWebViewAudio()
    {
        if (embeddedPage != null && embeddedPage.IsInitialized)
            embeddedPage.ExecuteJavaScript(
                "Array.prototype.forEach.call(document.querySelectorAll('audio,video'),function(media){media.pause();});", null);
    }

    public void CheckTheStatus() => FinishStage(true);

    private void FinishStage(bool completed)
    {
        if (stageEnded) return;
        stageEnded = true;
#if KLEVER_DPN
        boardSimulationActive = false;
#endif
        if (simulationRoutine != null)
        {
            StopCoroutine(simulationRoutine);
            simulationRoutine = null;
        }
        if (contentShown)
            Klass.Net.LiveViewPublisher.Instance?.HideContent(completed ? "simulation-completed" : "simulation-unavailable");
        StopWebViewAudio();
        Unsubscribe();
#if KLEVER_DPN
        if (boardWebPointer != null) boardWebPointer.SetPageReady(false);
        StopBrowserFrames();
#endif
        if (webView != null) Destroy(webView);
        endevent.Invoke();
        GameManager.Instance.serviceScript.AssignTaskBasedOnCondition(GameStatus.Mcq);
        GameManager.Instance.gamificationManager.AssignGames();
    }

    private void OnApplicationPause(bool paused) => applicationPaused = paused;

    private void OnDestroy()
    {
        Unsubscribe();
#if KLEVER_DPN
        if (boardWebPointer != null) boardWebPointer.SetPageReady(false);
        StopBrowserFrames();
#endif
    }

    private void Unsubscribe()
    {
        if (embeddedPage == null) return;
        embeddedPage.UrlChanged -= OnSimulationUrlChanged;
        embeddedPage = null;
    }

#if KLEVER_DPN
    private IEnumerator RenderOriginalBoardPage()
    {
        if (!(embeddedPage is IWithFallbackTextureData capture)) yield break;
        var overlay = new GameObject("Android WebView frame", typeof(RectTransform), typeof(RawImage));
        overlay.layer = _webViewPrefab.gameObject.layer;
        var rect = (RectTransform)overlay.transform;
        rect.SetParent(_webViewPrefab.transform, false);
        rect.SetAsLastSibling();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var image = overlay.GetComponent<RawImage>();
        image.raycastTarget = false;
        image.maskable = false;
        image.material = null;
        image.enabled = false;
        using var javaRuntime = new AndroidJavaClass("java.lang.Runtime").CallStatic<AndroidJavaObject>("getRuntime");
        bool loggedLowMemory = false;
        while (embeddedPage != null)
        {
            if (!applicationPaused && !LessonPause.IsPaused)
            {
                var size = embeddedPage.Size;
                long headroom = javaRuntime.Call<long>("maxMemory") - javaRuntime.Call<long>("totalMemory") +
                    javaRuntime.Call<long>("freeMemory");
                if (headroom < MinimumJavaHeapHeadroom)
                {
                    if (!loggedLowMemory)
                        Debug.LogWarning("[DPVR-SIMULATION] Frame capture waiting for Android heap headroom.");
                    loggedLowMemory = true;
                    yield return new WaitForSecondsRealtime(1f);
                    continue;
                }
                loggedLowMemory = false;
                if (size.x > 0 && size.y > 0 && Mathf.Max(size.x, size.y) <= MaxBoardBrowserDimension)
                {
                    var frame = capture.GetFallbackTextureData();
                    while (!frame.IsCompleted) yield return null;
                    if (frame.Status == TaskStatus.RanToCompletion && frame.Result != null &&
                        frame.Result.Length == size.x * size.y * 4 && (browserFrameShown || HasPagePixels(frame.Result)))
                    {
                        if (browserFrame == null || browserFrame.width != size.x || browserFrame.height != size.y)
                        {
                            if (browserFrame != null) Destroy(browserFrame);
                            browserFrame = new Texture2D(size.x, size.y, TextureFormat.RGBA32, false);
                            image.texture = browserFrame;
                        }
                        browserFrame.LoadRawTextureData(frame.Result);
                        browserFrame.Apply(false);
                        image.enabled = true;
                        if (!browserFrameShown)
                            Debug.Log("[DPVR-SIMULATION] Original webpage pixels rendered on the board.");
                        browserFrameShown = true;
                    }
                }
            }
            yield return new WaitForSecondsRealtime(BoardCaptureInterval);
        }
    }

    private static bool HasPagePixels(byte[] pixels)
    {
        for (int i = 1004; i + 3 < pixels.Length; i += 1004)
            if (pixels[i + 3] > 0 && (pixels[i] != pixels[0] || pixels[i + 1] != pixels[1] ||
                pixels[i + 2] != pixels[2])) return true;
        return false;
    }

    private void StopBrowserFrames()
    {
        if (browserFrameRoutine != null) StopCoroutine(browserFrameRoutine);
        browserFrameRoutine = null;
        if (browserFrame != null) Destroy(browserFrame);
        browserFrame = null;
    }

    private void LateUpdate()
    {
        if (boardSimulationActive && webView != null && webView.activeInHierarchy)
            PositionSimulationOnBoard(ChangeClassRoom.Instance);
    }

    public void PrepareDpvrBoardUi()
    {
        var viewerCamera = Camera.main;
        if (_webViewPrefab != null)
        {
            DisableMaskChain(_webViewPrefab.transform);
            ConfigureBoardCanvas(_webViewPrefab.GetComponentInParent<Canvas>(true), 1, viewerCamera);
            foreach (var raw in _webViewPrefab.GetComponentsInChildren<RawImage>(true))
                raw.maskable = false;
            var view = _webViewPrefab.transform.Find("CanvasWebViewPrefabView")?.GetComponent<RawImage>();
            if (view != null) view.raycastTarget = true;
        }
        if (timer != null)
        {
            var clockParent = timer.transform.parent != null ? timer.transform.parent : timer.transform;
            DisableMaskChain(clockParent);
            ConfigureBoardCanvas(clockParent.GetComponentInParent<Canvas>(true), 2, viewerCamera);
            foreach (var bg in clockParent.GetComponentsInChildren<Image>(true))
            {
                bg.maskable = false;
                bg.raycastTarget = false;
            }
            foreach (var label in clockParent.GetComponentsInChildren<TMP_Text>(true))
            {
                label.maskable = false;
                label.raycastTarget = false;
                label.enableAutoSizing = true;
                label.fontSizeMin = 6f;
                label.fontSizeMax = Mathf.Max(20f, label.fontSize);
            }
        }
        if (buttonNext != null)
        {
            DisableMaskChain(buttonNext.transform);
            var nextCanvas = buttonNext.GetComponent<Canvas>() ?? buttonNext.GetComponentInParent<Canvas>(true);
            if (nextCanvas == null) nextCanvas = buttonNext.AddComponent<Canvas>();
            ConfigureBoardCanvas(nextCanvas, 3, viewerCamera);
            var btn = buttonNext.GetComponent<Button>();
            if (btn != null) { btn.enabled = true; btn.interactable = true; }
            var img = buttonNext.GetComponent<Image>();
            if (img != null)
            {
                img.enabled = true;
                img.raycastTarget = true;
                img.maskable = false;
                if (img.color.a < 0.9f) img.color = Color.white;
            }
            var nextLabel = buttonNext.transform.Find("Text (TMP)");
            if (nextLabel != null)
            {
                nextLabel.gameObject.SetActive(true);
                var tmp = nextLabel.GetComponent<TMP_Text>();
                if (tmp != null)
                {
                    tmp.maskable = false;
                    tmp.enableAutoSizing = true;
                    tmp.fontSizeMin = 8f;
                    tmp.fontSizeMax = Mathf.Max(20f, tmp.fontSize);
                }
            }
            var warning = buttonNext.transform.Find("Warning Text");
            if (warning != null) warning.gameObject.SetActive(false);
            var caution = buttonNext.transform.Find("Caution Backplate");
            if (caution != null) caution.gameObject.SetActive(false);
        }
        EnsureDpvrBoardInteraction();
    }

    private void EnsureDpvrBoardInteraction()
    {
        if (!Application.isPlaying) return;
        if (_webViewPrefab != null)
        {
            _webViewPrefab.ClickingEnabled = true;
            _webViewPrefab.HoveringEnabled = true;
            _webViewPrefab.DragMode = DragMode.DragWithinPage;
            foreach (var detector in _webViewPrefab.GetComponentsInChildren<CanvasPointerInputDetector>(true))
                detector.enabled = false;
            if (boardWebPointer == null)
                boardWebPointer = _webViewPrefab.GetComponent<Klever.DPVR.DpvrBoardWebPointer>()
                    ?? _webViewPrefab.gameObject.AddComponent<Klever.DPVR.DpvrBoardWebPointer>();
            boardWebPointer.enabled = true;
            boardWebPointer.Configure(_webViewPrefab, Mathf.Max(20f, _webViewPrefab.DragThreshold));
            _webViewPrefab.SetPointerInputDetector(boardWebPointer);
        }
        if (buttonNext != null)
        {
            var nextRect = buttonNext.transform as RectTransform;
            if (nextRect != null)
            {
                SyncNextButtonCollider(nextRect);
                var interactable = buttonNext.GetComponent<XRSimpleInteractable>()
                    ?? buttonNext.AddComponent<XRSimpleInteractable>();
                var manager = FindObjectOfType<XRInteractionManager>();
                if (manager != null) interactable.interactionManager = manager;
                if (nextCollider != null && !interactable.colliders.Contains(nextCollider))
                    interactable.colliders.Add(nextCollider);
                if (!nextInteractableBound)
                {
                    interactable.selectEntered.AddListener(_ => CheckTheStatus());
                    nextInteractableBound = true;
                }
            }
        }
    }

    private void SyncNextButtonCollider(RectTransform nextRect)
    {
        if (!Application.isPlaying || nextRect == null) return;
        if (nextCollider == null)
            nextCollider = nextRect.GetComponent<BoxCollider>() ?? nextRect.gameObject.AddComponent<BoxCollider>();
        nextCollider.isTrigger = true;
        nextCollider.enabled = true;
        var rect = nextRect.rect;
        nextCollider.center = new Vector3(rect.center.x, rect.center.y, -2f);
        nextCollider.size = new Vector3(Mathf.Max(1f, rect.width), Mathf.Max(1f, rect.height), 6f);
    }

    private static void ConfigureBoardCanvas(Canvas canvas, int sortingOrder, Camera viewerCamera)
    {
        if (canvas == null) return;
        canvas.renderMode = RenderMode.WorldSpace;
        if (viewerCamera != null) canvas.worldCamera = viewerCamera;
        canvas.sortingOrder = Mathf.Max(canvas.sortingOrder, sortingOrder);
        var raycaster = canvas.GetComponent<TrackedDeviceGraphicRaycaster>();
        if (raycaster == null) raycaster = canvas.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
        raycaster.enabled = true;
        var standardRaycaster = canvas.GetComponent<GraphicRaycaster>();
        if (standardRaycaster != null) standardRaycaster.enabled = false;
    }

    private static void DisableMaskChain(Transform child)
    {
        for (var cursor = child; cursor != null; cursor = cursor.parent)
        {
            if (cursor == child) continue;
            var mask = cursor.GetComponent<Mask>();
            if (mask != null)
            {
                mask.enabled = false;
                var graphic = cursor.GetComponent<Graphic>();
                if (graphic != null)
                {
                    graphic.enabled = false;
                    graphic.raycastTarget = false;
                }
            }
            var rectMask = cursor.GetComponent<RectMask2D>();
            if (rectMask != null) rectMask.enabled = false;
            if (cursor.name == "CurvedCanvas")
            {
                foreach (var col in cursor.GetComponents<Collider>()) col.enabled = false;
                var interactable = cursor.GetComponent<XRBaseInteractable>();
                if (interactable != null) interactable.enabled = false;
            }
        }
    }

    public void FitSimulationToBoard(ChangeClassRoom room)
    {
        if (room == null || _webViewPrefab == null) return;
        PrepareDpvrBoardUi();
        PositionSimulationOnBoard(room);
    }

    private void PositionSimulationOnBoard(ChangeClassRoom room)
    {
        if (room == null || _webViewPrefab == null) return;
        var surface = Klever.VR.ClassroomBoardLayout.Surface(room);
        if (surface == null) return;
        var camera = Camera.main;
        var viewer = camera != null ? camera.transform.position : new Vector3(0, 1.6f, 0);
        var nextRect = buttonNext != null ? buttonNext.transform as RectTransform : null;
        Klever.VR.ClassroomBoardLayout.FitSimulation((RectTransform)_webViewPrefab.transform, surface, viewer,
            nextRect, timer != null ? timer.transform.parent as RectTransform : null);
        if (Application.isPlaying)
        {
            if (boardWebPointer != null) boardWebPointer.SyncCollider();
            if (nextRect != null) SyncNextButtonCollider(nextRect);
        }
    }
#else
    public IEnumerator KleverAppQuit()
    {
        yield return new WaitForSeconds(1f);
        Application.Quit();
    }
#endif
}
