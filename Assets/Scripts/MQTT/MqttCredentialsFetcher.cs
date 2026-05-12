using System;
using System.Collections;
using System.Text;
using SimpleJSON;
using UnityEngine;
using UnityEngine.Networking;

namespace Klass.Net
{
    /// <summary>
    /// Fetches short-lived MQTT credentials from the backend using the headset's
    /// existing device-id authentication (POST /v1/mqtt/credentials/device).
    /// </summary>
    public class MqttCredentialsFetcher
    {
        public class Credentials
        {
            public string BrokerWsUrl;
            public string BrokerTcpUrl;
            public string Username;
            public string Password;
            public string ClientId;
            public long   ExpiresAt;          // unix seconds
            public int    KeepaliveSeconds = 30;
            public string SchoolId;           // parsed from username
            public string DeviceId;           // parsed from username

            public bool IsExpired => ExpiresAt > 0 && DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= ExpiresAt - 30;
        }

        public delegate void OnSuccessHandler(Credentials c);
        public delegate void OnFailureHandler(string error);

        public IEnumerator Fetch(OnSuccessHandler onSuccess, OnFailureHandler onFailure)
        {
            var url = KlassEndpoints.V1("mqtt/credentials/device");
            using (var req = new UnityWebRequest(url, "POST"))
            {
                // empty JSON body so server frameworks don't reject it
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes("{}"));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("device-id", SystemInfo.deviceUniqueIdentifier);
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    onFailure?.Invoke($"HTTP {req.responseCode}: {req.error}");
                    yield break;
                }

                var json = JSONNode.Parse(req.downloadHandler.text);
                if (json == null)
                {
                    onFailure?.Invoke("invalid JSON from credential endpoint");
                    yield break;
                }
                var c = new Credentials
                {
                    BrokerWsUrl  = json["brokerWsUrl"],
                    BrokerTcpUrl = json["brokerTcpUrl"],
                    Username     = json["username"],
                    Password     = json["password"],
                    ClientId     = json["clientId"],
                    ExpiresAt    = (long)json["expiresAt"].AsDouble,
                };
                if (json["keepaliveSeconds"] != null) c.KeepaliveSeconds = json["keepaliveSeconds"].AsInt;

                // Username pattern: device:{deviceId}@{schoolId}
                if (!string.IsNullOrEmpty(c.Username) && c.Username.StartsWith("device:"))
                {
                    var rest = c.Username.Substring("device:".Length);
                    var at = rest.IndexOf('@');
                    if (at > 0)
                    {
                        c.DeviceId = rest.Substring(0, at);
                        c.SchoolId = rest.Substring(at + 1);
                    }
                }
                if (string.IsNullOrEmpty(c.SchoolId) || string.IsNullOrEmpty(c.DeviceId))
                {
                    onFailure?.Invoke("malformed username from credential endpoint");
                    yield break;
                }
                onSuccess?.Invoke(c);
            }
        }
    }
}
