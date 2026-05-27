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
        if (GameManager.Instance != null)
            classVrManager = GameManager.Instance.vrManager;
        if (classVrManager == null) classVrManager = FindObjectOfType<ClassVrManager>();
    }

    public void EnbleCallback()
    {
        // Re-resolve classVrManager in case Start() hadn't run yet, or the
        // GameManager wasn't ready on first frame after scene reload.
        if (classVrManager == null && GameManager.Instance != null)
            classVrManager = GameManager.Instance.vrManager;
        if (classVrManager == null) classVrManager = FindObjectOfType<ClassVrManager>();
        if (classVrManager == null)
        {
            Debug.LogError("[DownloadManager] ClassVrManager not found — cannot start loading bar.");
            return;
        }

        _registered.Clear();
        _completedCount = 0;

        // Subscribe only to the currently active model downloaders.
        RuntimeImportBehaviour[] allManagers = FindObjectsOfType<RuntimeImportBehaviour>();
        foreach (RuntimeImportBehaviour manager in allManagers)
        {
            if (manager == null || !manager.gameObject.activeSelf) continue;
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
        if (source == null || !source.IsDownloadComplete()) return; // guard against spurious calls

        _completedCount++;
        Debug.Log($"[DownloadManager] {_completedCount}/{_registered.Count} models downloaded.");

        if (_completedCount < _registered.Count) return; // still waiting for others

        // All registered models done — start the loading bar simulation.
        if (classVrManager != null) classVrManager.LoadingBarSimulator();
        OnDownloadComplete?.Invoke();
    }
}
