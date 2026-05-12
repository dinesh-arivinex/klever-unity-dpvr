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
        gamificationManager = GameManager.Instance.gamificationManager;
       
        downloadManager = FindObjectOfType<DownloadManager>();
        videoManager = FindAnyObjectByType<VideoManager>();
        googleTextToSpeech = GameManager.Instance.googleTextToSpeech;
       
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
            selectedModels.Add(modelsDownloader[i]);
            modelsDownloader[i].SetActive(true);
            runtimeImportBehaviour = modelsDownloader[i].GetComponent<RuntimeImportBehaviour>();
        }
        downloadManager.EnbleCallback();
    }
    public void LoadingBarSimulator()
    {
        loadingBar.SimulateLoading(runtimeImportBehaviour);
    }
    void Update()
    {
        
    }
}
