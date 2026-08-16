using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ErenshorContracts
{
    internal sealed class ContractLauncher
    {
        internal const float Width = 130f;
        internal const float Height = 30f;

        private GameObject _root;
        private RectTransform _panel;
        private TextMeshProUGUI _label;
        private RetainedPosition _position;
        private Action _toggle;

        internal void Initialize(float storedX, float storedY, Action<float, float> persist, Action toggle)
        {
            Dispose();
            _toggle = toggle;
            _root = RetainedUiKit.CreateCanvas("ErenshorContractLauncherCanvas", 510);
            RectTransform canvas = _root.GetComponent<RectTransform>();
            _panel = RetainedUiKit.CreateRect("ContractLauncher", canvas);
            RetainedUiKit.AnchorBottomLeft(_panel, 0f, 0f, Width, Height);
            RetainedUiKit.AddImage(_panel, RetainedUiKit.Panel);

            RectTransform grip = RetainedUiKit.CreateRect("DragGrip", _panel);
            grip.anchorMin = Vector2.zero;
            grip.anchorMax = new Vector2(0f, 1f);
            grip.pivot = Vector2.zero;
            grip.anchoredPosition = Vector2.zero;
            grip.sizeDelta = new Vector2(20f, 0f);
            RetainedUiKit.AddImage(grip, RetainedUiKit.Header);
            TextMeshProUGUI diamond = RetainedUiKit.AddLabel("GripLabel", grip, "◇", 14f, FontStyles.Bold, TextAlignmentOptions.Center);
            RetainedUiKit.Stretch(diamond.rectTransform, 0f, 0f, 0f, 0f);

            Button button = RetainedUiKit.AddButton("OpenContracts", _panel, "CONTRACTS", delegate { if (_toggle != null) _toggle(); }, Width - 20f, Height, false);
            RectTransform br = button.GetComponent<RectTransform>();
            br.anchorMin = Vector2.zero;
            br.anchorMax = Vector2.zero;
            br.pivot = Vector2.zero;
            br.anchoredPosition = new Vector2(20f, 0f);
            br.sizeDelta = new Vector2(Width - 20f, Height);
            LayoutElement le = br.GetComponent<LayoutElement>();
            if (le != null) UnityEngine.Object.DestroyImmediate(le);
            _label = button.GetComponentInChildren<TextMeshProUGUI>();

            RetainedUiKit.AddFrame(_panel, 1f);

            _position = new RetainedPosition(storedX, storedY, 0.86f, 0.82f, persist);
            SuiteDragHandler drag = grip.gameObject.AddComponent<SuiteDragHandler>();
            drag.Target = _panel;
            drag.OnDragCompleted = delegate { if (_position != null) _position.DragCompleted(_panel); };
            _position.Resolve(_panel);
            _root.SetActive(false);
        }

        internal void Tick(bool visible, bool open)
        {
            if (_root == null) return;
            if (_root.activeSelf != visible) _root.SetActive(visible);
            if (!visible) return;
            if (_position != null) _position.Resolve(_panel);
            if (_label != null) _label.text = open ? "CONTRACTS •" : "CONTRACTS";
        }

        internal void ResetPosition()
        {
            if (_position != null) _position.Reset(_panel);
        }

        internal void Dispose()
        {
            SuiteDragHandler.ForceReleaseIfOwned();
            RetainedUiKit.DestroyRoot(ref _root);
            _panel = null;
            _label = null;
            _position = null;
            _toggle = null;
        }
    }
}
