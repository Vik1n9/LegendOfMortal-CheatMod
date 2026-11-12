using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using LegendOfMortal_InternalMod.Config;
using LegendOfMortal_InternalMod.Core;
using Mortal.Core;
using Mortal.Story;

namespace LegendOfMortal_InternalMod
{
    /// <summary>
    /// 《唐門密卷》Lite：事件攻略、事件任務（只列出不指定）、選項提示。
    /// 不含骰子控制、數值修改或任何改變判定結果的補丁；唯一的 Harmony 補丁 InfoPatch 只讀取並附加顯示文字。
    /// GUID 與完整版相同：兩者同時安裝時 BepInEx 只會載入其中一個，避免兩個視窗互搶快捷鍵。
    /// </summary>
    [BepInPlugin("LegendOfMortal_InternalMod", "唐門密卷 Lite", "1.3.2")]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance { get; private set; }
        private static ManualLogSource _log;
        private static Harmony _harmony;

        // 養成事件／支線任務清單（給「事件任務」頁）
        private readonly List<(PositionEventData Ev, string Key, string Desc)> eventEntries = new();
        private readonly List<(MissionCheckData M, string Key, string Desc)> missionEntries = new();

        private void Awake()
        {
            Instance = this;
            _log = Logger;
            ModConfig.Init(base.Config);

            // 事件任務頁也靠 InfoPatch 收集資料，故一律套用；選項提示的顯示由設定即時切換
            _harmony = new Harmony("LegendOfMortal_InternalMod");
            try { _harmony.PatchAll(typeof(Patches.InfoPatch)); LogInfo("補丁已套用：選項提示／事件任務"); }
            catch (System.Exception ex) { LogError($"補丁 選項提示／事件任務 失敗: {ex.Message}"); }
            LogInfo("《唐門密卷》Lite v1.3.2 Beta 載入成功（適配 v1.0.6001.0 崆峒神威）");

            // Update/OnGUI 交給自建常駐物件，不依賴 BepInEx_Manager 存活
            ModRunner.Ensure("Awake");
        }

        private void OnDestroy()
        {
            // 不在此 UnpatchSelf：管理物件被遊戲銷毀時，補丁與 Runner 仍需繼續運作
            LogInfo("BepInEx 管理物件被銷毀（補丁保留，Runner 獨立運作）");
        }

        // ---- 日誌 API（給 InfoPatch 呼叫） ----
        public void ClearLog(string friendlyName, string luaString)
        {
            eventEntries.Clear(); missionEntries.Clear();
            // 解析結果存進 LuaParser.OptionResult，供 InfoPatch 在選項後顯示收益
            LuaParser.Instance.ExtractOptionActionsFromLua(friendlyName, luaString);
            foreach (var p in LuaParser.Instance.ExtractPositionWorkFromLua(friendlyName, luaString))
            {
                foreach (var ev in PositionEventCache.Instance.GetPositionEventsSortedByRate(p))
                    eventEntries.Add((ev, PositionEventCache.Instance.GetPositionEventName(ev),
                        PositionEventCache.Instance.GetPositionEventDesc(ev)));
            }
            foreach (var kv in DataHelper.GetMissions())
                missionEntries.Add((kv.Key, kv.Value.Key, kv.Value.Description));
        }

        public List<(PositionEventData Ev, string Key, string Desc)> Events => eventEntries;
        public List<(MissionCheckData M, string Key, string Desc)> Missions => missionEntries;

        public static void LogInfo(object o) => _log?.LogInfo(o);
        public static void LogError(object o) => _log?.LogError(o);
    }
}
