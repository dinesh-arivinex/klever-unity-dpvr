#if KLEVER_DPN
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using Vuplex.WebView;

namespace Klever.DPVR
{
    /// <summary>
    /// Converts DPVR 3DoF controller ray hits against the board's trigger collider
    /// directly into normalized Vuplex pointer events without eye-camera projection error.
    /// </summary>
    public sealed class DpvrBoardWebPointer : MonoBehaviour, IPointerInputDetector
    {
        public event EventHandler<EventArgs<Vector2>> BeganDrag;
        public event EventHandler<EventArgs<Vector2>> Dragged;
        public event EventHandler<PointerEventArgs> PointerDown;
        public event EventHandler PointerEntered;
        public event EventHandler<EventArgs<Vector2>> PointerExited;
        public event EventHandler<EventArgs<Vector2>> PointerMoved;
        public event EventHandler<PointerEventArgs> PointerUp;
        public event EventHandler<ScrolledEventArgs> Scrolled;

        public bool PointerMovedEnabled { get; set; } = true;

        private RectTransform rectTransform;
        private BoxCollider boardCollider;
        private CanvasWebViewPrefab webViewPrefab;
        private XRRayInteractor rayInteractor;
        private UnityEngine.InputSystem.InputAction scrollAction;

        private bool pageReady;
        private bool selectWasPressed;
        private float nextHoverTime;
        private bool isHovering;
        private bool isPressed;
        private bool dragStarted;
        private int clickCount = 1;
        private float lastDownTime = -10f;
        private float dragThresholdPixels = 20f;
        private Vector2 downPoint;
        private Vector2 lastPoint = new Vector2(0.5f, 0.5f);
        private Vector2 lastHoverPoint = new Vector2(-1f, -1f);
        private bool loggedFirstHover;
        private bool loggedFirstPress;

        public void Configure(CanvasWebViewPrefab prefab, float dragThreshold = 20f)
        {
            webViewPrefab = prefab;
            rectTransform = transform as RectTransform;
            dragThresholdPixels = Mathf.Max(1f, dragThreshold);
            SyncCollider();
            EnsureScrollAction();
        }

        public void SetPageReady(bool ready)
        {
            if (!ready) ReleaseIfPressed(lastPoint);
            pageReady = ready;
        }

        public void SyncCollider()
        {
            if (rectTransform == null) rectTransform = transform as RectTransform;
            if (rectTransform == null) return;
            if (boardCollider == null) boardCollider = GetComponent<BoxCollider>();
            if (boardCollider == null) boardCollider = gameObject.AddComponent<BoxCollider>();
            boardCollider.isTrigger = true;
            boardCollider.enabled = true;
            var rect = rectTransform.rect;
            float width = Mathf.Max(1f, rect.width);
            float height = Mathf.Max(1f, rect.height);
            // Offset slightly toward the viewer along local -Z so the board face
            // intercepts rays cleanly ahead of room wall/mesh colliders.
            float unitsPerMeter = 1f / Mathf.Max(0.00001f, transform.TransformVector(Vector3.forward).magnitude);
            boardCollider.center = new Vector3(rect.center.x, rect.center.y, -0.025f * unitsPerMeter);
            boardCollider.size = new Vector3(width, height, 0.01f * unitsPerMeter);
        }

        public static Vector2 NormalizedPointFromWorld(RectTransform target, Vector3 worldPoint)
        {
            Vector3 local = target.InverseTransformPoint(worldPoint);
            Vector2 normalized = Rect.PointToNormalized(target.rect, new Vector2(local.x, local.y));
            normalized.y = 1f - normalized.y;
            return new Vector2(Mathf.Clamp01(normalized.x), Mathf.Clamp01(normalized.y));
        }

        private void EnsureScrollAction()
        {
            if (scrollAction != null)
            {
                scrollAction.Enable();
                return;
            }
            scrollAction = new UnityEngine.InputSystem.InputAction("DPVR Board Web Scroll",
                UnityEngine.InputSystem.InputActionType.Value, expectedControlType: "Vector2");
            scrollAction.AddBinding("<KHRSimpleController>{RightHand}/uiScroll");
            scrollAction.AddBinding("<XRController>{RightHand}/uiScroll");
            scrollAction.AddBinding("<DpvrController>{RightHand}/uiScroll");
            scrollAction.Enable();
        }

        private XRRayInteractor ResolveRayInteractor()
        {
            if (rayInteractor != null && rayInteractor.isActiveAndEnabled) return rayInteractor;
            var controller = FindObjectOfType<DpvrThreeDofArmModel>();
            rayInteractor = controller != null ? controller.GetComponent<XRRayInteractor>() : null;
            return rayInteractor;
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying) return;
            if (rectTransform == null || boardCollider == null) SyncCollider();
            bool selectPressed = DpvrInput.SelectIsPressed;
            bool selectBegan = !selectWasPressed && (selectPressed || DpvrInput.SelectWasPressedThisFrame);
            selectWasPressed = selectPressed;
            var interactor = ResolveRayInteractor();
            if (!pageReady || interactor == null || !interactor.isActiveAndEnabled || LessonPause.IsPaused ||
                (Application.platform == RuntimePlatform.Android &&
                 dpn.DpnDaydreamController.State != dpn.DpnConnectionState.Connected))
            {
                ReleaseIfPressed(lastPoint);
                ExitIfHovering(lastPoint);
                return;
            }

            var origin = interactor.rayOriginTransform != null ? interactor.rayOriginTransform : interactor.transform;
            var ray = new Ray(origin.position, origin.forward);
            bool hasHit = TryRaycastBoard(ray, interactor, out var hitPoint);

            if (hasHit)
            {
                if (!isHovering)
                {
                    isHovering = true;
                    PointerEntered?.Invoke(this, EventArgs.Empty);
                    if (!loggedFirstHover && Debug.isDebugBuild)
                    {
                        loggedFirstHover = true;
                        Debug.Log("[DPVR-SIMULATION] Controller ray entered board simulation surface.");
                    }
                }
                if (PointerMovedEnabled && !isPressed && Time.unscaledTime >= nextHoverTime &&
                    Vector2.SqrMagnitude(hitPoint - lastHoverPoint) > 1e-6f)
                {
                    nextHoverTime = Time.unscaledTime + 1f / 30f;
                    lastHoverPoint = hitPoint;
                    PointerMoved?.Invoke(this, new EventArgs<Vector2>(hitPoint));
                }
            }
            else if (isHovering)
            {
                ExitIfHovering(lastPoint);
            }

            if (!isPressed && hasHit && selectBegan)
            {
                float now = Time.unscaledTime;
                clickCount = (now - lastDownTime <= 0.5f) ? clickCount + 1 : 1;
                lastDownTime = now;
                isPressed = true;
                dragStarted = false;
                downPoint = hitPoint;
                lastPoint = hitPoint;
                PointerDown?.Invoke(this, new PointerEventArgs {
                    Point = hitPoint,
                    Button = MouseButton.Left,
                    ClickCount = clickCount
                });
                if (!loggedFirstPress && Debug.isDebugBuild)
                {
                    loggedFirstPress = true;
                    Debug.Log("[DPVR-SIMULATION] Controller trigger dispatched to board simulation.");
                }
            }
            else if (isPressed && selectPressed)
            {
                Vector2 currentPoint = hasHit ? hitPoint
                    : (TryProjectOntoBoardPlane(ray, out var projected) ? projected : lastPoint);
                if (!dragStarted && ExceedsDragThreshold(downPoint, currentPoint))
                {
                    dragStarted = true;
                    BeganDrag?.Invoke(this, new EventArgs<Vector2>(downPoint));
                }
                if (dragStarted && Vector2.SqrMagnitude(currentPoint - lastPoint) > 1e-7f)
                {
                    lastPoint = currentPoint;
                    Dragged?.Invoke(this, new EventArgs<Vector2>(currentPoint));
                }
                else if (hasHit)
                {
                    lastPoint = currentPoint;
                }
            }
            else if (isPressed && !selectPressed)
            {
                Vector2 releasePoint = hasHit ? hitPoint : lastPoint;
                ReleaseIfPressed(releasePoint);
            }

            if (hasHit)
            {
                lastPoint = hitPoint;
                if (scrollAction != null)
                {
                    var delta = scrollAction.ReadValue<Vector2>();
                    if (delta.sqrMagnitude > 0.0001f)
                        Scrolled?.Invoke(this, new ScrolledEventArgs(-delta * 0.05f, hitPoint));
                }
            }
        }

        private bool TryRaycastBoard(Ray ray, XRRayInteractor interactor, out Vector2 normalizedPoint)
        {
            normalizedPoint = default;
            if (boardCollider == null || !boardCollider.enabled || !boardCollider.gameObject.activeInHierarchy)
                return false;
            if (!interactor.TryGetCurrent3DRaycastHit(out var hit) || hit.collider != boardCollider)
                return false;
            return TryProjectOntoBoardPlane(ray, out normalizedPoint);
        }

        private bool TryProjectOntoBoardPlane(Ray ray, out Vector2 normalizedPoint)
        {
            normalizedPoint = lastPoint;
            if (rectTransform == null) return false;
            var plane = new Plane(rectTransform.forward, rectTransform.position);
            if (!plane.Raycast(ray, out float distance) || distance <= 0f) return false;
            normalizedPoint = NormalizedPointFromWorld(rectTransform, ray.GetPoint(distance));
            return true;
        }

        private bool ExceedsDragThreshold(Vector2 a, Vector2 b)
        {
            var webView = webViewPrefab != null ? webViewPrefab.WebView : null;
            if (webView != null && webView.Size.x > 0 && webView.Size.y > 0)
            {
                var pixelDelta = new Vector2((a.x - b.x) * webView.Size.x, (a.y - b.y) * webView.Size.y);
                return pixelDelta.magnitude > dragThresholdPixels;
            }
            var rectSize = rectTransform != null ? rectTransform.rect.size : new Vector2(800f, 450f);
            return new Vector2((a.x - b.x) * rectSize.x, (a.y - b.y) * rectSize.y).magnitude > dragThresholdPixels;
        }

        private void ExitIfHovering(Vector2 point)
        {
            if (!isHovering) return;
            isHovering = false;
            lastHoverPoint = new Vector2(-1f, -1f);
            PointerExited?.Invoke(this, new EventArgs<Vector2>(point));
        }

        private void ReleaseIfPressed(Vector2 point)
        {
            if (!isPressed) return;
            isPressed = false;
            dragStarted = false;
            PointerUp?.Invoke(this, new PointerEventArgs {
                Point = point,
                Button = MouseButton.Left,
                ClickCount = clickCount
            });
        }

        private void OnDisable()
        {
            ReleaseIfPressed(lastPoint);
            ExitIfHovering(lastPoint);
            pageReady = false;
            scrollAction?.Disable();
        }

        private void OnDestroy()
        {
            ReleaseIfPressed(lastPoint);
            ExitIfHovering(lastPoint);
            scrollAction?.Dispose();
            scrollAction = null;
        }
    }
}
#endif
