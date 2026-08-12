using UnityEngine;

namespace ErenshorContracts
{
    internal sealed class ContractLauncher
    {
        private const int WindowId = 0x4552434C;
        internal const float Width = 126f;
        internal const float Height = 34f;

        private bool _open;
        private bool _requestToggle;
        private Texture2D _panelTexture;
        private Texture2D _buttonTexture;
        private Texture2D _buttonHoverTexture;
        private Texture2D _buttonOpenTexture;
        private GUIStyle _windowStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _openButtonStyle;

        internal bool RequestToggle
        {
            get { return _requestToggle; }
        }

        internal Rect Draw(Rect rect, bool open)
        {
            EnsureStyles();
            _open = open;
            _requestToggle = false;

            int previousDepth = GUI.depth;
            Rect result;
            try
            {
                GUI.depth = -55;
                result = GUI.Window(WindowId, rect, DrawContents, GUIContent.none, _windowStyle);
            }
            finally
            {
                GUI.depth = previousDepth;
            }
            return result;
        }

        internal void Dispose()
        {
            DestroyTexture(ref _panelTexture);
            DestroyTexture(ref _buttonTexture);
            DestroyTexture(ref _buttonHoverTexture);
            DestroyTexture(ref _buttonOpenTexture);
            _windowStyle = null;
            _buttonStyle = null;
            _openButtonStyle = null;
        }

        private void DrawContents(int id)
        {
            if (GUI.Button(new Rect(5f, 5f, Width - 10f, Height - 10f), "CONTRACTS", _open ? _openButtonStyle : _buttonStyle))
                _requestToggle = true;
            GUI.DragWindow(new Rect(0f, 0f, Width, Height));
        }

        private void EnsureStyles()
        {
            if (_windowStyle != null) return;

            Color cyanEdge = new Color(0.03f, 0.67f, 0.86f, 0.95f);
            Color softEdge = new Color(0.13f, 0.55f, 0.68f, 0.90f);
            _panelTexture = FramedTexture(new Color(0.015f, 0.09f, 0.125f, 0.74f), cyanEdge);
            _buttonTexture = FramedTexture(new Color(0.035f, 0.17f, 0.22f, 0.88f), softEdge);
            _buttonHoverTexture = FramedTexture(new Color(0.12f, 0.38f, 0.48f, 0.94f), cyanEdge);
            _buttonOpenTexture = FramedTexture(new Color(0.08f, 0.30f, 0.36f, 0.96f), cyanEdge);

            _windowStyle = new GUIStyle(GUI.skin.window);
            _windowStyle.normal.background = _panelTexture;
            _windowStyle.border = new RectOffset(1, 1, 1, 1);
            _windowStyle.padding = new RectOffset(0, 0, 0, 0);

            _buttonStyle = CreateButtonStyle(_buttonTexture, _buttonHoverTexture);
            _openButtonStyle = CreateButtonStyle(_buttonOpenTexture, _buttonHoverTexture);
            _openButtonStyle.fontStyle = FontStyle.Bold;
        }

        private static GUIStyle CreateButtonStyle(Texture2D normal, Texture2D hover)
        {
            GUIStyle style = new GUIStyle(GUI.skin.button);
            style.normal.background = normal;
            style.hover.background = hover;
            style.active.background = hover;
            style.normal.textColor = new Color(0.84f, 0.94f, 1f, 1f);
            style.hover.textColor = Color.white;
            style.active.textColor = Color.white;
            style.fontSize = 11;
            style.border = new RectOffset(1, 1, 1, 1);
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
            Object.Destroy(texture);
            texture = null;
        }
    }
}
