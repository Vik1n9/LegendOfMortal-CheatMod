using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Fungus;
using HarmonyLib;
using LegendOfMortal_InternalMod.Core;
using Mortal.Core;
using Mortal.Free;
using Mortal.Story;
using UnityEngine;

namespace LegendOfMortal_InternalMod.Patches
{
    /// <summary>
    /// 舊 OptionInfoPatch 在新版有兩個隱患：
    /// 1) MissionCheckData.Check() 舊版無參，新版是 Check(PlayerStatManagerData)，
    ///    舊 Prefix(只有 __instance) 雖然 Harmony 寬容仍能打上，但為穩定起見補上參數。
    /// 2) PositionController.OnPositionEnter/Click 在新版仍是無參，保留。
    /// 其餘 GetStoryText / LoadLuaFunction / Execute / GetRate 簽名未變，直接沿用。
    /// </summary>
    internal static class InfoPatch
    {
        private static string _originalDialogKey = string.Empty;
        private static PositionController _positionCtrl;

        public static PositionEventData TargetEvent;
        public static MissionCheckData TargetMission;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(LuaEnvironment), "LoadLuaFunction")]
        public static void LoadLua_Prefix(string luaString, string friendlyName)
        {
            try
            {
                Plugin.Instance.ClearLog(friendlyName, luaString);
            }
            catch (Exception ex)
            {
                LegendOfMortal_InternalMod.Plugin.LogError("LoadLua_Prefix: " + ex.Message);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(LeanLocalizationResolver), "GetStoryText")]
        public static void GetStoryText_Postfix(string key, ref string __result)
        {
            try
            {
                var dict = Core.LuaParser.Instance.OptionResult;
                if (!dict.TryGetValue(key, out var acts)) return;
                string text = Core.LuaParser.Description(acts);
                if (string.IsNullOrWhiteSpace(text)) return;
                string plain = Regex.Replace(text, "<.*?>", string.Empty);
                if (plain.Length > 30) plain = plain.Substring(0, 29) + "...";
                __result = __result + " <size=18>(" + plain + ")</size>";
            }
            catch { }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(PositionController), "OnPositionEnter")]
        public static void Enter_Prefix(PositionController __instance)
        {
            try
            {
                var pos = __instance.GetFieldValue<PositionType>("_position");
                if (_positionCtrl != null) HandleDialogChange(_positionCtrl, false);
                if (HasTriggerSubMissions(pos, out var missionKey))
                    HandleDialogChange(__instance, true, "存在事件：" + missionKey);
            }
            catch { }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(PositionController), "OnPositionClick")]
        public static void Click_Prefix()
        {
            if (_positionCtrl != null) HandleDialogChange(_positionCtrl, false);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ConditionResultData), "Execute")]
        public static void CondExec_Prefix(ConditionResultData __instance)
        {
            try
            {
                var items = __instance.GetFieldValue<List<ConditionResultItem>>("_items");
                LogCheckpoint("觸發劇情檢查點:", items);
            }
            catch { }
        }

        // 新版簽名：Check(PlayerStatManagerData playerStat)
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MissionCheckData), "Check")]
        public static void MissionCheck_Prefix(MissionCheckData __instance, PlayerStatManagerData playerStat)
        {
            try
            {
                var conds = __instance.GetFieldValue<ConditionResultItem[]>("_conditions");
                var sb = new StringBuilder("觸發任務檢查點:");
                AppendConditions(conds, sb);
                if (__instance.GetFieldValue<bool>("_dayCheck"))
                {
                    if (Core.DataHelper.GetMissionPeriod(__instance, out var period))
                    {
                        if (!string.IsNullOrWhiteSpace(period))
                            sb.AppendLine("    時限：" + period);
                    }
                }
                PrintToNarrativeLog(sb.ToString());
            }
            catch { }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(SwitchResultData), "Execute")]
        public static void SwitchExec_Prefix(SwitchResultData __instance)
        {
            try
            {
                var items = __instance.GetFieldValue<List<ConditionResultItem>>("_items");
                LogCheckpoint("觸發劇情分支檢查點:", items);
            }
            catch { }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PositionEventData), "GetRate")]
        public static void GetRate_Postfix(PositionEventData __instance, ref int __result)
        {
            if (TargetEvent == __instance)
            {
                __result = 99999;
                TargetEvent = null;
            }
        }

        // 新版簽名 Postfix 也要帶 playerStat（可省略，但寫上更穩）
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MissionCheckData), "Check")]
        public static void MissionCheck_Postfix(MissionCheckData __instance, PlayerStatManagerData playerStat, ref bool __result)
        {
            if (TargetMission == __instance)
            {
                __result = true;
                TargetMission = null;
            }
        }

        private static void HandleDialogChange(PositionController instance, bool addSuffix, string suffix = "")
        {
            try
            {
                var data = instance.GetFieldValue<FreePositionData>("_positionData");
                var dialog = data.GetDialog();
                string cur = dialog.GetFieldValue<string>("_dialog");
                if (addSuffix)
                {
                    _originalDialogKey = cur;
                    _positionCtrl = instance;
                    string text = LocalizationManager.Instance.LocaleResolver.GetString("Position/Dialog/" + cur);
                    if (string.IsNullOrEmpty(text)) text = cur;
                    dialog.SetPrivateField("_dialog", text + "(" + suffix + ")");
                }
                else
                {
                    dialog.SetPrivateField("_dialog", _originalDialogKey);
                    _positionCtrl = null;
                    _originalDialogKey = string.Empty;
                }
            }
            catch (Exception ex)
            {
                LegendOfMortal_InternalMod.Plugin.LogError("HandleDialogChange: " + ex.Message);
            }
        }

        private static bool HasTriggerSubMissions(PositionType position, out string missionKey)
        {
            foreach (var v in MissionManagerData.Instance.ActiveSubMissions.Values)
            {
                if (v.Position == position)
                {
                    missionKey = v.Key;
                    return true;
                }
            }
            missionKey = string.Empty;
            return false;
        }

        private static void LogCheckpoint(string header, IEnumerable<ConditionResultItem> items)
        {
            var sb = new StringBuilder(header);
            AppendConditions(items, sb);
            PrintToNarrativeLog(sb.ToString());
        }

        private static void AppendConditions(IEnumerable<ConditionResultItem> items, StringBuilder sb)
        {
            if (items == null) return;
            foreach (var it in items)
            {
                string t = Core.DataHelper.ExtractConditions(it, out _);
                if (!string.IsNullOrWhiteSpace(t)) sb.AppendLine("    " + t);
            }
        }

        // 1.0.6001.0 起回想改為 StoryLogPanel，Fungus NarrativeLog 不再顯示於任何介面。
        // isSave = false：不寫入存檔，移除 MOD 後不會在玩家存檔留下紀錄（讀檔後即清除）。
        private static void PrintToNarrativeLog(string message)
        {
            string text = Regex.Replace(message, @"^\s*$\n|\r", string.Empty, RegexOptions.Multiline).Trim();
            if (text.Length == 0) return;
            StoryLogData.AddText("<size=18>【內置MOD】" + text + "</size>", false);
        }
    }
}
