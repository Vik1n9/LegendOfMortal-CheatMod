using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace LegendOfMortal_InternalMod.Core
{
    public class LuaParser
    {
        private static readonly Lazy<LuaParser> instance = new(() => new LuaParser());
        private static readonly Regex indexMarkerRegex = new(@"^\s*--index:(O(_\w+)+)", RegexOptions.Multiline);
        private static readonly Regex endBlockRegex = new(@"\b(elseif|end)");
        private static readonly Regex switchRegex = new(@"checkpointmanager\.Switch\(([^)]*)\)");
        private static readonly Regex positionRegex = new(@"checkpointmanager\.Position\(([^)]*)\)");
        private static readonly Regex battleScenesRegex = new("luamanager\\.ChangeScene\\(\"(Combat|Battle)\"");
        private static readonly Regex scriptActionsRegex = new(@"luamanager\.SetNextScript");

        // NOTE：上面 modifyRegex 為避免轉義問題，實際以此為準
        private static readonly Regex modifyRegexReal = new(@"\s*statmodifymanager\.(?!GetDisplayTime)\w+\(.*?\)");

        public Dictionary<string, List<LuaAction>> OptionResult = new();
        private readonly Dictionary<string, List<LuaAction>> luaModification = new();
        private readonly Dictionary<string, List<string>> luaOptions = new();
        private readonly Dictionary<string, List<string>> luaSwitchCheck = new();
        private readonly Dictionary<string, List<string>> luaPositionWork = new();

        public static LuaParser Instance => instance.Value;

        public Dictionary<string, List<LuaAction>> ExtractOptionActionsFromLua(string friendlyName, string luaScript = "")
        {
            var dict = new Dictionary<string, List<LuaAction>>();
            if (luaOptions.TryGetValue(friendlyName, out var cached))
            {
                foreach (var k in cached) dict[k] = OptionResult[k];
                return dict;
            }
            luaOptions[friendlyName] = new List<string>();
            var matches = indexMarkerRegex.Matches(luaScript);
            for (int i = 0; i < matches.Count; i++)
            {
                string key = matches[i].Groups[1].Value;
                luaOptions[friendlyName].Add(key);
                if (OptionResult.ContainsKey(key)) { dict[key] = OptionResult[key]; continue; }
                int start = matches[i].Index + matches[i].Length;
                int end = luaScript.Length;
                if (i + 1 < matches.Count) end = Math.Min(end, matches[i + 1].Index);
                else
                {
                    var m = endBlockRegex.Match(luaScript.Substring(start));
                    if (m.Success) end = start + m.Index;
                }
                string block = luaScript.Substring(start, end - start);
                var list = new List<LuaAction>();
                AddActions(block, modifyRegexReal, ActionType.ModifyAction, list);
                AddActions(block, battleScenesRegex, ActionType.BattleScene, list);
                dict[key] = list;
                OptionResult[key] = list;
            }
            return dict;
        }

        private static void AddActions(string block, Regex rx, ActionType t, List<LuaAction> outList)
        {
            outList.AddRange(from Match m in rx.Matches(block) select new LuaAction(t, m.Value.Trim()));
        }

        public List<string> ExtractSwitchCheckFromLua(string friendlyName, string luaScript = "")
        {
            if (luaSwitchCheck.TryGetValue(friendlyName, out var v)) return v;
            var list = ExtractMatches(luaScript, switchRegex);
            luaSwitchCheck[friendlyName] = list;
            return list;
        }

        public List<string> ExtractPositionWorkFromLua(string friendlyName, string luaScript = "")
        {
            if (luaPositionWork.TryGetValue(friendlyName, out var v)) return v;
            var list = ExtractMatches(luaScript, positionRegex);
            luaPositionWork[friendlyName] = list;
            return list;
        }

        private static List<string> ExtractMatches(string lua, Regex rx)
        {
            var list = new List<string>();
            foreach (Match m in rx.Matches(lua))
            {
                string item = m.Groups[1].Value.Trim().Trim('"');
                if (!list.Contains(item)) list.Add(item);
            }
            return list;
        }

        public List<LuaAction> ExtractActionsFromLua(string luaScriptName)
        {
            if (luaModification.TryGetValue(luaScriptName, out var v)) return v;
            string[] lines = DataHelper.LoadLuaScript(luaScriptName).Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var list = new List<LuaAction>();
            bool cond = false;
            foreach (var raw in lines)
            {
                string t = raw.Trim();
                if (t.StartsWith("if") || t.StartsWith("elseif")) cond = true;
                else if (t.StartsWith("end")) cond = false;
                foreach (Match m in modifyRegexReal.Matches(t)) list.Add(new LuaAction(ActionType.ModifyAction, m.Value, cond));
                foreach (Match m in battleScenesRegex.Matches(t)) list.Add(new LuaAction(ActionType.BattleScene, m.Value, cond));
            }
            luaModification[luaScriptName] = list;
            return list;
        }

        public static string Description(List<LuaAction> actions)
        {
            var vals = (from a in actions select a.Description into d where !string.IsNullOrEmpty(d) select d).ToList();
            return string.Join("|", vals);
        }
    }
}
