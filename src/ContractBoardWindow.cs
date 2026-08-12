using System;
using System.Collections.Generic;
using UnityEngine;

namespace ErenshorContracts
{
    internal sealed class ContractBoardWindow
    {
        private const int WindowId = 0x45524342;
        private const float HeaderHeight = 31f;

        private List<ContractOffer> _offers;
        private ContractDocument _document;
        private string _zone;
        private Action<string> _accept;
        private Action<string> _abandon;
        private Action<string> _claim;
        private bool _requestClose;
        private Vector2 _scroll;
        private Rect _currentWindowRect;
        private bool _resizing;
        private Vector2 _resizeDelta;

        private Texture2D _panelTexture;
        private Texture2D _buttonTexture;
        private Texture2D _buttonHoverTexture;
        private Texture2D _selectedTexture;
        private Texture2D _dangerTexture;
        private Texture2D _dangerHoverTexture;
        private Texture2D _progressBackTexture;
        private Texture2D _progressFillTexture;
        private GUIStyle _windowStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _sectionStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _hintStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _dangerButtonStyle;
        private GUIStyle _claimButtonStyle;
        private GUIStyle _closeButtonStyle;
        private GUIStyle _boxStyle;
        private GUIStyle _progressBackStyle;
        private GUIStyle _progressFillStyle;
        private GUIStyle _resizeGripStyle;

        internal bool RequestClose
        {
            get { return _requestClose; }
        }

        internal Rect Draw(
            Rect rect,
            string zone,
            List<ContractOffer> offers,
            ContractDocument document,
            Action<string> accept,
            Action<string> abandon,
            Action<string> claim)
        {
            EnsureStyles();
            _zone = zone ?? string.Empty;
            _offers = offers ?? new List<ContractOffer>();
            _document = document;
            _accept = accept;
            _abandon = abandon;
            _claim = claim;
            _requestClose = false;
            _currentWindowRect = rect;
            _resizeDelta = Vector2.zero;

            int previousDepth = GUI.depth;
            Rect result;
            try
            {
                GUI.depth = -60;
                result = GUI.Window(WindowId, rect, DrawWindowContents, GUIContent.none, _windowStyle);
            }
            finally
            {
                GUI.depth = previousDepth;
            }

            if (_resizeDelta != Vector2.zero)
            {
                result.width += _resizeDelta.x;
                result.height += _resizeDelta.y;
            }
            return result;
        }

        internal void Dispose()
        {
            DestroyTexture(ref _panelTexture);
            DestroyTexture(ref _buttonTexture);
            DestroyTexture(ref _buttonHoverTexture);
            DestroyTexture(ref _selectedTexture);
            DestroyTexture(ref _dangerTexture);
            DestroyTexture(ref _dangerHoverTexture);
            DestroyTexture(ref _progressBackTexture);
            DestroyTexture(ref _progressFillTexture);

            _windowStyle = null;
            _titleStyle = null;
            _sectionStyle = null;
            _bodyStyle = null;
            _hintStyle = null;
            _buttonStyle = null;
            _dangerButtonStyle = null;
            _claimButtonStyle = null;
            _closeButtonStyle = null;
            _boxStyle = null;
            _progressBackStyle = null;
            _progressFillStyle = null;
            _resizeGripStyle = null;
        }

        private void DrawWindowContents(int id)
        {
            GUILayout.BeginVertical();
            DrawHeader();
            GUILayout.Space(2f);

            GUILayout.BeginHorizontal();
            GUILayout.Label("LOCAL BOARD", _sectionStyle, GUILayout.Width(86f));
            GUILayout.Label(string.IsNullOrWhiteSpace(_zone) ? "No active zone" : _zone, _bodyStyle, GUILayout.ExpandWidth(true));
            GUILayout.Label(DateTime.Now.ToString("yyyy-MM-dd"), _hintStyle, GUILayout.Width(78f));
            GUILayout.EndHorizontal();

            GUILayout.Space(4f);
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            DrawOffers();
            DrawAdditionalActive();
            GUILayout.EndScrollView();

            GUILayout.Space(3f);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Preview: no native XP/gold/item rewards are granted yet.", _hintStyle, GUILayout.ExpandWidth(true));
            if (_document != null)
                GUILayout.Label("Completed: " + _document.TotalCompleted.ToString(), _hintStyle, GUILayout.Width(92f));
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            DrawResizeGrip();
            GUI.DragWindow(new Rect(0f, 0f, Mathf.Max(0f, _currentWindowRect.width - 42f), HeaderHeight));
        }

        private void DrawHeader()
        {
            GUILayout.BeginHorizontal(GUILayout.Height(HeaderHeight));
            GUILayout.Label("ERENSHOR CONTRACTS", _titleStyle, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("X", _closeButtonStyle, GUILayout.Width(28f), GUILayout.Height(22f)))
                _requestClose = true;
            GUILayout.EndHorizontal();
        }

        private void DrawOffers()
        {
            GUILayout.Label("TODAY'S CONTRACTS", _sectionStyle);
            if (_offers.Count == 0)
            {
                GUILayout.Label("No contract templates are available for this scene yet.", _hintStyle);
                return;
            }

            for (int i = 0; i < _offers.Count; i++)
            {
                ContractOffer offer = _offers[i];
                DrawOffer(offer);
                GUILayout.Space(5f);
            }
        }

        private void DrawOffer(ContractOffer offer)
        {
            if (offer == null || offer.Template == null) return;
            GUILayout.BeginVertical(_boxStyle);

            GUILayout.BeginHorizontal();
            GUILayout.Label(offer.Template.Title, _bodyStyle, GUILayout.ExpandWidth(true));
            string provider = string.Equals(offer.Template.ProviderId, "builtin", StringComparison.OrdinalIgnoreCase)
                ? "LOCAL"
                : offer.Template.ProviderId.ToUpperInvariant();
            GUILayout.Label(provider, _hintStyle, GUILayout.Width(90f));
            GUILayout.EndHorizontal();

            if (!string.IsNullOrWhiteSpace(offer.Template.Description))
                GUILayout.Label(offer.Template.Description, _hintStyle);

            ContractInstance active = offer.Active;
            if (offer.Claimed)
            {
                GUILayout.Label("Completed today", _hintStyle);
            }
            else if (active != null)
            {
                DrawProgress(active);
                GUILayout.BeginHorizontal();
                if (active.IsComplete)
                {
                    if (GUILayout.Button("Claim completion", _claimButtonStyle, GUILayout.Width(124f), GUILayout.Height(25f)))
                        Invoke(_claim, active.OccurrenceId);
                }
                else
                {
                    GUILayout.Label("Active", _hintStyle, GUILayout.Width(52f));
                }

                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Abandon", _dangerButtonStyle, GUILayout.Width(74f), GUILayout.Height(25f)))
                    Invoke(_abandon, active.OccurrenceId);
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Accept", _buttonStyle, GUILayout.Width(86f), GUILayout.Height(25f)))
                    Invoke(_accept, offer.OccurrenceId);
                GUILayout.FlexibleSpace();
                GUILayout.Label("Target: " + offer.Template.Target.ToString(), _hintStyle, GUILayout.Width(82f));
                GUILayout.EndHorizontal();
            }

            if (!string.IsNullOrWhiteSpace(offer.Template.RewardText))
                GUILayout.Label(offer.Template.RewardText, _hintStyle);

            GUILayout.EndVertical();
        }

        private void DrawProgress(ContractInstance active)
        {
            string text = ContractCore.ProgressText(active);
            GUILayout.BeginHorizontal();
            GUILayout.Label(text, _hintStyle, GUILayout.Width(88f));

            Rect outer = GUILayoutUtility.GetRect(80f, 15f, GUILayout.ExpandWidth(true));
            GUI.Box(outer, GUIContent.none, _progressBackStyle);
            float ratio = active.Target <= 0 ? 0f : Mathf.Clamp01((float)active.Progress / (float)active.Target);
            Rect fill = new Rect(outer.x + 1f, outer.y + 1f, Mathf.Max(0f, (outer.width - 2f) * ratio), Mathf.Max(0f, outer.height - 2f));
            if (fill.width > 0f) GUI.Box(fill, GUIContent.none, _progressFillStyle);
            GUILayout.EndHorizontal();
        }

        private void DrawAdditionalActive()
        {
            if (_document == null || _document.Active.Count == 0) return;

            List<ContractInstance> additional = new List<ContractInstance>();
            for (int i = 0; i < _document.Active.Count; i++)
            {
                ContractInstance active = _document.Active[i];
                bool represented = false;
                for (int j = 0; j < _offers.Count; j++)
                {
                    if (string.Equals(_offers[j].OccurrenceId, active.OccurrenceId, StringComparison.OrdinalIgnoreCase))
                    {
                        represented = true;
                        break;
                    }
                }
                if (!represented) additional.Add(active);
            }
            if (additional.Count == 0) return;

            GUILayout.Space(8f);
            GUILayout.Label("OTHER ACTIVE CONTRACTS", _sectionStyle);
            for (int i = 0; i < additional.Count; i++)
            {
                ContractInstance active = additional[i];
                GUILayout.BeginVertical(_boxStyle);
                GUILayout.BeginHorizontal();
                GUILayout.Label(active.Title, _bodyStyle, GUILayout.ExpandWidth(true));
                GUILayout.Label(active.OriginZone, _hintStyle, GUILayout.Width(120f));
                GUILayout.EndHorizontal();
                DrawProgress(active);
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (active.IsComplete)
                {
                    if (GUILayout.Button("Claim", _claimButtonStyle, GUILayout.Width(68f), GUILayout.Height(24f)))
                        Invoke(_claim, active.OccurrenceId);
                }
                if (GUILayout.Button("Abandon", _dangerButtonStyle, GUILayout.Width(74f), GUILayout.Height(24f)))
                    Invoke(_abandon, active.OccurrenceId);
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
                GUILayout.Space(4f);
            }
        }

        private void DrawResizeGrip()
        {
            Rect grip = new Rect(Mathf.Max(0f, _currentWindowRect.width - 22f), Mathf.Max(0f, _currentWindowRect.height - 20f), 18f, 16f);
            GUI.Label(grip, "//", _resizeGripStyle);

            Event current = Event.current;
            if (current == null) return;

            if (!_resizing && current.type == EventType.MouseDown && current.button == 0 && grip.Contains(current.mousePosition))
            {
                _resizing = true;
                current.Use();
                return;
            }

            if (_resizing && current.type == EventType.MouseDrag && current.button == 0)
            {
                _resizeDelta += current.delta;
                current.Use();
                return;
            }

            if (_resizing && current.type == EventType.MouseUp && current.button == 0)
            {
                _resizing = false;
                current.Use();
            }
        }

        private static void Invoke(Action<string> action, string value)
        {
            if (action != null) action(value);
        }

        private void EnsureStyles()
        {
            if (_windowStyle != null) return;

            Color cyanEdge = new Color(0.03f, 0.67f, 0.86f, 0.95f);
            Color softEdge = new Color(0.13f, 0.55f, 0.68f, 0.90f);
            _panelTexture = FramedTexture(new Color(0.015f, 0.09f, 0.125f, 0.92f), cyanEdge);
            _buttonTexture = FramedTexture(new Color(0.035f, 0.17f, 0.22f, 0.90f), softEdge);
            _buttonHoverTexture = FramedTexture(new Color(0.12f, 0.38f, 0.48f, 0.95f), cyanEdge);
            _selectedTexture = FramedTexture(new Color(0.08f, 0.30f, 0.36f, 0.96f), cyanEdge);
            _dangerTexture = FramedTexture(new Color(0.19f, 0.15f, 0.09f, 0.90f), new Color(0.65f, 0.49f, 0.27f, 0.92f));
            _dangerHoverTexture = FramedTexture(new Color(0.34f, 0.23f, 0.10f, 0.96f), new Color(0.86f, 0.63f, 0.30f, 0.98f));
            _progressBackTexture = FramedTexture(new Color(0.018f, 0.055f, 0.068f, 0.96f), softEdge);
            _progressFillTexture = FramedTexture(new Color(0.08f, 0.30f, 0.36f, 0.98f), cyanEdge);

            _windowStyle = new GUIStyle(GUI.skin.window);
            _windowStyle.normal.background = _panelTexture;
            _windowStyle.border = new RectOffset(1, 1, 1, 1);
            _windowStyle.padding = new RectOffset(12, 12, 8, 10);

            _titleStyle = new GUIStyle(GUI.skin.label);
            _titleStyle.fontSize = 15;
            _titleStyle.fontStyle = FontStyle.Bold;
            _titleStyle.normal.textColor = new Color(0.56f, 0.88f, 1f, 1f);

            _sectionStyle = new GUIStyle(GUI.skin.label);
            _sectionStyle.fontSize = 11;
            _sectionStyle.fontStyle = FontStyle.Bold;
            _sectionStyle.normal.textColor = new Color(0.56f, 0.78f, 0.88f, 1f);

            _bodyStyle = new GUIStyle(GUI.skin.label);
            _bodyStyle.fontSize = 12;
            _bodyStyle.fontStyle = FontStyle.Bold;
            _bodyStyle.wordWrap = true;
            _bodyStyle.normal.textColor = new Color(0.92f, 0.94f, 0.92f, 1f);

            _hintStyle = new GUIStyle(GUI.skin.label);
            _hintStyle.fontSize = 10;
            _hintStyle.wordWrap = true;
            _hintStyle.normal.textColor = new Color(0.63f, 0.76f, 0.80f, 1f);

            _buttonStyle = CreateButtonStyle(_buttonTexture, _buttonHoverTexture, Color.white);
            _dangerButtonStyle = CreateButtonStyle(_dangerTexture, _dangerHoverTexture, new Color(1f, 0.94f, 0.74f, 1f));
            _claimButtonStyle = CreateButtonStyle(_selectedTexture, _buttonHoverTexture, new Color(0.88f, 1f, 0.98f, 1f));
            _claimButtonStyle.fontStyle = FontStyle.Bold;
            _closeButtonStyle = CreateButtonStyle(_buttonTexture, _buttonHoverTexture, new Color(0.84f, 0.94f, 1f, 1f));

            _boxStyle = new GUIStyle(GUI.skin.box);
            _boxStyle.normal.background = _progressBackTexture;
            _boxStyle.border = new RectOffset(1, 1, 1, 1);
            _boxStyle.padding = new RectOffset(9, 9, 7, 7);

            _progressBackStyle = new GUIStyle(GUI.skin.box);
            _progressBackStyle.normal.background = _progressBackTexture;
            _progressBackStyle.border = new RectOffset(1, 1, 1, 1);
            _progressFillStyle = new GUIStyle(GUI.skin.box);
            _progressFillStyle.normal.background = _progressFillTexture;
            _progressFillStyle.border = new RectOffset(1, 1, 1, 1);

            _resizeGripStyle = new GUIStyle(GUI.skin.label);
            _resizeGripStyle.fontSize = 11;
            _resizeGripStyle.alignment = TextAnchor.MiddleCenter;
            _resizeGripStyle.normal.textColor = new Color(0.56f, 0.88f, 1f, 0.90f);
        }

        private static GUIStyle CreateButtonStyle(Texture2D normal, Texture2D hover, Color text)
        {
            GUIStyle style = new GUIStyle(GUI.skin.button);
            style.normal.background = normal;
            style.hover.background = hover;
            style.active.background = hover;
            style.normal.textColor = text;
            style.hover.textColor = Color.white;
            style.active.textColor = Color.white;
            style.margin = new RectOffset(2, 2, 2, 2);
            style.border = new RectOffset(1, 1, 1, 1);
            style.padding = new RectOffset(6, 6, 2, 2);
            return style;
        }

        private static Texture2D FramedTexture(Color center, Color edge)
        {
            Texture2D texture = new Texture2D(3, 3, TextureFormat.RGBA32, false);
            for (int y = 0; y < 3; y++)
                for (int x = 0; x < 3; x++)
                    texture.SetPixel(x, y, x == 0 || x == 2 || y == 0 || y == 2 ? edge : center);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Point;
            texture.Apply(false, true);
            return texture;
        }

        private static void DestroyTexture(ref Texture2D texture)
        {
            if (texture == null) return;
            UnityEngine.Object.Destroy(texture);
            texture = null;
        }
    }
}
