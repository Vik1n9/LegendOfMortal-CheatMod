using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfMortal_InternalMod.Core;
using Mortal.Core;
using UnityEngine;
using Cfg = LegendOfMortal_InternalMod.Config.ModConfig;
using S = LegendOfMortal_InternalMod.UI.GameStyleUI;

namespace LegendOfMortal_InternalMod.UI
{
    /// <summary>
    /// 主視窗「唐門密卷 Lite」：F1 開關，拖曳標題列移動。
    /// Lite 版分頁：事件攻略（路線地圖・本旬事件・結局圖鑑）／事件任務（只列出，不指定）／選項提示
    /// </summary>
    public static class CheatWindow
    {
        public static bool Visible = false;

        /// <summary>視窗目前位置（GUI 座標，左上原點），給 ClickBlocker 對齊用。</summary>
        public static Rect WindowRect => rect;

        /// <summary>事件詳情視窗位置（未顯示時為 Rect.zero），給 ClickBlocker 對齊用。</summary>
        public static Rect PopupRect => popupRect;
        private static Rect popupRect;

        private const int WindowId = 2077, PopupId = 2078;
        private static bool mapPopup;
        private const float BaseW = 845f, BaseScrollH = 600f;   // 與完整版同寬，路線地圖版面不變
        private static Rect rect;
        private static float rectScale;
        private static Vector2 scroll;
        private static int tab;
        private static readonly string[] tabs = { "事件攻略", "事件任務", "選項提示" };

        public static void Draw(Plugin plugin)
        {
            if (!Visible) return;
            if (rectScale != S.Scale)
            {
                rectScale = S.Scale;
                // 放在左上 HUD（貢獻度／日期，約到 y=170）下方
                rect = new Rect(S.Px(60), S.Px(185), S.Px(BaseW), 0);
            }
            rect = GUILayout.Window(WindowId, rect, id => DrawMain(plugin), "唐門密卷 Lite", GUILayout.Width(S.Px(BaseW)));

            // 路線地圖的事件詳情視窗；是否顯示只在 Layout 決定，同一幀各事件一致
            if (Event.current.type == EventType.Layout)
                mapPopup = tab == 0 && guideSub == 0 && guideItems != null && guideError == null && GuideMapView.PopupOpen;
            popupRect = mapPopup ? GuideMapView.DrawPopup(PopupId, rect, guideNow, EventGuide.PlayerRoutes(guideNow)) : Rect.zero;
        }

        private static void DrawMain(Plugin plugin)
        {
            if (GUI.Button(new Rect(rect.width - S.Px(48), S.Px(14), S.Px(34), S.Px(34)), "×", S.Close)) Visible = false;

            tab = GUILayout.Toolbar(tab, tabs, S.Tab);
            GUILayout.Space(S.Px(10));

            scroll = GUILayout.BeginScrollView(scroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.Height(S.Px(BaseScrollH)));
            // 固定內容寬度＝視窗內寬扣掉捲軸，避免右側（例：即將開始的日期）被捲軸蓋住或裁掉
            float contentW = S.Px(BaseW) - GUI.skin.window.padding.horizontal - GUI.skin.verticalScrollbar.fixedWidth - S.Px(14);
            GUILayout.BeginVertical(GUILayout.Width(contentW));
            try
            {
                switch (tab)
                {
                    case 0: DrawGuide(); break;
                    case 1: DrawMissions(plugin); break;
                    default: DrawOptionInfo(); break;
                }
            }
            finally { GUILayout.EndVertical(); GUILayout.EndScrollView(); }

            GUILayout.Space(S.Px(6));
            GUILayout.Label($"{Cfg.MainMenuKey.Value} 開關視窗　·　拖曳標題列可移動　·　《唐門密卷》Lite v1.3.2 Beta", S.Desc);
            GUI.DragWindow(new Rect(0, 0, rect.width, S.Px(60)));
        }

        // ---- 事件攻略 ----
        private static readonly (string Name, GameStatType Type)[] GuideStats =
        {
            ("心相", GameStatType.心理衛生), ("修養", GameStatType.修養), ("性情", GameStatType.性情), ("處世", GameStatType.處世),
            ("道德", GameStatType.道德), ("學問", GameStatType.學問), ("名聲", GameStatType.門派名聲), ("門派規模", GameStatType.門派規模),
            ("貢獻度", GameStatType.個人貢獻度), ("行動次數", GameStatType.行動次數),
        };
        private static readonly string[] guideSubs = { "路線地圖", "本旬事件", "結局圖鑑" };
        private static int guideSub;
        private static bool guideUpcoming = true;
        private static List<GuideItem> guideItems;
        private static int guideNow;
        private static string guideRoute, guideError;
        private static float guideNextRefresh;

        // OnGUI 每幀呼叫，判定結果每秒重算一次即可；只在 Layout 事件更新，避免同一幀版面前後不一致
        private static void RefreshGuide()
        {
            if (Event.current.type != EventType.Layout || Time.unscaledTime < guideNextRefresh) return;
            guideNextRefresh = Time.unscaledTime + 1f;
            try
            {
                var mgr = PlayerStatManagerData.Instance;
                if (mgr == null || MissionManagerData.Instance == null) { guideError = "請先載入存檔"; return; }
                guideNow = mgr.GameTime.ConvertToRounds();
                guideRoute = EventGuide.RouteText(EventGuide.PlayerRoutes(guideNow));
                guideItems = EventGuide.Evaluate(guideNow, guideUpcoming);
                guideError = null;
            }
            catch (Exception ex)
            {
                guideError = "請先載入存檔";
                Plugin.LogError("事件攻略: " + ex.Message);
            }
        }

        /// 事件攻略／事件任務的資料來源標示（CC BY-NC-SA 4.0 要求標示來源與授權；僅顯示文字，不提供連結）
        private static void WikiCredit()
        {
            GUILayout.Label("攻略資料依CC BY-NC-SA 4.0授權使用", S.Desc);
            GUILayout.Label("活俠傳WIKI https://smilin.net/LoM-wiki/", S.Desc);
        }

        private static void DrawGuide()
        {
            RefreshGuide();
            if (guideItems == null || guideError != null)
            {
                GUILayout.Space(S.Px(40));
                GUILayout.Label(guideError ?? "讀取中…", S.Title);
                return;
            }

            WikiCredit();
            Section("目前狀況");
            GUILayout.Label($"<b>{EventGuide.FormatRound(guideNow)}</b>　·　路線：{guideRoute}", S.Entry);
            const int cols = 5;
            for (int i = 0; i < GuideStats.Length; i += cols)
            {
                GUILayout.BeginHorizontal();
                for (int c = 0; c < cols && i + c < GuideStats.Length; c++)
                {
                    // 每欄標籤寬度取該欄最長字數，上下列對齊、整列又不超出視窗
                    int len = 0;
                    for (int j = c; j < GuideStats.Length; j += cols) len = Math.Max(len, GuideStats[j].Name.Length);
                    var (name, type) = GuideStats[i + c];
                    GUILayout.Label(name, S.Row, GUILayout.Width(S.Px(20 * len + 12)));
                    GUILayout.Label(GetStat(type).ToString(), S.RowEntry, GUILayout.Width(S.Px(48)));
                    GUILayout.Space(S.Px(16));
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(S.Px(8));
            guideSub = GUILayout.Toolbar(guideSub, guideSubs, S.Tab);
            GUILayout.Space(S.Px(6));
            var routes = EventGuide.PlayerRoutes(guideNow);
            switch (guideSub)
            {
                case 0: GuideMapView.DrawMap(guideNow, routes); break;
                case 1: DrawGuideEvents(); break;
                default: GuideMapView.DrawEndings(guideNow, routes); break;
            }
            if (GuideMapView.ScrollRequest is float y) { scroll.y = y; GuideMapView.ScrollRequest = null; }
        }

        private static void DrawGuideEvents()
        {
            bool up = Check(guideUpcoming, $"顯示即將開始（未來 {EventGuide.Lookahead} 旬內）");
            if (up != guideUpcoming) { guideUpcoming = up; guideNextRefresh = 0; }
            GUILayout.Label("事件與條件依活俠傳 wiki 整理；灰字「另需」為程式無法判定的條件，請自行確認。", S.Desc);

            GuideSection("本旬可觸發", GuideState.Ready);
            GuideSection("條件不足", GuideState.Lacking);
            if (guideUpcoming) GuideSection("即將開始", GuideState.Upcoming);
        }

        private static void GuideSection(string title, GuideState state)
        {
            Section(title);
            var items = guideItems.Where(i => i.State == state).ToList();
            if (items.Count == 0) { GUILayout.Label("目前沒有對應事件", S.Desc); return; }
            foreach (var it in items)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.BeginHorizontal();
                GUILayout.Label(it.Event.Name, S.Entry);
                GUILayout.FlexibleSpace();
                if (state == GuideState.Upcoming) GUILayout.Label(EventGuide.FormatRound(it.Event.Start), S.Desc);
                GUILayout.EndHorizontal();
                if (it.Missing.Count > 0) GUILayout.Label(OnPaper(RichText.Red("缺：" + string.Join("、", it.Missing))), S.Desc);
                if (it.Met.Count > 0) GUILayout.Label(OnPaper(RichText.Green("已達：" + string.Join("、", it.Met))), S.Desc);
                if (it.Notes.Count > 0) GUILayout.Label(OnPaper(RichText.Gray("另需：" + string.Join("、", it.Notes))), S.Desc);
                GUILayout.EndVertical();
                GUILayout.Space(S.Px(6));
            }
        }

        private static void DrawMissions(Plugin plugin)
        {
            WikiCredit();
            RefreshReady(plugin);
            if (plugin.Events.Count + plugin.Missions.Count == 0)
            {
                GUILayout.Space(S.Px(40));
                GUILayout.Label("目前沒有資料", S.Title);
                GUILayout.Label("走到養成地點或進入劇情後，這裡會自動列出可觸發的事件與支線任務。", new GUIStyle(S.Desc) { alignment = TextAnchor.MiddleCenter });
                return;
            }
            GUILayout.Label("底色依目前條件即時判定：綠＝符合條件會觸發，紅＝條件不足。", S.Desc);
            if (plugin.Events.Count > 0)
            {
                Section("養成事件");
                GUILayout.Label("目前地點可能觸發的事件：會觸發的排在前面，其餘依機率由高到低排列。", S.Desc);
                foreach (var e in readyEvents) EntryCard(e.Key, e.Desc, Ready(e.Ev));
            }
            if (plugin.Missions.Count > 0)
            {
                Section("支線任務");
                GUILayout.Label("進行中的支線任務與觸發條件：會觸發的排在前面。", S.Desc);
                foreach (var m in readyMissions) EntryCard(m.Key, m.Desc, Ready(m.M));
            }
        }

        // 會觸發與否每秒重算一次，只在 Layout 更新，避免同一幀版面前後不一致
        private static readonly Dictionary<object, bool> readyCache = new Dictionary<object, bool>();
        private static float readyNextRefresh;
        private static int readyListCount = -1;
        // 依判定結果排序後的清單（會觸發的在前；OrderBy 為穩定排序，同組維持原本順序）
        private static List<(Mortal.Story.PositionEventData Ev, string Key, string Desc)> readyEvents = new();
        private static List<(MissionCheckData M, string Key, string Desc)> readyMissions = new();

        private static void RefreshReady(Plugin plugin)
        {
            if (Event.current.type != EventType.Layout) return;
            // 清單換了（走到新地點、進入劇情）就立刻重算，不必等到下一秒
            // （比對清單筆數而非快取筆數：同一事件可能出現在多個地點，快取筆數會比較少）
            int count = plugin.Events.Count + plugin.Missions.Count;
            if (count == readyListCount && Time.unscaledTime < readyNextRefresh) return;
            readyListCount = count;
            readyNextRefresh = Time.unscaledTime + 1f;
            readyCache.Clear();
            foreach (var e in plugin.Events)
            {
                bool ok;
                try { ok = e.Ev.GetRate() > 0; } catch { ok = false; }   // 啟用條件不符時遊戲回傳 0
                readyCache[e.Ev] = ok;
            }
            foreach (var m in plugin.Missions) readyCache[m.M] = DataHelper.IsMissionReady(m.M);
            readyEvents = plugin.Events.OrderBy(e => Ready(e.Ev) ? 0 : 1).ToList();
            readyMissions = plugin.Missions.OrderBy(m => Ready(m.M) ? 0 : 1).ToList();
        }

        private static bool Ready(object key) => key != null && readyCache.TryGetValue(key, out var ok) && ok;

        private static void DrawOptionInfo()
        {
            Section("選項提示");
            Cfg.EnableOptionInfo.Value = Check(Cfg.EnableOptionInfo.Value, "在劇情選項後顯示結果（例如 +銀兩）");
            GUILayout.Label("在劇情與議題選項後面直接顯示行動次數、屬性、好感度等變化；關閉後立即生效。", S.Desc);
            GUILayout.Label("劇情檢查點的條件會記在回想紀錄裡（不寫入存檔）。", S.Desc);
        }

        // ---- 元件 ----
        private static void Section(string title)
        {
            GUILayout.Space(S.Px(12));
            GUILayout.Label(title, S.Section);
            var r = GUILayoutUtility.GetRect(1, S.Px(2), GUILayout.ExpandWidth(true));
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, Mathf.Max(1, S.Px(1.5f))), S.LineTex);
            GUILayout.Space(S.Px(6));
        }

        /// <summary>自繪核取方塊：整列可點，方塊＋文字。</summary>
        private static bool Check(bool value, string text)
        {
            var content = new GUIContent(text);
            var r = GUILayoutUtility.GetRect(content, S.CheckLabel);
            bool hover = r.Contains(Event.current.mousePosition);
            if (GUI.Button(r, GUIContent.none, GUIStyle.none)) value = !value;
            float b = S.Px(24);
            GUI.DrawTexture(new Rect(r.x, r.y + (r.height - b) / 2f, b, b), value ? S.CheckOn : hover ? S.CheckHover : S.CheckOff);
            GUI.Label(r, content, S.CheckLabel);
            return value;
        }

        private static void EntryCard(string key, string desc, bool ready)
        {
            GUILayout.BeginVertical(ready ? S.BoxOk : S.BoxFail);
            GUILayout.Label(OnPaper(key), S.Entry);
            if (!string.IsNullOrEmpty(desc)) GUILayout.Label(OnPaper(desc), S.Desc);
            GUILayout.EndVertical();
            GUILayout.Space(S.Px(6));
        }

        private static int GetStat(GameStatType type)
        {
            try { return PlayerStatManagerData.Instance.Stats.Get(type).FinalValue; }
            catch { return 0; }
        }

        /// <summary>RichText 的亮色是給遊戲深色底用的，在紙底上換成同色系的深色。</summary>
        internal static string OnPaper(string s) => s
            .Replace("#FFD700", "#8A5A00").Replace("#7CFC00", "#2E7D32")
            .Replace("#FF6B6B", "#B3261E").Replace("#AAAAAA", "#7A6E62");
    }
}
