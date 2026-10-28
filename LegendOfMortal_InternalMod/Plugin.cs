using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using LegendOfMortal_InternalMod.Config;
using LegendOfMortal_InternalMod.Core;
using Mortal.Core;
using Mortal.Story;
using UnityEngine;

namespace LegendOfMortal_InternalMod
{
    [BepInPlugin("LegendOfMortal_InternalMod", "活俠傳內置MOD", "1.1")]
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

            _harmony = new Harmony("LegendOfMortal_InternalMod");
            Patch(typeof(Patches.DicePatch), ModConfig.EnableDiceController.Value, "骰子控制");
            Patch(typeof(Patches.InfoPatch), ModConfig.EnableOptionInfo.Value, "選項情報");
            Patch(typeof(Patches.BattleCheatPatch), ModConfig.EnableCheats.Value, "戰鬥作弊");
            Patch(typeof(Patches.UpkeepCheatPatch), ModConfig.EnableCheats.Value, "養成作弊");
            Patch(typeof(Patches.PracticeCheatPatch), ModConfig.EnableCheats.Value, "練功作弊");
            Patch(typeof(Patches.FriendshipCheatPatch), ModConfig.EnableCheats.Value, "好感作弊");
            Patch(typeof(Patches.CombatSinglePatch), ModConfig.EnableCheats.Value, "單挑作弊");
            LogInfo("內置MOD v1.1 載入成功（適配 v1.0.6001.0）");

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
            Patches.InfoPatch.TargetEvent = null;
            Patches.InfoPatch.TargetMission = null;
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

        private void Patch(System.Type type, bool enabled, string name)
        {
            if (!enabled) return;
            try { _harmony.PatchAll(type); LogInfo($"補丁已套用：{name}"); }
            catch (System.Exception ex) { LogError($"補丁 {name} 失敗: {ex.Message}"); }
        }
    }
}
