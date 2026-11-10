using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mortal.Core;
using Mortal.Story;
using OBB.Framework.Data;
using UnityEngine;

namespace LegendOfMortal_InternalMod.Core
{
    public class PositionEventCache
    {
        private static readonly Lazy<PositionEventCache> instance = new(() => new PositionEventCache());
        private readonly Dictionary<string, List<PositionEventData>> dataCache = new();
        private readonly Dictionary<PositionEventData, string> keyCache = new();
        private readonly Dictionary<PositionEventData, string> descCache = new();
        private readonly Dictionary<PositionEventData, string> prefixCache = new();

        public static PositionEventCache Instance => instance.Value;

        public List<PositionEventData> GetPositionEvents(string position)
        {
            if (!dataCache.TryGetValue(position, out var v))
            {
                v = LoadPositionEvents(position);
                dataCache[position] = v;
            }
            return v;
        }

        public string GetPositionEventName(PositionEventData ev)
        {
            if (!keyCache.TryGetValue(ev, out var v))
            {
                v = GenerateEventKey(ev);
                keyCache[ev] = v;
            }
            return v;
        }

        public string GetPositionEventDesc(PositionEventData ev)
        {
            if (!descCache.TryGetValue(ev, out var v))
            {
                v = GenerateStaticDesc(ev);
                descCache[ev] = v;
            }
            return GenerateDynamicDesc(ev) + "\n" + v;
        }

        public List<PositionEventData> GetPositionEventsSortedByRate(string position)
            => GetPositionEvents(position).OrderByDescending(e => e.GetRate()).ToList();

        private List<PositionEventData> LoadPositionEvents(string position)
        {
            var list = new List<PositionEventData>();
            var mgr = CheckPointManager.Instance;
            if (mgr == null) return list;
            var cfg = mgr.GetFieldValue<PositionResultConfig>("_position");
            var target = ((CollectionData<PositionResultData>)cfg).List.FirstOrDefault(p => ((UnityEngine.Object)p).name == position);
            if (target == null) return list;
            string prefix = target.GetFieldValue<string>("_scriptPrefix");
            foreach (PositionEventData ev in ((CollectionData<PositionEventData>)target).List)
            {
                // 遊戲資料裡同一事件可能被重複放入清單（例：Center_Work_F_02），只列一次
                if (ev == null || list.Contains(ev)) continue;
                prefixCache[ev] = prefix;
                list.Add(ev);
            }
            return list;
        }

        private string GenerateEventKey(PositionEventData ev)
        {
            string pos = dataCache.FirstOrDefault(p => p.Value.Contains(ev)).Key ?? "";
            string place = TrainingNames.Places.TryGetValue(pos, out var p) ? p : pos;
            string result = null;
            try { result = ev.ResultType; } catch { }   // _config 未設定時 ResultType 會丟例外
            string name = result != null && TrainingNames.Events.TryGetValue(pos + "/" + result, out var n) ? RichText.Golden(n) : DataHelper.GetEventName(ev);
            return place + "：" + name;
        }

        private string GenerateStaticDesc(PositionEventData ev)
        {
            var sb = new StringBuilder();
            sb.AppendLine("可能的結果：");
            if (!prefixCache.TryGetValue(ev, out var prefix))
                throw new InvalidOperationException("找不到 scriptPrefix");
            string lua = prefix + "_" + ev.ResultType;
            var all = LuaParser.Instance.ExtractActionsFromLua(lua);
            var must = all.Where(a => !a.IsConditional).ToList();
            var cond = all.Where(a => a.IsConditional).ToList();
            sb.AppendLine("    一定觸發：" + (must.Count > 0 ? LuaParser.Description(must) : "無"));
            if (cond.Count > 0) sb.AppendLine("    條件觸發：" + LuaParser.Description(cond));
            return sb.ToString();
        }

        private string GenerateDynamicDesc(PositionEventData ev)
        {
            var sb = new StringBuilder();
            int def = ev.GetFieldValue<int>("_defaultRate");
            sb.AppendLine($"機率：{ev.GetRate()}（預設 {def}）");
            bool tog = ev.GetFieldValue<bool>("_toggleCondition");
            var cond = ev.GetFieldValue<ConditionResultItem>("_activeCondition");
            if (tog && cond != null)
                sb.AppendLine("啟用條件：" + DataHelper.ExtractConditions(cond, out _));
            var rates = ev.GetFieldValue<PositionEventRateItem[]>("_eventRateItem");
            if (rates != null && rates.Length > 0)
            {
                sb.AppendLine("機率加成：");
                foreach (var r in rates)
                {
                    string t = DataHelper.ExtractConditions(r.Condition, out _);
                    string v = r.Rate > 0 ? $"+{r.Rate}" : r.Rate.ToString();
                    sb.AppendLine("    " + t + "：" + v);
                }
            }
            return sb.ToString();
        }
    }
}
