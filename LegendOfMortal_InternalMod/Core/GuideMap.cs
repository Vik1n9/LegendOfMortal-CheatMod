using System;
using System.Collections.Generic;
using System.Linq;

namespace LegendOfMortal_InternalMod.Core
{
    public enum NodeKind { Event, Branch, EndingGroup }

    /// <summary>分歧點的一個選項：解鎖條件、結果、去向。</summary>
    public sealed class MapOption
    {
        public string Label;
        public string Result;
        /// <summary>解鎖／觸發這個選項需要的條件（程式可判定的會即時顯示達成與否）。</summary>
        public Req[] Reqs = new Req[0];
        /// <summary>判定「玩家已經選了這個」，沒有對應旗標就留空。</summary>
        public Req Taken;
        /// <summary>去向：節點 Id，或 "E數字" 表示結局編號。</summary>
        public string[] To = new string[0];
    }

    /// <summary>
    /// 指南地圖上的一個節點。Lane／Row 是地圖座標（Lane 0~4 對應該區段的五條欄位）。
    /// </summary>
    public sealed class MapNode
    {
        public string Id, Title, Where, Note;
        public NodeKind Kind;
        public int Start, End;
        public Route Route = Route.Any;
        public int Lane, Row;
        /// <summary>進入此節點的條件／此時應檢查的旗標（即時顯示）。</summary>
        public Req[] Reqs = new Req[0];
        public MapOption[] Options = new MapOption[0];
        /// <summary>沒有分歧時的後續節點 Id。</summary>
        public string[] Next = new string[0];
        /// <summary>結局群組節點：列出符合的結局。</summary>
        public Func<Ending, bool> EndingFilter;

        public IEnumerable<string> AllTargets => Next.Concat(Options.SelectMany(o => o.To));
    }

    /// <summary>地圖的分段標題與該段五條欄位的名稱（空字串代表沒用到）。</summary>
    public sealed class MapSection
    {
        public int Row;
        public string Title;
        public string[] Lanes;
    }

    public sealed class Ending
    {
        public int No;
        public string Name, Tags, Cond;
        public Route Route;
        public int Deadline;
        public bool Unavailable;
        public Req[] Reqs;

        public Ending(int no, string name, string tags, string cond, Route route, int deadline, bool unavailable, params Req[] reqs)
        {
            No = no; Name = name; Tags = tags; Cond = cond; Route = route; Deadline = deadline; Unavailable = unavailable; Reqs = reqs;
        }
    }

    public enum EndingState { Open, WrongRoute, Missed, Unavailable }

    public enum NodeState { Past, Current, Future, OffRoute }

    /// <summary>已分類的條件結果（缺／已達／無法判定）。</summary>
    public sealed class ReqResult
    {
        public readonly List<string> Missing = new(), Met = new(), Notes = new();
        public bool Any => Missing.Count + Met.Count + Notes.Count > 0;
    }

    public static class GuideMap
    {
        private static Dictionary<string, MapNode> byId;
        private static Dictionary<int, Ending> endingByNo;

        public static MapNode Get(string id)
        {
            byId ??= GuideMapData.Nodes.ToDictionary(n => n.Id);
            return id != null && byId.TryGetValue(id, out var n) ? n : null;
        }

        public static Ending GetEnding(int no)
        {
            endingByNo ??= GuideEndings.All.ToDictionary(e => e.No);
            return endingByNo.TryGetValue(no, out var e) ? e : null;
        }

        /// <summary>去向字串是否為結局（"E21"）。</summary>
        public static bool IsEnding(string to, out int no)
        {
            no = 0;
            return to != null && to.Length > 1 && to[0] == 'E' && int.TryParse(to.Substring(1), out no);
        }

        public static NodeState StateOf(MapNode n, int now, Route routes)
        {
            if ((n.Route & routes) == 0) return NodeState.OffRoute;
            if (now > n.End) return NodeState.Past;
            if (now >= n.Start) return NodeState.Current;
            return NodeState.Future;
        }

        public static EndingState StateOf(Ending e, int now, Route routes)
        {
            if (e.Unavailable) return EndingState.Unavailable;
            if (now > e.Deadline) return EndingState.Missed;
            if ((e.Route & routes) == 0) return EndingState.WrongRoute;
            return EndingState.Open;
        }

        public static ReqResult Classify(Req[] reqs)
        {
            var res = new ReqResult();
            if (reqs == null) return res;
            foreach (var r in reqs)
            {
                bool? ok;
                string text;
                try { ok = r.Check(); text = r.Describe(); }
                catch (Exception ex)
                {
                    ok = null; text = r.Label + "（讀取失敗）";
                    LegendOfMortal_InternalMod.Plugin.LogError("GuideMap: " + ex.Message);
                }
                (ok == null ? res.Notes : ok.Value ? res.Met : res.Missing).Add(text);
            }
            return res;
        }

        /// <summary>某選項是否已被玩家選過；沒有判定依據回傳 null。</summary>
        public static bool? IsTaken(MapOption o)
        {
            if (o.Taken == null) return null;
            try { return o.Taken.Check(); }
            catch { return null; }
        }

        /// <summary>分歧節點上玩家已選的選項（有旗標可判定時）。</summary>
        public static MapOption TakenOption(MapNode n) => n.Options.FirstOrDefault(o => IsTaken(o) == true);

        /// <summary>
        /// 目前玩家「正在」或「下一個要面對」的節點：在自己路線上、尚未結束、開始最早的那個。
        /// 同一旬有多個時取地圖上排最前面的。
        /// </summary>
        public static MapNode FindCurrent(int now, Route routes)
        {
            MapNode best = null;
            foreach (var n in GuideMapData.Nodes)
            {
                if (n.Kind == NodeKind.EndingGroup || (n.Route & routes) == 0 || now > n.End) continue;
                if (best == null || n.Start < best.Start || (n.Start == best.Start && n.Row < best.Row)) best = n;
            }
            return best;
        }

        /// <summary>某結局群組節點底下實際包含的結局。</summary>
        public static IEnumerable<Ending> EndingsOf(MapNode n) =>
            n.EndingFilter == null ? Enumerable.Empty<Ending>() : GuideEndings.All.Where(e => !e.Unavailable && n.EndingFilter(e));
    }
}
