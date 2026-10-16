using System;
using System.Linq;
using UnityEngine;

namespace LegendOfMortal_InternalMod.UI
{
    /// <summary>
    /// 仿原版「狀態」卷軸：米色紙底＋墨色字＋朱紅強調，字體優先借用遊戲內的思源宋體。
    /// 整套樣式包成一個 GUISkin，OnGUI 開頭指定 GUI.skin 即可，按鈕/輸入框/捲軸都不用逐一傳樣式。
    /// 以 1920x1080 為基準依解析度縮放。
    /// </summary>
    public static class GameStyleUI
    {
        public static readonly Color Paper = Hex(0xEFE8DA);
        public static readonly Color PaperDark = Hex(0xE2D8C5);
        public static readonly Color Ink = Hex(0x2A211B);
        public static readonly Color InkSoft = Hex(0x6B5D50);
        public static readonly Color Vermilion = Hex(0xA8322A);
        public static readonly Color Frame = Hex(0x5A4632);

        public static GUISkin Skin;
        public static GUIStyle Title, Section, Label, Entry, Desc, Tab, Close, Notice, CheckLabel;
        /// <summary>與按鈕/輸入框同列的文字：同高、垂直置中</summary>
        public static GUIStyle Row, RowEntry, RowDesc;
        public static Texture2D LineTex, CheckOff, CheckOn, CheckHover;
        public static float Scale = 1f;

        private static GUISkin _default;
        private static Font _font;
        private static float _nextFontScan;
        private static int _builtW, _builtH;
        private static bool _builtWithFont;

        /// <summary>每次 OnGUI 呼叫：必要時（首次、解析度改變、字體剛找到）重建樣式並套用 skin。</summary>
        public static void Apply()
        {
            if (_font == null && Time.realtimeSinceStartup >= _nextFontScan)
            {
                _nextFontScan = Time.realtimeSinceStartup + 5f;
                _font = FindFont();
            }
            if (Skin == null || _builtW != Screen.width || _builtH != Screen.height || _builtWithFont != (_font != null))
                Build();
            GUI.skin = Skin;
        }

        public static int Px(float v) => Mathf.RoundToInt(v * Scale);

        private static void Build()
        {
            _builtW = Screen.width; _builtH = Screen.height; _builtWithFont = _font != null;
            Scale = Mathf.Clamp(Mathf.Min(Screen.width / 1920f, Screen.height / 1080f), 0.75f, 2f);

            // 以第一次取得的內建 skin 為底；之後 GUI.skin 可能已是我們自己的 skin
            if (_default == null) _default = GUI.skin;
            var d = _default;
            if (Skin != null) UnityEngine.Object.Destroy(Skin);
            Skin = UnityEngine.Object.Instantiate(d);
            Skin.hideFlags = HideFlags.HideAndDontSave;
            Skin.font = _font;

            var paperFramed = Framed(Paper, Frame, 2, Hex(0xC9B48A));
            var btn = Framed(PaperDark, Hex(0x8C7558), 1);
            var btnHover = Framed(Hex(0xD8CBB2), Hex(0x6E5A43), 1);
            var btnActive = Framed(Hex(0xC9B795), Hex(0x5A4632), 1);
            var field = Framed(Hex(0xFBF8F1), Hex(0x8C7558), 1);
            var fieldFocus = Framed(Hex(0xFFFFFF), Vermilion, 2);
            var tabOn = Framed(Vermilion, Hex(0x7A221C), 1);

            Skin.window = new GUIStyle(d.window)
            {
                fontSize = Px(26),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperCenter,
                border = new RectOffset(6, 6, 6, 6),
                padding = new RectOffset(Px(24), Px(24), Px(64), Px(18)),
                contentOffset = new Vector2(0, -Px(50)),
            };
            SetAll(Skin.window, paperFramed, Vermilion);

            Skin.label = new GUIStyle(d.label) { fontSize = Px(20), richText = true, wordWrap = true };
            SetAll(Skin.label, null, Ink);

            Skin.button = new GUIStyle(d.button)
            {
                fontSize = Px(19),
                border = new RectOffset(3, 3, 3, 3),
                padding = new RectOffset(Px(14), Px(14), Px(5), Px(5)),
                fixedHeight = Px(38),
                alignment = TextAnchor.MiddleCenter,
            };
            SetAll(Skin.button, btn, Ink);
            Skin.button.hover.background = Skin.button.onHover.background = btnHover;
            Skin.button.active.background = Skin.button.onActive.background = btnActive;

            Skin.textField = new GUIStyle(d.textField)
            {
                fontSize = Px(20),
                border = new RectOffset(3, 3, 3, 3),
                padding = new RectOffset(Px(10), Px(10), Px(4), Px(4)),
                fixedHeight = Px(38),
                alignment = TextAnchor.MiddleLeft,
            };
            SetAll(Skin.textField, field, Ink);
            Skin.textField.focused.background = Skin.textField.onFocused.background = fieldFocus;
            Skin.settings.cursorColor = Ink;
            Skin.settings.selectionColor = new Color(Vermilion.r, Vermilion.g, Vermilion.b, 0.3f);

            Skin.box = new GUIStyle(d.box) { border = new RectOffset(3, 3, 3, 3), padding = new RectOffset(Px(14), Px(14), Px(10), Px(10)) };
            SetAll(Skin.box, Framed(Hex(0xE7DECD), Hex(0xBFAE90), 1), Ink);

            Skin.scrollView = new GUIStyle(d.scrollView);
            SetAll(Skin.scrollView, null, Ink);
            Skin.verticalScrollbar = new GUIStyle(d.verticalScrollbar)
            {
                fixedWidth = Px(10), border = new RectOffset(0, 0, 0, 0), margin = new RectOffset(Px(8), 0, 0, 0), padding = new RectOffset(0, 0, 0, 0),
            };
            SetAll(Skin.verticalScrollbar, Solid(Hex(0xDCD1BC)), Ink);
            Skin.verticalScrollbarThumb = new GUIStyle(d.verticalScrollbarThumb) { fixedWidth = Px(10), border = new RectOffset(0, 0, 0, 0) };
            SetAll(Skin.verticalScrollbarThumb, Solid(Hex(0x9C8668)), Ink);

            Title = Make(Skin.label, 24, Vermilion, FontStyle.Bold, TextAnchor.MiddleCenter, false);
            Section = Make(Skin.label, 22, Vermilion, FontStyle.Bold, TextAnchor.MiddleLeft, false);
            Entry = Make(Skin.label, 20, Ink, FontStyle.Bold, TextAnchor.UpperLeft, true);
            Desc = Make(Skin.label, 17, InkSoft, FontStyle.Normal, TextAnchor.UpperLeft, true);
            Label = Skin.label;

            CheckLabel = Make(Skin.label, 20, Ink, FontStyle.Normal, TextAnchor.MiddleLeft, false);
            CheckLabel.padding = new RectOffset(Px(36), 0, 0, 0);
            CheckLabel.fixedHeight = Px(38);

            Row = Make(Skin.label, 20, Ink, FontStyle.Normal, TextAnchor.MiddleLeft, false);
            Row.fixedHeight = Px(38);
            RowEntry = new GUIStyle(Row) { fontStyle = FontStyle.Bold };
            RowDesc = new GUIStyle(Row) { fontSize = Px(17) };
            SetAll(RowDesc, null, InkSoft);

            Tab = new GUIStyle(Skin.button) { fontSize = Px(20), fixedHeight = Px(42) };
            Tab.onNormal.background = Tab.onHover.background = Tab.onActive.background = tabOn;
            Tab.onNormal.textColor = Tab.onHover.textColor = Tab.onActive.textColor = Paper;

            Close = new GUIStyle(Skin.button) { fontSize = Px(22), fixedHeight = Px(34), fixedWidth = Px(34), padding = new RectOffset(0, 0, 0, Px(2)) };

            Notice = new GUIStyle(Skin.box)
            {
                fontSize = Px(20), alignment = TextAnchor.MiddleLeft, richText = true,
                padding = new RectOffset(Px(16), Px(16), Px(8), Px(8)),
            };

            LineTex = Solid(new Color(Vermilion.r, Vermilion.g, Vermilion.b, 0.45f));
            int cb = Px(24);
            CheckOff = CheckBox(cb, Hex(0xFBF8F1), Hex(0x8C7558), false);
            CheckHover = CheckBox(cb, Hex(0xFFFFFF), Hex(0x5A4632), false);
            CheckOn = CheckBox(cb, Hex(0xFBF8F1), Vermilion, true);
        }

        // ---- 字體：思源宋體（原版介面字體）> 其他遊戲內動態字體 > 系統 CJK 字體 ----
        private static readonly string[] OsFonts = { "Source Han Serif TC", "Noto Serif CJK TC", "Microsoft JhengHei", "PMingLiU", "Noto Sans CJK TC" };

        private static Font FindFont()
        {
            try
            {
                var fonts = Resources.FindObjectsOfTypeAll<Font>().Where(f => f != null && f.dynamic).ToList();
                var tmpType = Type.GetType("TMPro.TMP_FontAsset, Unity.TextMeshPro");
                var srcProp = tmpType?.GetProperty("sourceFontFile");
                if (srcProp != null)
                    foreach (var fa in Resources.FindObjectsOfTypeAll(tmpType))
                        if (srcProp.GetValue(fa) is Font f && f.dynamic && !fonts.Contains(f)) fonts.Add(f);

                var pick = fonts.FirstOrDefault(f => f.name.Replace(" ", "").IndexOf("SourceHanSerif", StringComparison.OrdinalIgnoreCase) >= 0);
                if (pick != null)
                {
                    Plugin.LogInfo($"介面字體：{pick.name}（遊戲內建）");
                    return pick;
                }
                if (fonts.Count > 0)
                    Plugin.LogInfo("遊戲內動態字體（未選用）：" + string.Join("、", fonts.Select(f => f.name)));

                var os = Font.CreateDynamicFontFromOSFont(OsFonts, 20);
                if (os != null)
                {
                    os.hideFlags = HideFlags.HideAndDontSave;
                    Plugin.LogInfo("介面字體：系統字體 " + string.Join("/", os.fontNames ?? new string[0]));
                    return os;
                }
            }
            catch (Exception ex) { Plugin.LogError("尋找字體失敗：" + ex.Message); }
            return null;
        }

        // ---- 工具 ----
        private static GUIStyle Make(GUIStyle baseStyle, int size, Color color, FontStyle fs, TextAnchor align, bool wrap)
        {
            var s = new GUIStyle(baseStyle) { fontSize = Px(size), fontStyle = fs, alignment = align, wordWrap = wrap, richText = true };
            SetAll(s, null, color);
            return s;
        }

        private static void SetAll(GUIStyle s, Texture2D bg, Color text)
        {
            foreach (var st in new[] { s.normal, s.hover, s.active, s.focused, s.onNormal, s.onHover, s.onActive, s.onFocused })
            {
                st.background = bg;
                st.textColor = text;
            }
        }

        private static Color Hex(int rgb) => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);

        private static Texture2D NewTex(int w, int h)
        {
            return new Texture2D(w, h, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        }

        private static Texture2D Solid(Color c)
        {
            var t = NewTex(2, 2);
            t.SetPixels(new[] { c, c, c, c });
            t.Apply();
            return t;
        }

        /// <summary>9-slice 用：外框 border 像素，可選一圈內描線（燙金細線）。</summary>
        private static Texture2D Framed(Color fill, Color border, int bw, Color? inner = null)
        {
            const int n = 12;
            var t = NewTex(n, n);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int d = Math.Min(Math.Min(x, y), Math.Min(n - 1 - x, n - 1 - y));
                    t.SetPixel(x, y, d < bw ? border : (inner.HasValue && d == bw + 1 ? inner.Value : fill));
                }
            t.Apply();
            return t;
        }

        private static Texture2D CheckBox(int size, Color fill, Color border, bool on)
        {
            var t = NewTex(size, size);
            int bw = Math.Max(2, size / 12), pad = size / 4;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int d = Math.Min(Math.Min(x, y), Math.Min(size - 1 - x, size - 1 - y));
                    t.SetPixel(x, y, d < bw ? border : (on && d >= pad ? Vermilion : fill));
                }
            t.Apply();
            return t;
        }
    }
}
