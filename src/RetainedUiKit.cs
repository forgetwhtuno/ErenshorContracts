using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ErenshorContracts
{
    internal static class RetainedUiKit
    {
        // Local mechanical copy of the canonical Suite / Sim Actions visual tokens.
        // These mods remain independently loadable; sharing colors must not create a Hub dependency.
        internal static readonly Color Panel = new Color(0.015f, 0.09f, 0.125f, 0.72f);
        internal static readonly Color Header = new Color(0.025f, 0.13f, 0.17f, 0.88f);
        internal static readonly Color Button = new Color(0.035f, 0.17f, 0.22f, 0.78f);
        internal static readonly Color ButtonHover = new Color(0.12f, 0.38f, 0.48f, 0.90f);
        internal static readonly Color Selected = new Color(0.07f, 0.28f, 0.34f, 0.94f);
        internal static readonly Color Danger = new Color(0.30f, 0.15f, 0.08f, 0.88f);
        internal static readonly Color TextBack = new Color(0.012f, 0.05f, 0.065f, 0.90f);
        internal static readonly Color Edge = new Color(0.03f, 0.67f, 0.86f, 0.95f);
        internal static readonly Color Text = new Color(0.88f, 0.92f, 0.91f, 1f);
        internal static readonly Color Muted = new Color(0.56f, 0.78f, 0.88f, 1f);

        internal static GameObject CreateCanvas(string name, int sortingOrder)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            scaler.referencePixelsPerUnit = 100f;
            UnityEngine.Object.DontDestroyOnLoad(root);
            return root;
        }

        internal static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        internal static Image AddImage(RectTransform rect, Color color)
        {
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = true;
            return image;
        }

        internal static void Stretch(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        internal static void AnchorBottomLeft(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }


        internal static bool FitToScreen(RectTransform rect, float margin)
        {
            if (rect == null) return false;
            float safeMargin = Mathf.Max(0f, margin);
            float maxWidth = Mathf.Max(1f, Screen.width - (safeMargin * 2f));
            float maxHeight = Mathf.Max(1f, Screen.height - (safeMargin * 2f));
            float width = Mathf.Min(rect.rect.width, maxWidth);
            float height = Mathf.Min(rect.rect.height, maxHeight);
            bool changed = Math.Abs(width - rect.rect.width) > 0.1f || Math.Abs(height - rect.rect.height) > 0.1f;
            if (!changed) return false;
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            return true;
        }

        internal static void AddFrame(RectTransform parent, float thickness)
        {
            if (parent == null) return;
            float t = Mathf.Max(1f, thickness);
            AddFrameEdge("FrameTop", parent, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -t), new Vector2(0f, 0f));
            AddFrameEdge("FrameBottom", parent, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, 0f), new Vector2(0f, t));
            AddFrameEdge("FrameLeft", parent, new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(0f, 0f), new Vector2(t, 0f));
            AddFrameEdge("FrameRight", parent, new Vector2(1f, 0f), new Vector2(1f, 1f),
                new Vector2(-t, 0f), new Vector2(0f, 0f));
        }

        private static void AddFrameEdge(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            RectTransform edge = CreateRect(name, parent);
            edge.anchorMin = anchorMin;
            edge.anchorMax = anchorMax;
            edge.pivot = new Vector2(0.5f, 0.5f);
            edge.offsetMin = offsetMin;
            edge.offsetMax = offsetMax;
            Image image = AddImage(edge, Edge);
            image.raycastTarget = false;
        }

        internal static void AnchorTopStretch(RectTransform rect, float left, float top, float right, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, -top - height);
            rect.offsetMax = new Vector2(-right, -top);
        }

        internal static TextMeshProUGUI AddLabel(string name, Transform parent, string value, float fontSize, FontStyles style, TextAlignmentOptions alignment)
        {
            RectTransform rect = CreateRect(name, parent);
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value ?? string.Empty;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = Text;
            text.alignment = alignment;
            text.enableWordWrapping = true;
            text.raycastTarget = false;
            return text;
        }

        internal static Button AddButton(string name, Transform parent, string label, Action onClick, float width, float height, bool danger)
        {
            RectTransform rect = CreateRect(name, parent);
            Image image = AddImage(rect, danger ? Danger : Button);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = danger ? Danger : Button;
            colors.highlightedColor = ButtonHover;
            colors.pressedColor = Selected;
            colors.selectedColor = Selected;
            button.colors = colors;
            LayoutElement le = rect.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = height;
            le.minHeight = height;
            if (onClick != null) button.onClick.AddListener(delegate { onClick(); });

            TextMeshProUGUI text = AddLabel("Label", rect, label, 11f, FontStyles.Normal, TextAlignmentOptions.Center);
            Stretch(text.rectTransform, 4f, 1f, 4f, 1f);
            return button;
        }

        internal static RectTransform AddHorizontalRow(string name, Transform parent, float height, float spacing)
        {
            RectTransform rect = CreateRect(name, parent);
            HorizontalLayoutGroup layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            LayoutElement le = rect.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.minHeight = height;
            return rect;
        }

        internal static RectTransform AddVerticalContent(string name, Transform parent, float spacing, int padding)
        {
            RectTransform rect = CreateRect(name, parent);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            VerticalLayoutGroup layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = rect.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return rect;
        }

        internal static ScrollRect AddScrollRect(string name, Transform parent, bool horizontal, bool vertical, out RectTransform viewport, out RectTransform content)
        {
            RectTransform scrollRect = CreateRect(name, parent);
            ScrollRect scroll = scrollRect.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = horizontal;
            scroll.vertical = vertical;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;

            viewport = CreateRect("Viewport", scrollRect);
            AddImage(viewport, new Color(0f, 0f, 0f, 0.02f));
            viewport.gameObject.AddComponent<RectMask2D>();
            Stretch(viewport, 0f, 0f, 0f, 0f);

            content = CreateRect("Content", viewport);
            if (vertical)
            {
                content.anchorMin = new Vector2(0f, 1f);
                content.anchorMax = new Vector2(1f, 1f);
                content.pivot = new Vector2(0.5f, 1f);
            }
            else
            {
                content.anchorMin = new Vector2(0f, 0f);
                content.anchorMax = new Vector2(0f, 1f);
                content.pivot = new Vector2(0f, 0.5f);
            }
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            scroll.viewport = viewport;
            scroll.content = content;
            return scroll;
        }

        internal static TMP_InputField AddInputField(string name, Transform parent, string value, bool multiline, int charLimit)
        {
            RectTransform root = CreateRect(name, parent);
            Image back = AddImage(root, TextBack);

            RectTransform viewport = CreateRect("Text Area", root);
            viewport.gameObject.AddComponent<RectMask2D>();
            Stretch(viewport, 7f, 5f, 7f, 5f);

            TextMeshProUGUI text = AddLabel("Text", viewport, value, 12f, FontStyles.Normal,
                multiline ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.Left);
            Stretch(text.rectTransform, 0f, 0f, 0f, 0f);
            text.raycastTarget = false;

            TextMeshProUGUI placeholder = AddLabel("Placeholder", viewport, string.Empty, 12f, FontStyles.Italic,
                multiline ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.Left);
            placeholder.color = Muted;
            Stretch(placeholder.rectTransform, 0f, 0f, 0f, 0f);

            TMP_InputField input = root.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = back;
            input.textViewport = viewport;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.text = value ?? string.Empty;
            input.characterLimit = charLimit;
            input.lineType = multiline ? TMP_InputField.LineType.MultiLineNewline : TMP_InputField.LineType.SingleLine;
            input.richText = false;
            return input;
        }

        internal static SuiteDragHandler AddDragSurface(string name, Transform parent, RectTransform target,
            float leftExclusion, float rightExclusion, Action onCompleted)
        {
            RectTransform rect = CreateRect(name, parent);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(Mathf.Max(0f, leftExclusion), 0f);
            rect.offsetMax = new Vector2(-Mathf.Max(0f, rightExclusion), 0f);
            Image hit = AddImage(rect, Color.clear);
            hit.raycastTarget = true;
            SuiteDragHandler drag = rect.gameObject.AddComponent<SuiteDragHandler>();
            drag.Target = target;
            drag.OnDragCompleted = onCompleted;
            return drag;
        }

        internal static SuiteResizeHandler AddResizeGrip(string name, Transform parent, RectTransform target,
            float size, Vector2 minimumSize, Action<float, float> onCompleted)
        {
            RectTransform rect = CreateRect(name, parent);
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-2f, 2f);
            rect.sizeDelta = new Vector2(size, size);
            Image hit = AddImage(rect, new Color(Edge.r, Edge.g, Edge.b, 0.65f));
            hit.raycastTarget = true;
            TextMeshProUGUI mark = AddLabel("Mark", rect, "↗", 10f, FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(mark.rectTransform, 0f, 0f, 0f, 0f);

            SuiteResizeHandler resize = rect.gameObject.AddComponent<SuiteResizeHandler>();
            resize.Target = target;
            resize.MinimumSize = minimumSize;
            resize.OnResizeCompleted = onCompleted;
            return resize;
        }

        internal static void ClearChildren(RectTransform parent)
        {
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (child != null) UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }

        internal static void DestroyRoot(ref GameObject root)
        {
            if (root == null) return;
            UnityEngine.Object.DestroyImmediate(root);
            root = null;
        }
    }

    internal sealed class SuiteDragHandler : MonoBehaviour,
        IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerUpHandler
    {
        private static readonly HashSet<SuiteDragHandler> _owners = new HashSet<SuiteDragHandler>();
        internal static bool HasOwners { get { return _owners.Count > 0; } }
        internal RectTransform Target;
        internal Action OnDragCompleted;

        private RectTransform _parentRect;
        private Vector2 _startPointer;
        private Vector2 _startPosition;
        private readonly SuiteUiGestureState _gesture = new SuiteUiGestureState();
        private bool _owning;

        public void OnPointerDown(PointerEventData eventData) { }

        public void OnBeginDrag(PointerEventData eventData)
        {
            try
            {
                if (eventData == null || eventData.button != PointerEventData.InputButton.Left) return;
                if (Target == null) Target = GetComponent<RectTransform>();
                if (Target == null) return;
                _parentRect = Target.parent as RectTransform;
                if (_parentRect == null) return;
                Vector2 local;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_parentRect, eventData.position, eventData.pressEventCamera, out local)) return;
                _startPointer = local;
                _startPosition = Target.anchoredPosition;
                _gesture.Begin();
                if (!_owning)
                {
                    _owning = true;
                    _owners.Add(this);
                }
                GameData.DraggingUIElement = true;
            }
            catch (Exception)
            {
                EndDrag(false);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_gesture.IsActive || Target == null || _parentRect == null) return;
            try
            {
                Vector2 local;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_parentRect, eventData.position, eventData.pressEventCamera, out local)) return;
                Vector2 next = _startPosition + (local - _startPointer);
                Rect pr = _parentRect.rect;
                Rect tr = Target.rect;
                next.x = Mathf.Clamp(next.x, 0f, Mathf.Max(0f, pr.width - tr.width));
                next.y = Mathf.Clamp(next.y, 0f, Mathf.Max(0f, pr.height - tr.height));
                Target.anchoredPosition = next;
            }
            catch (Exception)
            {
                EndDrag(false);
            }
        }

        public void OnEndDrag(PointerEventData eventData) { EndDrag(true); }
        public void OnPointerUp(PointerEventData eventData) { EndDrag(false); }
        private void OnDisable() { EndDrag(true); }
        private void OnDestroy() { EndDrag(true); }

        private void EndDrag(bool notify)
        {
            bool wasDragging = _gesture.End();
            Release();
            if (notify && wasDragging && OnDragCompleted != null)
            {
                try { OnDragCompleted(); } catch (Exception) { }
            }
        }

        private void Release()
        {
            if (!_owning) return;
            _owning = false;
            _owners.Remove(this);
            if (_owners.Count == 0 && !SuiteResizeHandler.HasOwners)
            {
                try { GameData.DraggingUIElement = false; } catch (Exception) { }
            }
        }

        internal static void ForceReleaseIfOwned()
        {
            bool ownedByThisMod = _owners.Count > 0 || SuiteResizeHandler.HasOwners;
            if (_owners.Count > 0)
            {
                SuiteDragHandler[] owners = new SuiteDragHandler[_owners.Count];
                _owners.CopyTo(owners);
                for (int i = 0; i < owners.Length; i++)
                {
                    SuiteDragHandler owner = owners[i];
                    if (owner == null) continue;
                    owner._gesture.ForceRelease();
                    owner._owning = false;
                    owner._parentRect = null;
                }
                _owners.Clear();
            }
            SuiteResizeHandler.ForceReleaseIfOwned();
            if (ownedByThisMod)
            {
                try { GameData.DraggingUIElement = false; } catch (Exception) { }
            }
        }
    }

    internal sealed class SuiteResizeHandler : MonoBehaviour,
        IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerUpHandler
    {
        private static readonly HashSet<SuiteResizeHandler> _owners = new HashSet<SuiteResizeHandler>();
        internal static bool HasOwners { get { return _owners.Count > 0; } }
        internal RectTransform Target;
        internal Vector2 MinimumSize = new Vector2(320f, 240f);
        internal Action<float, float> OnResizeCompleted;

        private RectTransform _parentRect;
        private Vector2 _startPointer;
        private Vector2 _startSize;
        private readonly SuiteUiGestureState _gesture = new SuiteUiGestureState();
        private bool _owning;

        public void OnPointerDown(PointerEventData eventData) { }

        public void OnBeginDrag(PointerEventData eventData)
        {
            try
            {
                if (eventData == null || eventData.button != PointerEventData.InputButton.Left) return;
                if (Target == null) return;
                _parentRect = Target.parent as RectTransform;
                if (_parentRect == null) return;
                Vector2 local;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_parentRect, eventData.position, eventData.pressEventCamera, out local)) return;
                _startPointer = local;
                _startSize = Target.rect.size;
                _gesture.Begin();
                if (!_owning)
                {
                    _owning = true;
                    _owners.Add(this);
                }
                GameData.DraggingUIElement = true;
            }
            catch (Exception)
            {
                EndResize(false);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_gesture.IsActive || Target == null || _parentRect == null) return;
            try
            {
                Vector2 local;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_parentRect, eventData.position, eventData.pressEventCamera, out local)) return;
                Vector2 delta = local - _startPointer;
                Rect parentRect = _parentRect.rect;
                float maxWidth = Mathf.Max(120f, parentRect.width - Target.anchoredPosition.x);
                float maxHeight = Mathf.Max(120f, parentRect.height - Target.anchoredPosition.y);
                float minWidth = Mathf.Min(Mathf.Max(120f, MinimumSize.x), maxWidth);
                float minHeight = Mathf.Min(Mathf.Max(120f, MinimumSize.y), maxHeight);
                float width = Mathf.Clamp(_startSize.x + delta.x, minWidth, maxWidth);
                float height = Mathf.Clamp(_startSize.y + delta.y, minHeight, maxHeight);
                Target.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
                Target.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            }
            catch (Exception)
            {
                EndResize(false);
            }
        }

        public void OnEndDrag(PointerEventData eventData) { EndResize(true); }
        public void OnPointerUp(PointerEventData eventData) { EndResize(false); }
        private void OnDisable() { EndResize(true); }
        private void OnDestroy() { EndResize(true); }

        private void EndResize(bool notify)
        {
            bool wasResizing = _gesture.End();
            Release();
            if (notify && wasResizing && Target != null)
            {
                try { LayoutRebuilder.ForceRebuildLayoutImmediate(Target); } catch (Exception) { }
                if (OnResizeCompleted != null)
                {
                    try { OnResizeCompleted(Target.rect.width, Target.rect.height); } catch (Exception) { }
                }
            }
        }

        private void Release()
        {
            if (!_owning) return;
            _owning = false;
            _owners.Remove(this);
            if (_owners.Count == 0 && !SuiteDragHandler.HasOwners)
            {
                try { GameData.DraggingUIElement = false; } catch (Exception) { }
            }
        }

        internal static void ForceReleaseIfOwned()
        {
            if (_owners.Count > 0)
            {
                SuiteResizeHandler[] owners = new SuiteResizeHandler[_owners.Count];
                _owners.CopyTo(owners);
                for (int i = 0; i < owners.Length; i++)
                {
                    SuiteResizeHandler owner = owners[i];
                    if (owner == null) continue;
                    owner._gesture.ForceRelease();
                    owner._owning = false;
                    owner._parentRect = null;
                }
                _owners.Clear();
            }
        }
    }

    internal sealed class RetainedPosition
    {
        private readonly float _defaultX;
        private readonly float _defaultY;
        private readonly Action<float, float> _persist;
        private float _storedX;
        private float _storedY;
        private int _lastWidth = -1;
        private int _lastHeight = -1;

        internal RetainedPosition(float storedX, float storedY, float defaultX, float defaultY, Action<float, float> persist)
        {
            _storedX = SuiteUiPositionPolicy.InterpretStoredAxis(storedX);
            _storedY = SuiteUiPositionPolicy.InterpretStoredAxis(storedY);
            _defaultX = defaultX;
            _defaultY = defaultY;
            _persist = persist;
        }

        internal void Resolve(RectTransform target)
        {
            if (target == null) return;
            if (_lastWidth == Screen.width && _lastHeight == Screen.height) return;
            _lastWidth = Screen.width;
            _lastHeight = Screen.height;
            target.anchoredPosition = new Vector2(
                SuiteUiPositionPolicy.ResolveAxis(_storedX, _defaultX, Screen.width, target.rect.width),
                SuiteUiPositionPolicy.ResolveAxis(_storedY, _defaultY, Screen.height, target.rect.height));
        }

        internal void DragCompleted(RectTransform target)
        {
            if (target == null) return;
            Vector2 current = target.anchoredPosition;
            if (!SuiteUiPositionPolicy.IsFinite(current.x) || !SuiteUiPositionPolicy.IsFinite(current.y))
            {
                _lastWidth = -1;
                _lastHeight = -1;
                Resolve(target);
                return;
            }
            Clamp(target);
            _storedX = SuiteUiPositionPolicy.NormalizeAxis(target.anchoredPosition.x, Screen.width);
            _storedY = SuiteUiPositionPolicy.NormalizeAxis(target.anchoredPosition.y, Screen.height);
            _lastWidth = Screen.width;
            _lastHeight = Screen.height;
            if (_persist != null) _persist(_storedX, _storedY);
        }

        internal void Reset(RectTransform target)
        {
            _storedX = SuiteUiPositionPolicy.Unset;
            _storedY = SuiteUiPositionPolicy.Unset;
            _lastWidth = -1;
            _lastHeight = -1;
            Resolve(target);
            if (_persist != null) _persist(_storedX, _storedY);
        }

        internal void Clamp(RectTransform target)
        {
            if (target == null) return;
            Vector2 p = target.anchoredPosition;
            if (!SuiteUiPositionPolicy.IsFinite(p.x) || !SuiteUiPositionPolicy.IsFinite(p.y))
            {
                _lastWidth = -1;
                _lastHeight = -1;
                Resolve(target);
                return;
            }
            p.x = Mathf.Clamp(p.x, 0f, Mathf.Max(0f, Screen.width - target.rect.width));
            p.y = Mathf.Clamp(p.y, 0f, Mathf.Max(0f, Screen.height - target.rect.height));
            target.anchoredPosition = p;
        }
    }
}
