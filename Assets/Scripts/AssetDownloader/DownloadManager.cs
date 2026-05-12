using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class DownloadManager : MonoBehaviour
{
    public GameObject[] objectsToEnable;
    public UnityEvent OnDownloadComplete;
    private ClassVrManager classVrManager;

    // Track only the RuntimeImportBehaviours that were registered for this
    // session, so the completion check is not confused by other inactive GOs.
    private readonly List<RuntimeImportBehaviour> _registered = new List<RuntimeImportBehaviour>();
    private int _completedCount;

    void Start()
    {
        classVrManager = GameManager.Instance.vrManager;
    }

    public void EnbleCallback()
    {
        _registered.Clear();
        _completedCount = 0;

        // Subscribe only to the currently active model downloaders.
        RuntimeImportBehaviour[] allManagers = FindObjectsOfType<RuntimeImportBehaviour>();
        foreach (RuntimeImportBehaviour manager in allManagers)
        {
            if (!manager.gameObject.activeSelf) continue;
            _registered.Add(manager);
            // Use a local capture so the lambda references the correct instance.
            RuntimeImportBehaviour captured = manager;
            manager.OnAssetDownloaded.AddListener(() => OnDownloadCompleted(captured));
        }

        if (_registered.Count == 0)
        {
            Debug.LogWarning("[DownloadManager] No active RuntimeImportBehaviour found — triggering LoadingBar immediately.");
            classVrManager.LoadingBarSimulator();
        }
    }

    private void OnDownloadCompleted(RuntimeImportBehaviour source)
    {
        if (!source.IsDownloadComplete()) return; // guard against spurious calls

        _completedCount++;
        Debug.Log($"[DownloadManager] {_completedCount}/{_registered.Count} models downloaded.");

        if (_completedCount < _registered.Count) return; // still waiting for others

        // All registered models done — start the loading bar simulation.
        classVrManager.LoadingBarSimulator();
        OnDownloadComplete?.Invoke();
    }
}
