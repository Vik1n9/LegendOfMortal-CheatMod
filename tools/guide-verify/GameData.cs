using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using LegendOfMortal_InternalMod.Core;
using Mortal.Core;

namespace GuideVerify
{
    /// <summary>
    /// 用遊戲本體抽出的資料（tools/game-extract/out）檢查攻略內容。
    /// 沒有資料時整段跳過，只跑 Program.cs 的純資料檢查，方便沒有遊戲的環境。
    /// </summary>
    public static class GameData
    {
        public sealed class Flag
        {
            public int Default;
            /// <summary>開發備註裡列出的值（只有數字），例如 "1:青城 2:崆峒" → { 1, 2 }。</summary>
            public readonly HashSet<int> Values = new();
        }

        public sealed class Ending
        {
            public int No;
            public string Title;
        }

        public sealed class Script
        {
            public List<string> OpenEndings = new();
            public List<string> NextScripts = new();
            public List<string> Refs = new();
            public List<KeyValuePair<string, int>> SetFlags = new();
            public List<EndingOpen> Opens = new();
        }

        /// <summary>某個腳本裡開啟某結局的位置，Guards 是包住它的分支條件。</summary>
        public sealed class EndingOpen
        {
            public string Script;
            public int Id;
            public List<Guard> Guards = new();
            public string Text => string.Join("、", Guards.Select(g => g.Text));
        }

        public sealed class Guard
        {
            public string Kind;   // flag / checkpoint / script
            public string Key;
            public int Value;
            public string Text => Kind switch
            {
                "flag" => $"{Key}={Value}",
                "checkpoint" => $"{Key} 選項{Value}",
                _ => "劇本條件"
            };
        }

        public static Dictionary<string, Flag> Flags = new();
        public static List<Ending> Endings = new();
        public static Dictionary<string, Script> Scripts = new();
        public static HashSet<string> MissionStarts = new();
        /// <summary>旗標 → 有哪些值曾被任何腳本／任務寫入（用來判斷分支條件是否可能成立）。</summary>
        public static Dictionary<string, HashSet<int>> FlagSetters = new();
        /// <summary>任務檢查點的時間（旬序 → 代表名稱），用來做攻略收錄率統計。</summary>
        public static Dictionary<int, string> MissionTimes = new();

        public static int Fails, Warns;

        private static void Fail(string msg) { Fails++; Console.WriteLine("FAIL: " + msg); }
        private static void Warn(string msg) { Warns++; Console.WriteLine("WARN: " + msg); }

        /// <summary>資料目錄：優先用環境變數 LOM_GAMEDATA，其次找 repo 內的 tools/game-extract/out。</summary>
        public static string FindDir()
        {
            var env = Environment.GetEnvironmentVariable("LOM_GAMEDATA");
            if (!string.IsNullOrEmpty(env) && Directory.Exists(env)) return env;
            for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            {
                var c = Path.Combine(d.FullName, "tools", "game-extract", "out");
                if (Directory.Exists(c)) return c;
            }
            return null;
        }

        public static bool Load(string dir)
        {
            var flagsFile = Path.Combine(dir, "flags.json");
            var endingsFile = Path.Combine(dir, "endings.json");
            var scriptsFile = Path.Combine(dir, "scripts.json");
            var missionsFile = Path.Combine(dir, "missions.json");
            if (!File.Exists(flagsFile) || !File.Exists(endingsFile) || !File.Exists(scriptsFile))
                return false;

            using (var doc = JsonDocument.Parse(File.ReadAllText(flagsFile)))
                foreach (var p in doc.RootElement.EnumerateObject())
                {
                    var f = new Flag { Default = p.Value.GetProperty("default").GetInt32() };
                    foreach (var v in p.Value.GetProperty("values").EnumerateArray()) f.Values.Add(v.GetInt32());
                    Flags[p.Name] = f;
                }

            using (var doc = JsonDocument.Parse(File.ReadAllText(endingsFile)))
                foreach (var e in doc.RootElement.EnumerateArray())
                    Endings.Add(new Ending { No = e.GetProperty("no").GetInt32(), Title = e.GetProperty("title").GetString() });

            using (var doc = JsonDocument.Parse(File.ReadAllText(scriptsFile)))
                foreach (var p in doc.RootElement.EnumerateObject())
                {
                    var s = new Script();
                    foreach (var x in p.Value.GetProperty("openEndings").EnumerateArray()) s.OpenEndings.Add(x.GetString());
                    foreach (var x in p.Value.GetProperty("nextScripts").EnumerateArray()) s.NextScripts.Add(x.GetString());
                    if (p.Value.TryGetProperty("refs", out var refs))
                        foreach (var x in refs.EnumerateArray()) s.Refs.Add(x.GetString());
                    foreach (var f in p.Value.GetProperty("setFlags").EnumerateArray())
                        s.SetFlags.Add(new KeyValuePair<string, int>(
                            f.GetProperty("flag").GetString(), f.GetProperty("value").GetInt32()));
                    if (p.Value.TryGetProperty("endingOpens", out var opens))
                        foreach (var o in opens.EnumerateArray())
                        {
                            var eo = new EndingOpen { Script = p.Name, Id = int.Parse(o.GetProperty("id").GetString()) };
                            foreach (var g in o.GetProperty("guards").EnumerateArray())
                            {
                                var guard = new Guard();
                                if (g.TryGetProperty("kind", out var kind)) guard.Kind = kind.GetString();
                                if (g.TryGetProperty("key", out var key)) guard.Key = key.GetString();
                                if (g.TryGetProperty("value", out var val)) guard.Value = val.GetInt32();
                                eo.Guards.Add(guard);
                            }
                            s.Opens.Add(eo);
                        }
                    Scripts[p.Name] = s;
                }

            foreach (var sc in Scripts.Values)
                foreach (var p in sc.SetFlags)
                {
                    if (!FlagSetters.TryGetValue(p.Key, out var vs)) FlagSetters[p.Key] = vs = new HashSet<int>();
                    vs.Add(p.Value);
                }

            if (File.Exists(missionsFile))
                using (var doc = JsonDocument.Parse(File.ReadAllText(missionsFile)))
                    foreach (var m in doc.RootElement.EnumerateArray())
                        foreach (var c in m.GetProperty("checks").EnumerateArray())
                        {
                            var s = c.GetProperty("startScript").GetString();
                            if (!string.IsNullOrEmpty(s)) MissionStarts.Add(s);
                            var t = c.GetProperty("time");
                            var label = m.GetProperty("name").GetString() + "/" + c.GetProperty("key").GetString()
                                        + (string.IsNullOrEmpty(s) ? "" : "（" + s + "）");
                            foreach (var key in new[] { "at", "from" })
                                if (t.TryGetProperty(key, out var at) && at.ValueKind == JsonValueKind.Array && at.GetArrayLength() == 3)
                                {
                                    int r = Round(at[0].GetInt32(), at[1].GetInt32(), at[2].GetInt32());
                                    if (!MissionTimes.ContainsKey(r)) MissionTimes[r] = label;
                                }
                        }
            return true;
        }

        public static void Run()
        {
            CheckFlagsUsedByGuide();
            CheckEndingTable();
            CheckEndingReachability();
            CheckEndingTargets();
            CheckMissionTimes();
            SmokeTest();
        }

        // ---- 旗標 ----
        private static IEnumerable<FlagReq> FlagReqs(Req[] reqs)
        {
            foreach (var r in reqs ?? new Req[0])
            {
                if (r is FlagReq f) yield return f;
                else if (r is AnyReq a)
                    foreach (var x in FlagReqs(a.Items)) yield return x;
            }
        }

        private static IEnumerable<(FlagReq Req, string Owner)> GuideFlags()
        {
            foreach (var n in GuideMapData.Nodes)
            {
                foreach (var r in FlagReqs(n.Reqs)) yield return (r, n.Id);
                foreach (var o in n.Options)
                {
                    foreach (var r in FlagReqs(o.Reqs)) yield return (r, n.Id + "／" + o.Label);
                    foreach (var r in FlagReqs(o.Taken == null ? null : new[] { o.Taken })) yield return (r, n.Id + "／" + o.Label);
                }
            }
            foreach (var e in GuideEndings.All)
                foreach (var r in FlagReqs(e.Reqs)) yield return (r, "結局" + e.No);
        }

        private static void CheckFlagsUsedByGuide()
        {
            int n = 0;
            foreach (var (r, owner) in GuideFlags())
            {
                n++;
                if (!Flags.TryGetValue(r.Id, out var f))
                {
                    Fail($"{owner}: 旗標 {r.Id} 不在遊戲旗標表");
                    continue;
                }
                // 備註沒列值域的旗標無從比對；有列的話，攻略要求的值必須在裡面（或是預設值）
                if (r.Op != Op.Eq || r.Value <= 0 || f.Values.Count == 0) continue;
                if (!f.Values.Contains(r.Value) && f.Default != r.Value)
                    Warn($"{owner}: {r.Id}={r.Value}（{r.Label}）不在遊戲值域 {string.Join("/", f.Values.OrderBy(v => v))}");
            }
            Console.WriteLine($"旗標：檢查 {n} 個引用（{Flags.Count} 個旗標可用）");
        }

        // ---- 結局 ----
        private static void CheckEndingTable()
        {
            if (Endings.Count != 66) Fail($"遊戲結局數 {Endings.Count}，預期 66");
            var byNo = Endings.ToDictionary(e => e.No);
            foreach (var e in GuideEndings.All)
            {
                if (!byNo.TryGetValue(e.No, out var g)) { Fail($"攻略結局{e.No} 在遊戲找不到"); continue; }
                if (g.Title != e.Name) Fail($"結局{e.No} 名稱「{e.Name}」，遊戲為「{g.Title}」");
            }
            Console.WriteLine($"結局：{GuideEndings.All.Length} 個，編號與名稱比對完成");
        }

        private static HashSet<string> reachableScripts;
        private static HashSet<string> ReachableScripts()
        {
            if (reachableScripts != null) return reachableScripts;
            var seen = new HashSet<string>();
            var q = new Queue<string>(MissionStarts);
            while (q.Count > 0)
            {
                var s = q.Dequeue();
                if (!seen.Add(s)) continue;
                if (Scripts.TryGetValue(s, out var sc))
                {
                    foreach (var n in sc.NextScripts) q.Enqueue(n);
                    foreach (var n in sc.Refs) q.Enqueue(n);
                }
            }
            return reachableScripts = seen;
        }

        private static Dictionary<int, List<string>> openedBy;
        private static Dictionary<int, List<string>> OpenedBy()
        {
            if (openedBy != null) return openedBy;
            openedBy = new Dictionary<int, List<string>>();
            foreach (var p in Scripts)
                foreach (var id in p.Value.OpenEndings)
                    if (int.TryParse(id, out int raw) && raw >= 20000 && raw <= 20065)
                    {
                        int no = raw - 19999;
                        if (!openedBy.TryGetValue(no, out var list)) openedBy[no] = list = new List<string>();
                        list.Add(p.Key);
                    }
            return openedBy;
        }

        /// <summary>分支條件的旗標值是否真的有人寫入（例：E12000=1 要有劇本設定過）。</summary>
        private static bool Satisfiable(EndingOpen o) =>
            o.Guards.Where(g => g.Kind == "flag")
                    .All(g => (FlagSetters.TryGetValue(g.Key, out var vs) && vs.Contains(g.Value))
                           || (Flags.TryGetValue(g.Key, out var f) && f.Default == g.Value));

        private static string Describe(IEnumerable<EndingOpen> opens) =>
            string.Join("；", opens.Take(2).Select(o => $"{o.Script}（{o.Text}）"));

        private static void CheckEndingReachability()
        {
            var reach = ReachableScripts();
            var opens = Scripts.Values.SelectMany(x => x.Opens).ToList();
            int live = 0, unreachable = 0;
            foreach (var e in GuideEndings.All)
            {
                int raw = 19999 + e.No;
                var mine = opens.Where(o => o.Id == raw).ToList();
                var viable = mine.Where(Satisfiable).ToList();
                bool any = mine.Count > 0;
                bool isLive = viable.Count > 0;
                if (isLive) live++;
                if (any && !mine.Any(o => reach.Contains(o.Script))) unreachable++;

                if (e.Unavailable && isLive)
                    Warn($"結局{e.No}「{e.Name}」標為目前無法達成，但 {Describe(viable)} 會開啟它"
                         + (reach.Contains(viable[0].Script) ? "（且該劇本可從任務走到）" : ""));
                else if (!e.Unavailable && !any)
                    Warn($"結局{e.No}「{e.Name}」標為可達成，但沒有任何腳本開啟");
                else if (!e.Unavailable && !isLive)
                    Warn($"結局{e.No}「{e.Name}」標為可達成，但開啟點的條件不可能成立（{Describe(mine)}）");
                else if (e.Unavailable && any && !isLive)
                    Console.WriteLine($"  結局{e.No}「{e.Name}」：腳本存在但分支條件不可達（{Describe(mine)}）→ 與標記一致");
            }
            Console.WriteLine($"結局腳本：{live}/66 的開啟條件可成立；{unreachable} 個只出現在走不到的腳本（僅供參考）");
        }

        private static void CheckEndingTargets()
        {
            int n = 0;
            foreach (var node in GuideMapData.Nodes)
                foreach (var o in node.Options)
                    foreach (var t in o.To)
                        if (GuideMap.IsEnding(t, out int no))
                        {
                            n++;
                            if (GuideMap.GetEnding(no) is { Unavailable: true } e)
                                Warn($"{node.Id} → 結局{no}「{e.Name}」目前標為無法達成");
                        }
            Console.WriteLine($"地圖→結局連線：{n} 條");
        }

        // ---- 時間與收錄率 ----
        private static int Round(int y, int m, int s) => (y - 1) * 36 + (m - 1) * 3 + s;

        private static void CheckMissionTimes()
        {
            // 1) 地圖節點的時間有沒有落在任務檢查點上（±1 旬容忍）
            var missionRounds = new HashSet<int>(MissionTimes.Keys);
            var unmatched = new List<string>();
            foreach (var n in GuideMapData.Nodes)
                if (!missionRounds.Contains(n.Start) && !missionRounds.Contains(n.Start - 1) && !missionRounds.Contains(n.Start + 1))
                    unmatched.Add($"{n.Id}（{EventGuide.FormatRound(n.Start)}）");
            if (unmatched.Count > 0)
                Console.WriteLine($"節點時間沒有對應任務檢查點（可能由劇本直接排程）：{string.Join("、", unmatched)}");
            else
                Console.WriteLine($"地圖節點時間：{GuideMapData.Nodes.Length} 個節點全部對得上任務檢查點");

            // 2) 遊戲有的任務，攻略收了幾成（只統計可判定的特定／區間時間）
            var events = EventGuideData.All;
            var missing = MissionTimes
                .Where(kv => !events.Any(e => e.Start <= kv.Key && kv.Key <= e.End))
                .OrderBy(kv => kv.Key).ToList();
            Console.WriteLine($"任務收錄率：{MissionTimes.Count - missing.Count}/{MissionTimes.Count} 個任務旬有對應的「本旬事件」條目");
            if (missing.Count > 0)
                Console.WriteLine("  未收錄：" + string.Join("、",
                    missing.Take(15).Select(kv => $"{EventGuide.FormatRound(kv.Key)} {kv.Value}"))
                    + (missing.Count > 15 ? $" …等 {missing.Count} 筆" : ""));
        }

        // ---- 煙霧測試：直接跑攻略的路線判定 ----
        private static void SmokeTest()
        {
            var cases = new (string Name, int Now, Dictionary<string, int> Flags, string Expect)[]
            {
                ("第一年最初", EventGuide.Round(1, 4, 3), new(), "葉氏兄妹"),
                ("留學討論", EventGuide.Round(2, 2, 1), new(), "留學討論"),
                ("崆峒留學後", EventGuide.Round(2, 4, 1), new() { ["M20008"] = 2 }, "江陵決戰"),
                ("離家出走住破廟", EventGuide.Round(2, 7, 1), new() { ["M20028"] = 1 }, "破廟密謀"),
                ("破廟轉青城", EventGuide.Round(2, 8, 3), new() { ["M20028"] = 1, ["M20008"] = 1 }, "青城醒轉"),
                ("眾人的決策", EventGuide.Round(3, 12, 1), new() { ["M70015"] = 4 }, "眾人的決策"),
                ("西武林出使", EventGuide.Round(3, 12, 2), new() { ["M70015"] = 4 }, "出使四派"),
                ("頑抗圍攻", EventGuide.Round(4, 2, 2), new() { ["M70015"] = 2 }, "圍攻戰"),
                ("退隱", EventGuide.Round(4, 1, 3), new() { ["M70015"] = 1 }, "告別唐門"),
            };

            var cacheField = typeof(EventGuide).GetField("flagCache", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var cache = cacheField?.GetValue(null) as System.Collections.IDictionary;
            int pass = 0;
            foreach (var c in cases)
            {
                cache?.Clear();
                MissionManagerData.Flags.Clear();
                StatSet.V.Clear();
                RelSet.V.Clear();
                foreach (var kv in c.Flags) MissionManagerData.Flags[kv.Key] = kv.Value;
                var routes = EventGuide.PlayerRoutes(c.Now);
                var cur = GuideMap.FindCurrent(c.Now, routes);
                if (cur?.Id == c.Expect) pass++;
                else Fail($"煙霧測試「{c.Name}」：目前節點應為 {c.Expect}，實際 {cur?.Id ?? "(無)"}");
            }
            Console.WriteLine($"煙霧測試：{pass}/{cases.Length} 通過");

            // 結局狀態：早期結局過了期限要標成已錯過，無期限的要保持可達成
            var e7 = GuideMap.GetEnding(7);
            var e37 = GuideMap.GetEnding(37);
            var late = EventGuide.Round(4, 6, 1);
            if (GuideMap.StateOf(e7, late, Route.決策線 | Route.留學線) != EndingState.Missed)
                Fail("結局7（南宮壽宴）在第四年應為已錯過");
            if (GuideMap.StateOf(e37, late, Route.頑抗) != EndingState.Open)
                Fail("結局37（唐門薪火）在頑抗線應仍可達成");
        }
    }
}
