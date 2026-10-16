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
        private static RectTransform _rt;

        /// <summary>每幀呼叫：視窗開著就讓遮罩跟著視窗位置與大小，關著就隱藏。</summary>
        public static void Sync(bool visible, Rect guiRect)
        {
            if (!visible || guiRect.width <= 0 || guiRect.height <= 0)
            {
                if (_root) _root.SetActive(false);
                return;
            }
            if (!_root) Create();
            _root.SetActive(true);
            // GUI 座標左上為原點，Canvas 左下為原點；Overlay Canvas 無 CanvasScaler 時 1 單位 = 1 像素
            _rt.anchoredPosition = new Vector2(guiRect.x, Screen.height - guiRect.yMax);
            _rt.sizeDelta = guiRect.size;
        }

        private static void Create()
        {
            _root = new GameObject("LoM_InternalMod_ClickBlocker") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(_root);
            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            _root.AddComponent<GraphicRaycaster>();

            var go = new GameObject("Blocker") { hideFlags = HideFlags.HideAndDontSave };
            go.transform.SetParent(_root.transform, false);
            _rt = go.AddComponent<RectTransform>();
            _rt.anchorMin = _rt.anchorMax = _rt.pivot = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0); // 透明但仍是射線目標
            img.raycastTarget = true;
            Plugin.LogInfo("點擊遮罩已建立");
        }
    }
}
