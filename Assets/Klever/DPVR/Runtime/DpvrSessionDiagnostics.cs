#if KLEVER_DPN
using System;
using System.IO;
using UnityEngine;

namespace Klever.DPVR
{
    // Only explicitly categorized port diagnostics are persisted. Never collect
    // raw API bodies, URLs, credentials, entered codes or arbitrary application logs.
    public sealed class DpvrSessionDiagnostics : MonoBehaviour
    {
        private string path;
        private int lines;
        private void Awake()
        {
            if (!Debug.isDebugBuild) return;
            try
            {
                var folder = Path.Combine(Application.persistentDataPath, "DPVR-Diagnostics");
                Directory.CreateDirectory(folder);
                path = Path.Combine(folder, "session-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".txt");
                File.AppendAllText(path, "Klever DPVR UAT content follow-up; UTC diagnostic session\n");
                Application.logMessageReceived += Record;
            }
            catch { path = null; }
        }
        private void Record(string message, string stack, LogType type)
        {
            if (path == null || lines >= 400 || !message.StartsWith("[DPVR-", StringComparison.Ordinal)) return;
            // Defense in depth if a future diagnostic accidentally includes a URL.
            if (message.Contains("://") || message.Contains("Bearer ")) return;
            try { File.AppendAllText(path, DateTime.UtcNow.ToString("HH:mm:ss.fff") + " " + message + "\n"); lines++; }
            catch { path = null; }
        }
        private void OnDestroy() => Application.logMessageReceived -= Record;
    }
}
#endif
