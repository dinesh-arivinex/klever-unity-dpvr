using System.Collections;
using System.Collections.Generic;
using TriLibCore.Dae.Schema;
using UnityEngine;


public class ClassVrManager : MonoBehaviour
{
    private VideoManager videoManager;
    private GoogleTextToSpeech googleTextToSpeech;
    private List<GameObject> selectedModels = new List<GameObject>();
    public GameObject[] modelsDownloader;
    private DownloadManager downloadManager;
    public LoadingBar loadingBar;
    private RuntimeImportBehaviour runtimeImportBehaviour;
    private GamificationManager gamificationManager;
    void Start()
    {
        if (GameManager.Instance != null)
        {
            gamificationManager = GameManager.Instance.gamificationManager;
            googleTextToSpeech = GameManager.Instance.googleTextToSpeech;
        }
        else
        {
            Debug.LogError("[ClassVrManager] GameManager.Instance is null in Start() — script order issue.");
        }

        downloadManager = FindObjectOfType<DownloadManager>();
        videoManager = FindAnyObjectByType<VideoManager>();
    }
    public void AssignValues(List<string> typeOfGame)
    {
        gamificationManager.AssignGames(typeOfGame);
    }
    public void PreferredLanguage(string language)
    {
        if(language == "arabic")
        {
            googleTextToSpeech.LanguageSelector(Language.Arabic);
        }
        else if(language == "spanish")
        {
            googleTextToSpeech.LanguageSelector(Language.Spanish);
        }
        else
        {
            googleTextToSpeech.LanguageSelector(Language.English);
        }


            
    }
   
    public void UpdateVideoType(string type)
    {
      //  videoManager.ChangeVideoType(type);
    }
    public void AddDownloadManager(int modelDetailsCount)
    {
        selectedModels.Clear();

        // Activate each model downloader object and subscribe to the global
        // OnDownloadCompleted callback. We keep the LAST one in the list as the
        // reference passed to LoadingBarSimulator — DownloadManager already
        // checks that ALL downloads are done before calling LoadingBarSimulator,
        // so any single valid reference works for the totalStep count.
        runtimeImportBehaviour = null;
        for (int i = 0; i < modelDetailsCount; i++)
        {
            if (i >= modelsDownloader.Length)
            {
                Debug.LogError("[ClassVrManager] modelsDownloader array too short for modelDetailsCount=" + modelDetailsCount);
                break;
            }
            if (modelsDownloader[i] == null) continue; // skip empty slots
            selectedModels.Add(modelsDownloader[i]);
            modelsDownloader[i].SetActive(true);
            var rib = modelsDownloader[i].GetComponent<RuntimeImportBehaviour>();
            if (rib != null) runtimeImportBehaviour = rib;
        }

        // Re-resolve downloadManager if Start() hadn't run yet or it was lost
        // on a reload — avoids NRE on cold boot / quick Start press.
        if (downloadManager == null) downloadManager = FindObjectOfType<DownloadManager>();
        if (downloadManager == null)
        {
            Debug.LogError("[ClassVrManager] DownloadManager missing in scene — invoking LoadingBar fallback directly.");
            LoadingBarSimulator();
            return;
        }
        downloadManager.EnbleCallback();
    }
    public void LoadingBarSimulator()
    {
        if (loadingBar == null)
        {
            loadingBar = FindObjectOfType<LoadingBar>();
            if (loadingBar == null)
            {
                Debug.LogError("[ClassVrManager] LoadingBar reference missing — cannot start loading UI.");
                return;
            }
        }
        // runtimeImportBehaviour may be null when the experience has no models;
        // LoadingBar.SimulateLoading handles that null case safely.
        loadingBar.SimulateLoading(runtimeImportBehaviour);
    }
    void Update()
    {
        
    }
}
