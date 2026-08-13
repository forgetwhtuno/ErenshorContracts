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
        private GUIStyle _gripStyle;

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
            _gripStyle = null;
        }

        private void DrawContents(int id)
        {
            // Matches ErenshorJournal's JournalLauncher interaction model: a narrow grip strip
            // owns dragging and a separate pure-click button area fills the rest of the launcher.
            // GUI.DragWindow's rect must never overlap the button rect below -- if it does, a
            // click that lands inside both can be consumed as a drag-start instead of being
            // delivered to the button, which was the previous bug (launcher couldn't be dragged
            // cleanly and clicks didn't reliably open the board).
            GUI.Label(new Rect(3f, 5f, 14f, 24f), "||", _gripStyle);
            if (GUI.Button(new Rect(18f, 4f, Width - 22f, 26f), "CONTRACTS", _open ? _openButtonStyle : _buttonStyle))
                _requestToggle = true;
            GUI.DragWindow(new Rect(0f, 0f, 18f, Height));
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

            _gripStyle = new GUIStyle(GUI.skin.label);
            _gripStyle.fontSize = 10;
            _gripStyle.fontStyle = FontStyle.Bold;
            _gripStyle.alignment = TextAnchor.MiddleCenter;
            _gripStyle.normal.textColor = new Color(0.56f, 0.88f, 1f, 0.95f);
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
