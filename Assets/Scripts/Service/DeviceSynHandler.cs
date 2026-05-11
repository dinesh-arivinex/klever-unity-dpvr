using SimpleJSON;
using System;
using System.Collections;
using Klass.Net;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SpaceData
{
    public string experienceId;
    public string schoolId;
    public string sectionId;
    public string gradeId;
}

/// <summary>
/// MQTT-driven replacement for the previous 2s polling loop.
/// Public surface (fields, methods, statics) is preserved so that
/// LoadingBar / ServiceScript / GameManager / API continue to compile.
/// </summary>
public class DeviceSynHandler : MonoBehaviour
{
    [System.Serializable]
    public class Data
    {
        public string schoolId;
        public bool isActive;
        public bool isSynced;
        public bool isCompleted;
    }

    [System.Serializable]
    public class Response
    {
        public Data data;
        public string message;
    }

    public class DeviceStatus
    {
        public bool isActive;
        public bool isSynced;
        public bool isCompleted;
    }

    public class SynchDevice
    {
        public string deviceId;
        public bool isCompleted;
    }

    // Preserved public surface
    private bool isSync;
    public bool isActiveDevice;
    public bool isSyncedDevice;
    public bool isCompletedDevice;
    public static bool isOpenWelcomeScreen;
    public GameObject WelcomeScene;
    private API apiManager;
    private bool isStartGame;
    public bool isOpenGame;
    public LoadingBar loadingBar;
    public static bool isGameOver;
    public bool isOpening;
    private bool isStartButtonPressed;
    public GamificationManager gamificationManager;
    private bool isStopCalling;
    private bool isFirstTime;
    private bool isLobby = true;

    private bool _mqttBooted;
    private string _lastPublishedFingerprint;
    private const float HeartbeatSeconds = 30f;
    private float _heartbeatTimer;
    private int _downloadProgress; // 0..100, set by LoadingBar via PublishDownloadProgress

    void Start()
    {
        isFirstTime = true;
        apiManager = GameManager.Instance.api;
        isActiveDevice = false;
        isOpenWelcomeScreen = true;
        isGameOver = false;
        BootMqtt();
    }

    void OnDestroy()
    {
        if (KlassMqttClient.Instance != null)
        {
            KlassMqttClient.Instance.OnConnected      -= HandleMqttConnected;
            KlassMqttClient.Instance.OnDisconnected   -= HandleMqttDisconnected;
            KlassMqttClient.Instance.OnSessionControl -= HandleSessionControl;
            KlassMqttClient.Instance.OnCommand        -= HandleCommand;
        }
    }

    void Update()
    {
        _heartbeatTimer += Time.deltaTime;
        if (_heartbeatTimer >= HeartbeatSeconds)
        {
            _heartbeatTimer = 0f;
            PublishStateIfChanged(force: true);
        }
    }

    private void BootMqtt()
    {
        if (_mqttBooted) return;
        _mqttBooted = true;
        if (KlassMqttClient.Instance == null)
        {
            var go = new GameObject("[KlassMqttClient]");
            go.AddComponent<KlassMqttClient>();
        }
        var c = KlassMqttClient.Instance;
        c.OnConnected      += HandleMqttConnected;
        c.OnDisconnected   += HandleMqttDisconnected;
        c.OnSessionControl += HandleSessionControl;
        c.OnCommand        += HandleCommand;
        c.Begin();
    }

    private void HandleMqttConnected()
    {
        isActiveDevice = true;
        PublishStateIfChanged(force: true);
    }

    private void HandleMqttDisconnected()
    {
        isActiveDevice = false;
    }

    private void HandleSessionControl(JSONNode payload)
    {
        if (payload == null || payload.Count == 0)
        {
            if (!isLobby) LoadScene();
            return;
        }
        bool isStart = payload["isStart"].AsBool;
        bool isStop  = payload["isStop"].AsBool;

        if (isFirstTime && !string.IsNullOrEmpty(payload["gradeId"]))
        {
            string experianceID = payload["experienceId"];
            string schoolId     = payload["schoolId"];
            string sectionId    = payload["sectionId"];
            string gradeId      = payload["gradeId"];
            if (gamificationManager != null)
                gamificationManager.StudentPerformance(experianceID, schoolId, sectionId, gradeId);
            isFirstTime = false;
        }

        isStartButtonPressed = isStart;
        if (isStart && !isOpenGame)
        {
            isOpening = true;
            if (loadingBar != null) loadingBar.SynchDevice();
            TriggerWelcomeIfReady();
            isOpenGame = true;
        }
        if (isStop && isOpening && !string.IsNullOrEmpty(payload["experienceId"]) && !isStopCalling)
        {
            isStartButtonPressed = false;
            isStopCalling = true;
        }
    }

    private void HandleCommand(JSONNode cmd)
    {
        if (cmd == null) return;
        string action = cmd["action"];
        string msgId  = cmd["msgId"];
        KlassMqttClient.Instance?.PublishCmdAck(msgId, ok: true);
        if (action == "stop") LoadScene();
    }

    public void ActiveDeviceStatus()      => PublishStateIfChanged(force: false);

    public void SynDevice()
    {
        isSync = true;
        isStartGame = true;
        isSyncedDevice = true;
        PublishStateIfChanged(force: true);
        if (isStartButtonPressed) TriggerWelcomeIfReady();
    }

    public void ApplicationStatus()
    {
        DebugUtil.Log("reg the account");
    }

    public void LoadScene()
    {
        isGameOver = true;
        isCompletedDevice = true;
        PublishStateIfChanged(force: true);
        int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;
        SceneManager.LoadScene(currentSceneIndex);
    }

    private void TriggerWelcomeIfReady()
    {
        if (!isStartButtonPressed || !isActiveDevice || !isSync || !isOpenWelcomeScreen) return;
        isLobby = false;
        isOpenWelcomeScreen = false;
        if (WelcomeScene != null) WelcomeScene.SetActive(true);
        if (gamificationManager != null && gamificationManager.welcomeSessionScene != null && gamificationManager.welcomeSessionScene != WelcomeScene)
            gamificationManager.welcomeSessionScene.SetActive(true);
        if (apiManager != null && apiManager.isLoadedResponse == false)
            apiManager.CallingClassVrExperiance();
    }

    private void PublishStateIfChanged(bool force)
    {
        var mqtt = KlassMqttClient.Instance;
        if (mqtt == null || !mqtt.IsConnected) return;

        bool active    = true;
        bool synced    = isSync;
        bool completed = isGameOver;
        int progress   = synced ? 100 : Mathf.Clamp(_downloadProgress, 0, 100);
        string playState = completed ? "completed" : synced ? "ready" : (_downloadProgress > 0 ? "downloading" : "idle");

        // Include progress in the fingerprint so a changing % triggers a publish.
        var fp = $"{active}|{synced}|{completed}|{progress}|{playState}";
        if (!force && fp == _lastPublishedFingerprint) return;
        _lastPublishedFingerprint = fp;

        mqtt.PublishStateWithProgress(active, synced, completed, progress, playState);

        isActiveDevice    = active;
        isSyncedDevice    = synced;
        isCompletedDevice = completed;
    }

    /// <summary>
    /// Called by LoadingBar (and any other asset pipeline) every time the
    /// download progress changes. Throttled internally: we only re-publish
    /// when the integer percent changes, to avoid flooding the broker.
    /// </summary>
    public void PublishDownloadProgress(float percent01)
    {
        int p = Mathf.Clamp(Mathf.RoundToInt(percent01 * 100f), 0, 100);
        if (p == _downloadProgress) return;
        _downloadProgress = p;
        PublishStateIfChanged(force: false);
    }
}
