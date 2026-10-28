using System;
using LegendOfMortal_InternalMod.Features;
using LegendOfMortal_InternalMod.Patches;
using Mortal.Core;
using UnityEngine;
using Cfg = LegendOfMortal_InternalMod.Config.ModConfig;
using S = LegendOfMortal_InternalMod.UI.GameStyleUI;

namespace LegendOfMortal_InternalMod.UI
{
    /// <summary>
    /// 主視窗「唐門密卷」：F1 開關，拖曳標題列移動。
    /// 分頁：骰子選項／屬性銀兩／戰鬥養成／事件任務／系統
    /// </summary>
    public static class CheatWindow
    {
        public static bool Visible = false;

        /// <summary>視窗目前位置（GUI 座標，左上原點），給 ClickBlocker 對齊用。</summary>
        public static Rect WindowRect => rect;

        private const int WindowId = 2077;
        private const float BaseW = 780f, BaseScrollH = 600f;
        private static Rect rect;
        private static float rectScale;
        private static Vector2 scroll;
        private static int tab;
        private static readonly string[] tabs = { "骰子選項", "屬性銀兩", "戰鬥養成", "事件任務", "系統" };

        private static string diceIn, moneyIn = "0", fateIn = "0", statIn = "100";

        private static readonly GameStatType[] PersonStats =
            { GameStatType.體力, GameStatType.內力, GameStatType.輕功, GameStatType.魅力, GameStatType.學問, GameStatType.嘴力, GameStatType.道德, GameStatType.修養 };
        private static readonly GameStatType[] SectStats =
            { GameStatType.門派資產, GameStatType.門派名聲, GameStatType.門派人數, GameStatType.向心力, GameStatType.個人貢獻度 };

        public static void Draw(Plugin plugin)
        {
            if (!Visible) return;
            if (rectScale != S.Scale)
            {
                rectScale = S.Scale;
                // 放在左上 HUD（貢獻度／日期，約到 y=170）下方
                rect = new Rect(S.Px(60), S.Px(185), S.Px(BaseW), 0);
            }
            rect = GUILayout.Window(WindowId, rect, id => DrawMain(plugin), "唐門密卷", GUILayout.Width(S.Px(BaseW)));
        }

        private static void DrawMain(Plugin plugin)
        {
            if (GUI.Button(new Rect(rect.width - S.Px(48), S.Px(14), S.Px(34), S.Px(34)), "×", S.Close)) Visible = false;

            tab = GUILayout.Toolbar(tab, tabs, S.Tab);
            GUILayout.Space(S.Px(10));

            scroll = GUILayout.BeginScrollView(scroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.Height(S.Px(BaseScrollH)));
            try
            {
                switch (tab)
                {
                    case 0: DrawDice(); break;
                    case 1: DrawStats(); break;
                    case 2: DrawBattle(); break;
                    case 3: DrawMissions(plugin); break;
                    case 4: DrawSystem(); break;
                }
            }
            finally { GUILayout.EndScrollView(); }

            GUILayout.Space(S.Px(6));
            GUILayout.Label($"{Cfg.MainMenuKey.Value} 開關視窗　·　拖曳標題列可移動", S.Desc);
            GUI.DragWindow(new Rect(0, 0, rect.width, S.Px(60)));
        }

        // ---- 分頁 ----
        private static void DrawDice()
        {
            Section("天命骰子");
            Cfg.EnableDiceController.Value = Check(Cfg.EnableDiceController.Value, "啟用骰子控制");
            int forced = Cfg.ForcedDiceValue.Value;
            diceIn ??= forced.ToString();
            GUILayout.BeginHorizontal();
            GUILayout.Label("指定點數", S.Row, GUILayout.Width(S.Px(110)));
            diceIn = GUILayout.TextField(diceIn, GUILayout.Width(S.Px(90)));
            if (GUILayout.Button("套用", GUILayout.Width(S.Px(80))) && int.TryParse(diceIn, out var v)) Cfg.ForcedDiceValue.Value = Math.Max(0, v);
            if (GUILayout.Button("改回隨機", GUILayout.Width(S.Px(110)))) { Cfg.ForcedDiceValue.Value = 0; diceIn = "0"; }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Label(forced > 0 ? $"目前：固定擲出 <b>{forced}</b>" : "目前：隨機", S.Entry);
            GUILayout.Label("最終點數仍會加上武學、好感等加成。填 0 等於隨機。", S.Desc);

            Section("選項提示");
            Cfg.EnableOptionInfo.Value = Check(Cfg.EnableOptionInfo.Value, "在劇情選項後顯示結果（例如 +銀兩）");
            GUILayout.Label("養成地點的事件與支線任務，請到「事件任務」頁查看。", S.Desc);
        }

        private static void DrawStats()
        {
            Section("銀兩與命運");
            StatRow("銀兩", GameStatType.銀兩, ref moneyIn);
            StatRow("命運", GameStatType.命運, ref fateIn);
            GUILayout.BeginHorizontal();
            GUILayout.Label("行動點", S.Row, GUILayout.Width(S.Px(110)));
            if (GUILayout.Button("回滿行動點", GUILayout.Width(S.Px(140)))) CheatEngine.AddActionPoints();
            GUILayout.Space(S.Px(16));
            Cfg.InfAction.Value = Check(Cfg.InfAction.Value, "無限行動點");
            GUILayout.EndHorizontal();

            Section("人物屬性");
            GUILayout.Label("請先打開人物選單再設定；超過上限會自動放寬。", S.Desc);
            int target = StatTargetRow();
            StatGrid(PersonStats, target);

            Section("門派");
            GUILayout.Label("請先打開門派介面再設定，數值同上方「設定值」。", S.Desc);
            StatGrid(SectStats, target);
        }

        private static void DrawBattle()
        {
            Section("戰鬥");
            Cfg.GodCombat.Value = Check(Cfg.GodCombat.Value, "單挑：我方無敵");
            Cfg.GodBattle.Value = Check(Cfg.GodBattle.Value, "群戰：我方無敵");
            BattleCheatPatch.OneHitKill = Check(BattleCheatPatch.OneHitKill, "群戰：一擊必殺");
            BattleCheatPatch.InfCooldown = Check(BattleCheatPatch.InfCooldown, "技能無冷卻");
            BattleCheatPatch.InfTime = Check(BattleCheatPatch.InfTime, "戰鬥不限時");

            Section("養成");
            Cfg.InfPractice.Value = Check(Cfg.InfPractice.Value, "練功不消耗");
            Cfg.FastPractice.Value = Check(Cfg.FastPractice.Value, "快速練功");
            Cfg.MaxFriendship.Value = Check(Cfg.MaxFriendship.Value, "好感只升不降（含小梅、小竹結緣線）");
        }

        private static void DrawMissions(Plugin plugin)
        {
            if (plugin.Events.Count + plugin.Missions.Count == 0)
            {
                GUILayout.Space(S.Px(40));
                GUILayout.Label("目前沒有資料", S.Title);
                GUILayout.Label("走到養成地點或進入劇情後，這裡會自動列出可觸發的事件與支線任務。", new GUIStyle(S.Desc) { alignment = TextAnchor.MiddleCenter });
                return;
            }
            if (plugin.Events.Count > 0)
            {
                Section("養成事件");
                GUILayout.Label("按「指定」後，下次在此地點必定觸發。", S.Desc);
                foreach (var e in plugin.Events)
                    EntryCard(e.Key, e.Desc, InfoPatch.TargetEvent == e.Ev ? "已指定" : "指定", () => InfoPatch.TargetEvent = e.Ev);
            }
            if (plugin.Missions.Count > 0)
            {
                Section("支線任務");
                GUILayout.Label("按「必定通過」後，該任務判定必過。", S.Desc);
                foreach (var m in plugin.Missions)
                    EntryCard(m.Key, m.Desc, InfoPatch.TargetMission == m.M ? "已選定" : "必定通過", () => InfoPatch.TargetMission = m.M);
            }
        }

        private static void DrawSystem()
        {
            Section("遊戲速度");
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<b>{Cfg.GameSpeed.Value:0.0}</b> 倍", S.Row, GUILayout.Width(S.Px(110)));
            if (GUILayout.Button("－0.5", GUILayout.Width(S.Px(80)))) Cfg.GameSpeed.Value = Math.Max(0.5f, Cfg.GameSpeed.Value - 0.5f);
            if (GUILayout.Button("＋0.5", GUILayout.Width(S.Px(80)))) Cfg.GameSpeed.Value = Math.Min(5f, Cfg.GameSpeed.Value + 0.5f);
            if (GUILayout.Button("恢復正常", GUILayout.Width(S.Px(110)))) Cfg.GameSpeed.Value = 1f;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Label("只影響一般遊玩，不會干擾暫停與劇情快轉。", S.Desc);

            Section("關於");
            GUILayout.Label("活俠傳內置 MOD v1.1（適用 1.0.6001.0）", S.Desc);
            GUILayout.Label("設定檔：BepInEx/config/LegendOfMortal_InternalMod.cfg", S.Desc);
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

        private static void StatRow(string name, GameStatType type, ref string input)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, S.Row, GUILayout.Width(S.Px(110)));
            GUILayout.Label($"目前 {CheatEngine.GetStat(type)}", S.RowDesc, GUILayout.Width(S.Px(150)));
            input = GUILayout.TextField(input, GUILayout.Width(S.Px(120)));
            if (GUILayout.Button("設定", GUILayout.Width(S.Px(80))) && int.TryParse(input, out var v)) CheatEngine.SetStat(type, v);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private static int StatTargetRow()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("設定值", S.Row, GUILayout.Width(S.Px(110)));
            statIn = GUILayout.TextField(statIn, GUILayout.Width(S.Px(120)));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            return int.TryParse(statIn, out var v) ? v : 100;
        }

        /// <summary>兩欄：名稱　目前值　[設為 N]</summary>
        private static void StatGrid(GameStatType[] stats, int target)
        {
            for (int i = 0; i < stats.Length; i += 2)
            {
                GUILayout.BeginHorizontal();
                for (int j = i; j < i + 2; j++)
                {
                    if (j < stats.Length)
                    {
                        var t = stats[j];
                        GUILayout.Label(t.ToString(), S.Row, GUILayout.Width(S.Px(110)));
                        GUILayout.Label(CheatEngine.GetStat(t).ToString(), S.RowEntry, GUILayout.Width(S.Px(80)));
                        if (GUILayout.Button($"設為 {target}", GUILayout.Width(S.Px(130)))) CheatEngine.SetStat(t, target);
                    }
                    if (j == i) GUILayout.Space(S.Px(30));
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.Space(S.Px(4));
            }
        }

        private static void EntryCard(string key, string desc, string button, Action onClick)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.Label(OnPaper(key), S.Entry);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(button, GUILayout.Width(S.Px(130)))) onClick();
            GUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(desc)) GUILayout.Label(OnPaper(desc), S.Desc);
            GUILayout.EndVertical();
            GUILayout.Space(S.Px(6));
        }

        /// <summary>RichText 的亮色是給遊戲深色底用的，在紙底上換成同色系的深色。</summary>
        private static string OnPaper(string s) => s
            .Replace("#FFD700", "#8A5A00").Replace("#7CFC00", "#2E7D32")
            .Replace("#FF6B6B", "#B3261E").Replace("#AAAAAA", "#7A6E62");
    }
}
