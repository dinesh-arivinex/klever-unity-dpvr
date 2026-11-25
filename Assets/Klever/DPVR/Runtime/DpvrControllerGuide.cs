#if KLEVER_DPN
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Klever.DPVR
{
    public sealed class DpvrControllerGuide : MonoBehaviour
    {
        private ChangeClassRoom room;
        private Renderer cachedSurface;
        private int cachedRoom = -1;
        private readonly Vector3[] corners = new Vector3[4];
        public const string Instructions = "<b>POINT & CLICK</b>\nAim the ray. Press and release Trigger to select.\nA touchpad click also selects buttons.\n\n<b>MOVE A 3D MODEL</b>\nAim at the model and hold Trigger to select it.\nMove slowly to reposition it above the table. Release to leave it.\n\n<b>WHILE HOLDING TRIGGER</b>\nTouch left / right on the touchpad to rotate.\nTouch up / down to resize. Return to centre to stop.\n\n<b>ANNOTATIONS & SOUND</b>\nRelease the model, then click a label or speaker icon.";

        public static void ConfigureAll()
        {
            foreach (var canvas in Resources.FindObjectsOfTypeAll<Canvas>())
                if (canvas.gameObject.scene.IsValid() && (canvas.name == "Controller Panel" || canvas.name == "Controller Panel(Clone)") &&
                    canvas.GetComponent<DpvrControllerGuide>() == null)
                    canvas.gameObject.AddComponent<DpvrControllerGuide>().Build();
        }

        public void Build()
        {
            foreach (var candidate in Resources.FindObjectsOfTypeAll<ChangeClassRoom>())
                if (candidate.gameObject.scene == gameObject.scene) { room = candidate; break; }
            var original = GetComponentInChildren<TMP_Text>(true);
            var font = original != null ? original.font : TMP_Settings.defaultFontAsset;
            foreach (Transform child in transform)
                if (child.GetComponentInChildren<Selectable>(true) == null) child.gameObject.SetActive(false);
            var content = new GameObject("DPVR controller instructions", typeof(RectTransform), typeof(Image));
            content.transform.SetParent(transform, false);
            var rect = content.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            var background = content.GetComponent<Image>();
            background.color = new Color(0.035f, 0.075f, 0.12f, 0.96f); background.raycastTarget = false;
            AddText(content.transform, font, "DPVR P1 Pro Edu", new Vector2(80, -55), new Vector2(1750, 110), 76);
            AddText(content.transform, font, Instructions, new Vector2(80, -200), new Vector2(1260, 820), 43);
            AddText(content.transform, font, "ONE CONTROLLER\n\n<b>TOUCHPAD</b>\nClick to select\nTouch to rotate / resize\nwhile holding Trigger\n\n<b>TRIGGER</b>\nClick to select\nHold to move a model", new Vector2(1400, -230), new Vector2(450, 760), 40);
            LateUpdate();
        }

        private void LateUpdate()
        {
            if (room == null) return;
            if (cachedRoom != room.classRoom || cachedSurface == null)
            {
                cachedRoom = room.classRoom;
                var boardObject = room.classRoom == 1 ? room.boardB : room.classRoom == 2 ? room.boardC : room.boardA;
                if (boardObject == null) return;
                cachedSurface = boardObject.GetComponent<Renderer>();
                if (cachedSurface == null)
                    foreach (var candidate in boardObject.GetComponentsInChildren<Renderer>(true))
                        if (candidate.name == "Board") { cachedSurface = candidate; break; }
            }
            if (cachedSurface != null) FitToBoard(cachedSurface, DpvrRuntimeBootstrap.LegacyStartEyePosition);
        }

        public void FitToBoard(Renderer surface, Vector3 viewer)
        {
            Klever.VR.ClassroomBoardLayout.Fit(GetComponent<RectTransform>(), surface, viewer);
        }

        private static void AddText(Transform parent, TMP_FontAsset font, string value, Vector2 position, Vector2 size, float fontSize)
        {
            var obj = new GameObject("DPVR guide text", typeof(RectTransform), typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);
            var text = obj.GetComponent<TextMeshProUGUI>();
            text.font = font; text.text = value; text.fontSize = fontSize; text.color = Color.white; text.raycastTarget = false;
            text.enableWordWrapping = true; text.enableAutoSizing = true; text.fontSizeMin = fontSize * 0.8f; text.fontSizeMax = fontSize; text.overflowMode = TextOverflowModes.Truncate;
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = text.rectTransform.pivot = new Vector2(0, 1);
            text.rectTransform.anchoredPosition = position; text.rectTransform.sizeDelta = size;
        }
    }
}
#endif
