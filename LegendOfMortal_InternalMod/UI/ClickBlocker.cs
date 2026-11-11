using UnityEngine;
using UnityEngine.UI;

namespace LegendOfMortal_InternalMod.UI
{
    /// <summary>
    /// IMGUI 視窗擋不住遊戲的 UGUI／地圖點擊（養成地圖走 EventSystem 的 IPointerClick/EnterHandler）。
    /// 在密卷視窗正下方放一塊透明 Image，掛在排序最高的 Overlay Canvas 上：
    /// EventSystem 射線先打到它，底下的地點就收不到點擊與懸停。
    /// </summary>
    public static class ClickBlocker
    {
        private static GameObject _root;
        private static RectTransform _rt, _rtExtra;

        /// <summary>每幀呼叫：視窗開著就讓遮罩跟著視窗位置與大小，關著就隱藏；extra 為旁邊的詳情視窗（無則 Rect.zero）。</summary>
        public static void Sync(bool visible, Rect guiRect, Rect extra)
        {
            if (!visible || guiRect.width <= 0 || guiRect.height <= 0)
            {
                if (_root) _root.SetActive(false);
                return;
            }
            if (!_root) Create();
            _root.SetActive(true);
            Place(_rt, guiRect);
            Place(_rtExtra, extra);
        }

        private static void Place(RectTransform rt, Rect r)
        {
            // GUI 座標左上為原點，Canvas 左下為原點；Overlay Canvas 無 CanvasScaler 時 1 單位 = 1 像素
            rt.anchoredPosition = new Vector2(r.x, Screen.height - r.yMax);
            rt.sizeDelta = r.size;
        }

        private static void Create()
        {
            _root = new GameObject("LoM_InternalMod_ClickBlocker") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(_root);
            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            _root.AddComponent<GraphicRaycaster>();

            _rt = MakeBlocker("Blocker");
            _rtExtra = MakeBlocker("BlockerExtra");
            Plugin.LogInfo("點擊遮罩已建立");
        }

        private static RectTransform MakeBlocker(string name)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            go.transform.SetParent(_root.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0); // 透明但仍是射線目標
            img.raycastTarget = true;
            return rt;
        }
    }
}
