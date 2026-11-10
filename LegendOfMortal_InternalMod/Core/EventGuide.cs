using System;
using System.Collections.Generic;
using System.Linq;
using Mortal.Core;

namespace LegendOfMortal_InternalMod.Core
{
    /// <summary>
    /// 「事件攻略」頁：依目前日期、路線與參數，判斷 wiki 整理的事件是否會觸發。
    /// 只讀遊戲資料，不改任何數值。事件清單見 EventGuideData。
    /// </summary>
    public static class EventGuide
    {
        public const int Lookahead = 6;

        public static int Round(int year, int month, int stage) => (year - 1) * 36 + (month - 1) * 3 + stage;

        private static readonly string[] Nums = { "零", "一", "二", "三", "四", "五", "六", "七", "八", "九", "十", "十一", "十二" };
        private static readonly string[] Stages = { "上旬", "中旬", "下旬" };

        public static string FormatRound(int r)
        {
            int y = (r - 1) / 36 + 1, rem = (r - 1) % 36;
            return $"第{Nums[y]}年{Nums[rem / 3 + 1]}月{Stages[rem % 3]}";
        }

        /// <summary>地圖節點用的短格式：「二年八月中旬」。</summary>
        public static string FormatRoundShort(int r)
        {
            int y = (r - 1) / 36 + 1, rem = (r - 1) % 36;
            return $"{Nums[y]}年{Nums[rem / 3 + 1]}月{Stages[rem % 3]}";
        }

        // ---- 遊戲資料讀取 ----
        private static readonly Dictionary<string, FlagData> flagCache = new();

        public static int Flag(string id)
        {
            if (!flagCache.TryGetValue(id, out var f) || f == null)
            {
                f = MissionManagerData.Instance?.GetFlag(id);
                flagCache[id] = f;
            }
            return f != null ? f.State : 0;
        }

        public static int Stat(GameStatType t) => PlayerStatManagerData.Instance.Stats.Get(t).FinalValue;

        public static int Rel(RelationshipStatType t) => PlayerStatManagerData.Instance.Relationships.Get(t)?.Value ?? 0;

        // ---- 路線 ----
        /// <summary>玩家目前可能所在的路線（尚未分歧的軸視為全部可能）。</summary>
        public static Route PlayerRoutes(int now)
        {
            int study = Flag("M20008"), home = Flag("M20028"), decision = Flag("M70015");
            Route y2;
            if (study == 2) y2 = Route.崆峒;
            else if (study == 1) y2 = Route.青城;
            else if (home == 2) y2 = Route.客棧;
            else if (home == 1) y2 = now > Round(2, 8, 3) ? Route.破廟 : Route.破廟 | Route.青城;
            else y2 = now > Round(2, 2, 1) ? Route.不留學 : Route.留學線;

            Route end = decision switch
            {
                1 => Route.退隱,
                2 or 3 => Route.頑抗,
                4 => Route.西武林,
                _ => Route.決策線,
            };
            return y2 | end;
        }

        public static string RouteText(Route r)
        {
            var parts = new List<string>();
            var y2 = r & Route.留學線;
            if (y2 == Route.崆峒) parts.Add("崆峒留學");
            else if (y2 == Route.青城) parts.Add("青城留學");
            else if (y2 == Route.客棧) parts.Add("不留學・住客棧");
            else if (y2 == Route.破廟) parts.Add("不留學・住破廟");
            else if (y2 == (Route.破廟 | Route.青城)) parts.Add("不留學・住破廟（可能轉青城）");
            else if (y2 == Route.不留學) parts.Add("不留學");
            var end = r & Route.決策線;
            if (end == Route.退隱) parts.Add("退隱線");
            else if (end == Route.頑抗) parts.Add("滅門線（不成立西武林）");
            else if (end == Route.西武林) parts.Add("成立西武林");
            return parts.Count > 0 ? string.Join("・", parts) : "未分歧";
        }

        // ---- 判定 ----
        public static List<GuideItem> Evaluate(int now, bool includeUpcoming)
        {
            var routes = PlayerRoutes(now);
            var list = new List<GuideItem>();
            foreach (var ev in EventGuideData.All)
            {
                if ((ev.Route & routes) == 0 || now > ev.End) continue;
                GuideState state;
                if (now >= ev.Start) state = GuideState.Ready;
                else if (includeUpcoming && ev.Start - now <= Lookahead) state = GuideState.Upcoming;
                else continue;

                var item = new GuideItem { Event = ev };
                var res = GuideMap.Classify(ev.Reqs);
                item.Missing.AddRange(res.Missing);
                item.Met.AddRange(res.Met);
                item.Notes.AddRange(res.Notes);
                if (state == GuideState.Ready && item.Missing.Count > 0) state = GuideState.Lacking;
                item.State = state;
                list.Add(item);
            }
            return list.OrderBy(i => i.State).ThenBy(i => i.Event.Start).ToList();
        }
    }

    [Flags]
    public enum Route
    {
        崆峒 = 1, 客棧 = 2, 破廟 = 4, 青城 = 8,
        退隱 = 16, 頑抗 = 32, 西武林 = 64,
        不留學 = 客棧 | 破廟 | 青城,
        留學線 = 崆峒 | 不留學,
        決策線 = 退隱 | 頑抗 | 西武林,
        Any = 留學線 | 決策線,
    }

    public enum GuideState { Ready, Lacking, Upcoming }

    public sealed class GuideEvent
    {
        public string Name;
        public int Start, End;
        public Route Route;
        public Req[] Reqs;
    }

    public sealed class GuideItem
    {
        public GuideEvent Event;
        public GuideState State;
        public readonly List<string> Missing = new(), Met = new(), Notes = new();
    }

    public enum Op { Ge, Le, Gt, Lt, Eq, Ne }

    /// <summary>事件條件。Check 回傳 null 代表程式無法判定（備註）。</summary>
    public abstract class Req
    {
        public string Label;
        public abstract bool? Check();
        public abstract string Describe();

        protected static bool Compare(int a, Op op, int b) => op switch
        {
            Op.Ge => a >= b,
            Op.Le => a <= b,
            Op.Gt => a > b,
            Op.Lt => a < b,
            Op.Eq => a == b,
            _ => a != b,
        };

        protected static string Symbol(Op op) => op switch
        {
            Op.Ge => "≥",
            Op.Le => "≤",
            Op.Gt => ">",
            Op.Lt => "<",
            Op.Eq => "＝",
            _ => "≠",
        };
    }

    /// <summary>數值比較：角色數值、好感、計數型旗標共用。</summary>
    public sealed class ValueReq : Req
    {
        public Func<int> Read;
        public Op Op;
        public int Value;
        public override bool? Check() => Compare(Read(), Op, Value);
        public override string Describe() => $"{Label} {Read()}（需{Symbol(Op)}{Value}）";
    }

    /// <summary>是／否型旗標：只顯示名稱，名稱本身寫明要求的狀態。</summary>
    public sealed class FlagReq : Req
    {
        public string Id;
        public Op Op;
        public int Value;
        public override bool? Check() => Compare(EventGuide.Flag(Id), Op, Value);
        public override string Describe() => Label;
    }

    public sealed class AnyReq : Req
    {
        public Req[] Items;
        public override bool? Check()
        {
            var results = Items.Select(i => i.Check()).ToList();
            if (results.Any(r => r == true)) return true;
            return results.Any(r => r == null) ? null : false;
        }
        public override string Describe() => string.Join(" 或 ", Items.Select(i => i.Describe()));
    }

    public sealed class NoteReq : Req
    {
        public override bool? Check() => null;
        public override string Describe() => Label;
    }
}
