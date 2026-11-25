#if KLEVER_DPN
using System;
using System.Security.Cryptography;
using System.Text;
using RenderHeads.Media.AVProVideo;
using UnityEngine;

namespace Klever.DPVR
{
    public static class DpvrVideoRenditions
    {
        [Serializable] public class Entry { public string sourceKey; public string streamingPath; public int width; public int height; }
        [Serializable] private class Manifest { public Entry[] entries; }
        private static Manifest manifest;

        public static string SourceKey(string source)
        {
            if (!Uri.TryCreate(source, UriKind.Absolute, out var uri)) return "";
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(uri.GetLeftPart(UriPartial.Path)));
                var key = new StringBuilder();
                foreach (var b in bytes) key.Append(b.ToString("x2"));
                return key.ToString();
            }
        }

        public static MediaPath Resolve(string original)
        {
            if (manifest == null)
            {
                var json = Resources.Load<TextAsset>("DpvrVideoRenditions");
                manifest = json == null ? new Manifest() : JsonUtility.FromJson<Manifest>(json.text);
            }
            string key = SourceKey(original);
            if (manifest.entries != null)
                foreach (var entry in manifest.entries)
                    if (entry.sourceKey == key && !string.IsNullOrEmpty(entry.streamingPath) &&
                        entry.streamingPath.StartsWith("DPVR/Video/", StringComparison.Ordinal) && !entry.streamingPath.Contains(".."))
                    {
                        Debug.Log("[DPVR-VIDEO] Using packaged rendition " + entry.width + "x" + entry.height);
                        return new MediaPath(entry.streamingPath, MediaPathType.RelativeToStreamingAssetsFolder);
                    }
            return new MediaPath(original, MediaPathType.AbsolutePathOrURL);
        }
    }
}
#endif
