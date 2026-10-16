using System;
using System.Collections.Generic;
using System.Text;
using Mortal.Core;
using Mortal.Story;
using OBB.Framework.Data;
using OBB.Framework.Utils;
using UnityEngine;

namespace LegendOfMortal_InternalMod.Core
{
    public static class DataHelper
    {
        public static string ExtractConditions(ConditionResultItem condition, out bool isInPeriod)
        {
            try
            {
                var list = new List<string>();
                isInPeriod = true;
                foreach (var item in condition.GetFieldValue<List<StatCompareItem>>("_items"))
                {
                    var v1 = item.GetFieldValue<StatGroupVariable>("_value1");
                    var v2 = item.GetFieldValue<StatGroupVariable>("_value2");
                    string op = GetCompareOperator(item.GetFieldValue<StatCompareType>("_compareOp"));
                    if (((UnityEngine.Object)v1).name.Contains("時間_") && !item.Check()) isInPeriod = false;
                    string n1 = FormatConditionValName(v1);
                    string n2 = FormatConditionValName(v2);
                    string s = string.Format("{0}({1}) {2} {3}({4})", n1, v1.GetResult(), op, n2, v2.GetResult());
                    s = item.Check() ? RichText.Green(s) : RichText.Red(s);
                    list.Add(s);
                }
                var lop = condition.GetFieldValue<LogicOperatorType>("_logicOp");
                return string.Join(" " + GetLogicOperator(lop) + " ", list);
            }
            catch (Exception ex)
            {
                LegendOfMortal_InternalMod.Plugin.LogError("ExtractConditions: " + ex.Message);
                isInPeriod = false;
                return string.Empty;
            }
        }

        private static string FormatConditionValName(StatGroupVariable v)
        {
            string name = ((UnityEngine.Object)v).name;
            if (name.StartsWith("旗標_"))
            {
                var list = v.GetFieldValue<List<StatValueReference>>("_value");
                if (list.Count > 0) return GetFlagName(list[0].GetFieldValue<FlagData>("_flag"));
            }
            return name.Replace("數值_", "").Replace("時間_", "").Replace("常數_", "");
        }

        public static string ParseSwitchCheckpoint(string checkpoint)
        {
            var inst = CheckPointManager.Instance;
            if (inst == null) return string.Empty;
            var sb = new StringBuilder();
            var arr = inst.GetFieldValue<SwitchResultConfig[]>("_switchList");
            SwitchResultData data = null;
            for (int i = 0; i < arr.Length; i++)
            {
                data = arr[i].GetData(checkpoint);
                if (data != null) break;
            }
            if (data == null) return string.Empty;
            foreach (var c in data.GetFieldValue<List<ConditionResultItem>>("_items"))
                sb.AppendLine("    " + ExtractConditions(c, out _));
            return sb.ToString();
        }

        public static Dictionary<MissionCheckData, (string Key, string Description)> GetMissions()
        {
            var dict = new Dictionary<MissionCheckData, (string, string)>();
            var sub = MissionManagerData.Instance?.GetFieldValue<SubMissionsData>("_subMissions");
            if (sub == null) return dict;
            foreach (MissionData md in ((CollectionData<MissionData>)sub).List)
            {
                string desc = md.GetFieldValue<string>("_desc");
                desc = string.IsNullOrWhiteSpace(desc) ? md.Name : desc;
                string cur = md.CurrentKey;
                if (cur == "clear") continue;
                var check = md.GetByKey(cur);
                if (check == null || !IsValidMission(check)) continue;
                string d2 = check.GetFieldValue<string>("_desc");
                d2 = string.IsNullOrWhiteSpace(d2) ? ((UnityEngine.Object)check).name : d2;
                string title = desc + " - " + d2;
                string pos = check.Position.ToString();
                bool inPeriod = GetMissionPeriod(check, out var period);
                if (!inPeriod) continue;
                var conds = ExtractConditions(check, out inPeriod);
                if (!inPeriod) continue;
                var sb = new StringBuilder();
                sb.AppendLine("位置：" + pos);
                sb.AppendLine("時間：" + period);
                sb.AppendLine("條件：");
                foreach (var c in conds) sb.AppendLine("    " + c);
                dict[check] = (title, sb.ToString());
            }
            return dict;
        }

        private static bool IsValidMission(MissionCheckData c)
        {
            bool day = c.GetFieldValue<bool>("_dayCheck");
            bool hasPos = (int)c.Position > 0;
            bool hasCond = c.GetFieldValue<ConditionResultItem[]>("_conditions").Length != 0;
            if (LoadLuaScript(c.StartScript) != string.Empty) return day | hasPos | hasCond;
            return false;
        }

        private static List<string> ExtractConditions(MissionCheckData c, out bool inPeriod)
        {
            var list = new List<string>();
            var arr = c.GetFieldValue<ConditionResultItem[]>("_conditions");
            inPeriod = true;
            foreach (var it in arr)
            {
                list.Add(ExtractConditions(it, out var sub));
                if (!sub) inPeriod = false;
            }
            return list;
        }

        public static bool GetMissionPeriod(MissionCheckData c, out string period)
        {
            if (!c.GetFieldValue<bool>("_dayCheck")) { period = "不限"; return true; }
            var now = PlayerStatManagerData.Instance?.GameTime;
            var t = c.GetFieldValue<TimeCheckType>("_timeCheckType");
            if ((int)t == 1)
            {
                var s = c.GetFieldValue<GameTime>("_specificTime");
                period = $"第{s.YearText}年{s.MonthText}月{s.StageText}";
                return now == s;
            }
            if ((int)t == 2)
            {
                var min = c.GetFieldValue<GameTime>("_minTime");
                var max = c.GetFieldValue<GameTime>("_maxTime");
                period = $"第{min.YearText}年{min.MonthText}月{min.StageText} - 第{max.YearText}年{max.MonthText}月{max.StageText}";
                if (now >= min) return now <= max;
                return false;
            }
            period = "不限";
            return true;
        }

        public static bool TryParseEnum<TEnum>(string value, out TEnum result) where TEnum : struct
        {
            return EnumUtils.TryParseByStringValue<TEnum>(value, out result);
        }

        public static string GetItemName(GameItemType type, string id)
        {
            var item = ItemDatabase.Instance.GetItem(type, id);
            if (item == null) return string.Empty;
            return LocalizationManager.Instance.LocaleResolver.GetString(item.GetNameKey());
        }

        public static string GetTalentName(string id)
        {
            var t = PlayerStatManagerData.Instance.Talents.Get(id);
            if (t == null) return string.Empty;
            return LocalizationManager.Instance.LocaleResolver.GetString(t.GetIdKey());
        }

        public static string GetStoryName(string id)
            => LocalizationManager.Instance.LocaleResolver.GetString("LegendInfo/" + id);

        public static string GetTeamName(string id)
        {
            var t = PlayerStatManagerData.Instance.EnemyTeam.Get(id);
            if (t == null) return string.Empty;
            return LocalizationManager.Instance.LocaleResolver.GetString(t.GetNameKey());
        }

        public static string GetEventName(PositionEventData ev)
        {
            var cfg = ev.GetFieldValue<StoryMappingItem>("_config");
            string name = cfg != null ? ((UnityEngine.Object)cfg).name : null;
            string note = ev.GetFieldValue<string>("_devNote");
            if (!string.IsNullOrWhiteSpace(note)) name = note;
            return RichText.Golden(name);
        }

        public static string LoadLuaScript(string scriptName)
        {
            if (string.IsNullOrEmpty(scriptName)) return string.Empty;
            // 新版支援繁中/簡中自動偵測，先試繁中再試簡中
            var a = Resources.Load<TextAsset>("story/chinesetraditional/" + scriptName.ToLower());
            if (a != null) return a.text;
            var b = Resources.Load<TextAsset>("story/chinesesimplified/" + scriptName.ToLower());
            return (b != null ? b.text : null) ?? string.Empty;
        }

        public static string GetCompareOperator(StatCompareType t) => (int)t switch
        {
            0 => "=",
            1 => "!=",
            2 => "<",
            3 => "<=",
            4 => ">",
            5 => ">=",
            _ => "unknown",
        };

        public static string GetLogicOperator(LogicOperatorType t)
        {
            if ((int)t == 1) return "或";
            if ((int)t == 0) return "且";
            return "unknown";
        }

        public static string GetFlagName(string id)
            => GetFlagName(MissionManagerData.Instance.GetFlag(id));

        public static string GetFlagName(FlagData f)
        {
            if (f == null) return string.Empty;
            string t = f.DevNote;
            if (t.Contains("\n")) t = t.Split('\n')[0];
            return string.IsNullOrEmpty(t) ? string.Empty : t;
        }
    }
}
