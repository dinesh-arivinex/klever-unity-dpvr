using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class LoadingBar : MonoBehaviour
{
    public GameObject loadingScreen;
   // public Slider slider;
    public Image sliderNew;
    public TextMeshProUGUI progressText;
    private RuntimeImportBehaviour importBehaviour;
    public GameObject[] toggleItem;
    private GameObject parentObject;
    public UnityEvent StartGame;
    private int totalCount;
    private bool IsmodelDownload;
    public DeviceSynHandler deviceSynHandler;
    private bool isLoaded;
    public GameObject blackSphere;
    private API api;
  
    private void Start()
    {
        api=FindObjectOfType<API>();
      //  deviceSynHandler=FindObjectOfType<DeviceSynHandler>();
        parentObject = transform.parent != null ? transform.parent.gameObject : null;
        //importBehaviour =FindAnyObjectByType<RuntimeImportBehaviour>();
        // SimulateLoading();
        ToggleObject(false);
    }
  
    public void CustomSumulatedLoading()
    {
        StartCoroutine(GenerateRandomProgress());
        totalCount = 20;
    }
    public void SimulateLoading(RuntimeImportBehaviour finalScript)
    {
        importBehaviour=finalScript;
        IsmodelDownload =true;
        totalCount = importBehaviour.totalStep;
        StartCoroutine(GenerateRandomProgress());
    }

    IEnumerator GenerateRandomProgress()
    {
        if (loadingScreen != null) loadingScreen.SetActive(true);

        int totalProgressSteps = totalCount > 0 ? totalCount : 100;
        // Cap the max loading time to around 3-5 seconds to avoid infinite loading screens
        float waitTime = Mathf.Clamp(5.0f / totalProgressSteps, 0.01f, 0.1f);

        for (int i = 0; i <= totalProgressSteps; i++)
        {
            float progress = Mathf.Clamp01((float)i / totalProgressSteps);
            if (sliderNew != null) sliderNew.fillAmount = progress;
            if (progressText != null) progressText.text = (progress * 100f).ToString("F0") + "%";
            // Broadcast live download progress to the teacher dashboard via MQTT.
            if (deviceSynHandler != null) deviceSynHandler.PublishDownloadProgress(progress);
            yield return new WaitForSeconds(waitTime); // Consistent, safe delay
        }

        // Complete the progress bar at 100%.
        if (sliderNew != null) sliderNew.fillAmount = 1f;
        if (progressText != null) progressText.text = "100%";
        if (deviceSynHandler != null) deviceSynHandler.PublishDownloadProgress(1f);
        isLoaded = true;

        // 1) Mark this device as synced (publishes MQTT state → web console
        //    shows "playing"; also enables Welcome flow checks elsewhere).
        DeviceSync();

        // 2) Hide the loading UI — the user requirement: "Loading bar completes
        //    and needs to be disabled".
        if (loadingScreen != null) loadingScreen.SetActive(false);
        if (blackSphere != null) blackSphere.SetActive(false);
        ToggleObject(true);
        // 3) Fire the StartGame UnityEvent. In the KlassScene prefab this is
        //    wired to RuntimeImportBehaviourHelper.AssetDownloadCallback which
        //    runs FindingModel() + StartSpeaking() (Google TTS) and continues
        //    the experience flow (360 videos, images, etc.).
        //    Guarded by IsmodelDownload so the no-model path (CustomSumulatedLoading)
        //    does not trigger model-binding logic.
        if (IsmodelDownload && StartGame != null)
        {
            try { StartGame.Invoke(); }
            catch (System.Exception e)
            {
                Debug.LogError("[LoadingBar] StartGame.Invoke failed: " + e.Message);
                if (deviceSynHandler != null)
                    deviceSynHandler.PublishDownloadError("Post-load start failed: " + e.Message);
            }
        }
    }
    private void Update()
    {
       
        
    }

    public void DeviceSync()
    {
        if (deviceSynHandler == null)
        {
            deviceSynHandler = FindObjectOfType<DeviceSynHandler>();
            if (deviceSynHandler == null)
            {
                Debug.LogError("[LoadingBar] DeviceSynHandler reference missing — cannot mark device synced.");
                return;
            }
        }
        deviceSynHandler.SynDevice();
    }
    public void SynchDevice()
    {
        //if (blackSphere != null) blackSphere.SetActive(false);
        //ToggleObject(true);
        if (IsmodelDownload && StartGame != null)
            StartGame.Invoke();
    }
    public void ToggleObject(bool isTrue)
    {
        if (isTrue)
        {
            foreach (var item in toggleItem)
            {
                if (item != null) item.SetActive(true);
            }
            if (parentObject != null) parentObject.SetActive(false);

        }
        else
        { 
            foreach (var item in toggleItem)

            {
                if (item != null) item.SetActive(false);
            }
           
        }
            
    }
}
