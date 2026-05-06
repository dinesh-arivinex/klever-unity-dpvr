using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using SimpleJSON;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using UnityEngine;

namespace Klass.Net
{
    /// <summary>
    /// Singleton MonoBehaviour wrapping M2Mqtt for the Klever_VR headset.
    ///
    /// Responsibilities:
    ///  - Fetch credentials (HTTP), connect to EMQX (TCP), publish LWT-aware status.
    ///  - Auto-reconnect with exponential backoff.
    ///  - Marshal incoming messages onto the Unity main thread so handlers can
    ///    safely touch GameObjects.
    ///  - Expose simple PublishState/PublishTelemetry/PublishCmdAck helpers and
    ///    OnSessionControl / OnCommand events.
    ///
    /// IMPORTANT: this script must live in a scene (or be instantiated with
    /// DontDestroyOnLoad) before any consumers call Instance.
    /// </summary>
    public class KlassMqttClient : MonoBehaviour
    {
        public static KlassMqttClient Instance { get; private set; }

        [Header("Tuning")]
        public float reconnectInitialSeconds = 2f;
        public float reconnectMaxSeconds = 30f;

        // ── Public state ───────────────────────────────────────────────────
        public bool IsConnected { get; private set; }
        public string SchoolId  { get; private set; }
        public string DeviceId  { get; private set; }

        // ── Public events (always fired on Unity main thread) ──────────────
        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<JSONNode> OnSessionControl; // payload of session/control
        public event Action<JSONNode> OnCommand;        // payload of devices/{me}/cmd
        public event Action<JSONNode> OnBroadcast;      // payload of broadcast

        // ── Internals ──────────────────────────────────────────────────────
        private MqttClient _client;
        private MqttCredentialsFetcher.Credentials _creds;
        private readonly ConcurrentQueue<Action> _mainThread = new ConcurrentQueue<Action>();
        private readonly object _lock = new object();
        private float _backoff;
        private bool _wantConnected;
        private Coroutine _reconnectLoop;
        private Coroutine _refreshLoop;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Update()
        {
            // Drain main-thread work queued from MQTT worker thread
            while (_mainThread.TryDequeue(out var act))
            {
                try { act(); } catch (Exception ex) { Debug.LogException(ex); }
            }
        }

        void OnApplicationQuit()
        {
            _wantConnected = false;
            DisconnectInternal("app quit");
        }

        // ── Public API ─────────────────────────────────────────────────────

        /// <summary>Begin connecting (idempotent). Will keep retrying.</summary>
        public void Begin()
        {
            if (_wantConnected) return;
            _wantConnected = true;
            _backoff = reconnectInitialSeconds;
            _reconnectLoop = StartCoroutine(ReconnectLoop());
        }

        public void End()
        {
            _wantConnected = false;
            if (_reconnectLoop != null) StopCoroutine(_reconnectLoop);
            if (_refreshLoop != null) StopCoroutine(_refreshLoop);
            DisconnectInternal("end");
        }

        public void PublishState(bool isActive, bool isSynced, bool isCompleted, string playState = null, string experienceId = null)
        {
            if (!IsConnected) return;
            var node = new JSONObject();
            node["isActive"]    = isActive;
            node["isSynced"]    = isSynced;
            node["isCompleted"] = isCompleted;
            if (!string.IsNullOrEmpty(playState))    node["playState"]    = playState;
            if (!string.IsNullOrEmpty(experienceId)) node["experienceId"] = experienceId;
            node["ts"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            PublishJson(MqttTopics.State(SchoolId, DeviceId), node, retain: true, qos: 1);
        }

        public void PublishTelemetry(JSONObject node)
        {
            if (!IsConnected) return;
            PublishJson(MqttTopics.Telemetry(SchoolId, DeviceId), node, retain: false, qos: 0);
        }

        public void PublishCmdAck(string msgId, bool ok, string error = null)
        {
            if (!IsConnected) return;
            var node = new JSONObject();
            node["msgId"] = msgId ?? "";
            node["ok"] = ok;
            if (!string.IsNullOrEmpty(error)) node["error"] = error;
            node["ts"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            PublishJson(MqttTopics.CmdAck(SchoolId, DeviceId), node, retain: false, qos: 1);
        }

        public void PublishJson(string topic, JSONNode body, bool retain, byte qos)
        {
            if (!IsConnected) return;
            try
            {
                var bytes = Encoding.UTF8.GetBytes(body == null ? "{}" : body.ToString());
                _client.Publish(topic, bytes, qos, retain);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[KlassMqtt] publish failed on {topic}: {ex.Message}");
            }
        }

        // ── Connect + reconnect loop ───────────────────────────────────────

        private IEnumerator ReconnectLoop()
        {
            while (_wantConnected)
            {
                if (!IsConnected)
                {
                    yield return ConnectOnce();
                    if (!IsConnected)
                    {
                        var wait = _backoff;
                        _backoff = Mathf.Min(reconnectMaxSeconds, _backoff * 2f);
                        yield return new WaitForSeconds(wait);
                        continue;
                    }
                    _backoff = reconnectInitialSeconds;
                }
                yield return new WaitForSeconds(1f);
            }
        }

        private IEnumerator ConnectOnce()
        {
            // 1. (re)fetch creds if missing or expired
            if (_creds == null || _creds.IsExpired)
            {
                bool done = false;
                bool ok = false;
                string err = null;
                yield return new MqttCredentialsFetcher().Fetch(
                    c => { _creds = c; ok = true; done = true; },
                    e => { err = e; done = true; });
                if (!done) yield break;
                if (!ok) { Debug.LogWarning($"[KlassMqtt] credential fetch failed: {err}"); yield break; }
                SchoolId = _creds.SchoolId;
                DeviceId = _creds.DeviceId;
            }

            // 2. open TCP connection
            try
            {
                _client = new MqttClient(KlassEndpoints.MqttHost, KlassEndpoints.MqttPort, KlassEndpoints.MqttUseTls,
                    null, null, MqttSslProtocols.TLSv1_2);
                _client.MqttMsgPublishReceived += HandleIncomingMessage;
                _client.ConnectionClosed += (_, __) => {
                    EnqueueMain(() => { IsConnected = false; OnDisconnected?.Invoke(); });
                };

                // Last Will: broker auto-publishes when this client drops/keepalive-times-out
                var willTopic   = MqttTopics.Status(SchoolId, DeviceId);
                var willPayload = Encoding.UTF8.GetBytes("{\"online\":false,\"ts\":0}");
                byte code = _client.Connect(
                    _creds.ClientId,
                    _creds.Username,
                    _creds.Password,
                    /*willRetain*/ true,
                    /*willQos*/ MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE,
                    /*willFlag*/ true,
                    willTopic,
                    Encoding.UTF8.GetString(willPayload),
                    /*cleanSession*/ false,
                    /*keepAlivePeriod*/ (ushort)_creds.KeepaliveSeconds);

                if (code != 0)
                {
                    Debug.LogWarning($"[KlassMqtt] connect refused, code={code}");
                    SafeDisconnect();
                    yield break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[KlassMqtt] connect threw: {ex.Message}");
                SafeDisconnect();
                yield break;
            }

            IsConnected = true;

            // 3. Subscribe to commands & session topics
            try
            {
                _client.Subscribe(
                    new[] {
                        MqttTopics.Cmd(SchoolId, DeviceId),
                        MqttTopics.SessionControl(SchoolId),
                        MqttTopics.Broadcast(SchoolId),
                    },
                    new[] {
                        MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE,
                        MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE,
                        MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE,
                    });
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[KlassMqtt] subscribe failed: {ex.Message}");
            }

            // 4. Publish online presence (retained)
            try
            {
                var online = new JSONObject();
                online["online"] = true;
                online["ts"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                _client.Publish(MqttTopics.Status(SchoolId, DeviceId),
                    Encoding.UTF8.GetBytes(online.ToString()),
                    MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE, /*retain*/ true);
            }
            catch (Exception ex) { Debug.LogWarning($"[KlassMqtt] presence publish failed: {ex.Message}"); }

            EnqueueMain(() => OnConnected?.Invoke());

            // 5. Schedule credential refresh
            if (_refreshLoop != null) StopCoroutine(_refreshLoop);
            _refreshLoop = StartCoroutine(CredentialRefreshLoop());
        }

        private IEnumerator CredentialRefreshLoop()
        {
            while (_wantConnected && IsConnected)
            {
                var ttl = _creds.ExpiresAt - DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var sleep = Mathf.Max(60f, (float)(ttl - 60));
                yield return new WaitForSeconds(sleep);
                // Force reconnect with fresh creds (keeps things simple & robust)
                Debug.Log("[KlassMqtt] credential refresh triggered");
                _creds = null;
                SafeDisconnect();
                IsConnected = false;
                EnqueueMain(() => OnDisconnected?.Invoke());
                yield break;
            }
        }

        private void HandleIncomingMessage(object sender, MqttMsgPublishEventArgs e)
        {
            // M2Mqtt fires this on a worker thread — defer handler invocation.
            string topic = e.Topic;
            string payload = e.Message != null ? Encoding.UTF8.GetString(e.Message) : "";
            EnqueueMain(() =>
            {
                JSONNode node = null;
                try { node = JSONNode.Parse(payload); } catch { /* not JSON */ }

                if (topic == MqttTopics.SessionControl(SchoolId))      OnSessionControl?.Invoke(node);
                else if (topic == MqttTopics.Broadcast(SchoolId))      OnBroadcast?.Invoke(node);
                else if (topic == MqttTopics.Cmd(SchoolId, DeviceId))  OnCommand?.Invoke(node);
            });
        }

        private void DisconnectInternal(string reason)
        {
            try
            {
                if (_client != null && _client.IsConnected)
                {
                    // best-effort offline status before clean disconnect
                    try
                    {
                        var off = new JSONObject();
                        off["online"] = false;
                        off["ts"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                        _client.Publish(MqttTopics.Status(SchoolId, DeviceId),
                            Encoding.UTF8.GetBytes(off.ToString()),
                            MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE, true);
                    }
                    catch { /* swallow */ }
                    _client.Disconnect();
                }
            }
            catch { /* swallow */ }
            finally
            {
                IsConnected = false;
                _client = null;
            }
            Debug.Log($"[KlassMqtt] disconnected: {reason}");
        }

        private void SafeDisconnect()
        {
            try { if (_client != null && _client.IsConnected) _client.Disconnect(); } catch { /* swallow */ }
            _client = null;
            IsConnected = false;
        }

        private void EnqueueMain(Action a) => _mainThread.Enqueue(a);
    }
}
