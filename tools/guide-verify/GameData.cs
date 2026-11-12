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
            /// <summary>名稱出現在遊戲程式碼（例：戰鬥結果 C00000），可能由程式寫入任何值。</summary>
            public bool Code;
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
            /// <summary>用 AddFlag 累加的旗標：值無法靜態列舉。</summary>
            public List<string> AddFlags = new();
            public List<EndingOpen> Opens = new();
        }

        /// <summary>比較式的一邊：旗標、常數，或其他數值（屬性、好感、心上人等，一律視為可能成立）。</summary>
        public sealed class SwitchValue
        {
            public string Flag;
            public int? Const;
            public string Other;
            public string Text => Flag ?? Const?.ToString() ?? Other ?? "?";
        }

        public sealed class SwitchBranch
        {
            public bool Or;
            public List<(SwitchValue Left, int Op, SwitchValue Right)> Items = new();
        }

        /// <summary>劇情檢查點（checkpointmanager.Switch）：回傳第一個成立的分支編號，全部不成立回傳分支數＋1。</summary>
        public sealed class Switch
        {
            public bool UseCount;
            public List<SwitchBranch> Branches = new();
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
        /// <summary>有劇本用 AddFlag 改動的旗標，值可能是任何數。</summary>
        public static HashSet<string> FlagAnyValue = new();
        /// <summary>劇情檢查點名稱 → 條件（同名可能有多份）；沒有 switches.json 時為空，檢查點條件一律視為可能成立。</summary>
        public static Dictionary<string, List<Switch>> Switches = new();
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
            var switchesFile = Path.Combine(dir, "switches.json");
            if (!File.Exists(flagsFile) || !File.Exists(endingsFile) || !File.Exists(scriptsFile))
                return false;

            using (var doc = JsonDocument.Parse(File.ReadAllText(flagsFile)))
                foreach (var p in doc.RootElement.EnumerateObject())
                {
                    var f = new Flag { Default = p.Value.GetProperty("default").GetInt32(),
                                       Code = p.Value.TryGetProperty("code", out var code) && code.GetBoolean() };
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
                    if (p.Value.TryGetProperty("addFlags", out var adds))
                        foreach (var x in adds.EnumerateArray()) s.AddFlags.Add(x.GetString());
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
            {
                foreach (var p in sc.SetFlags)
                {
                    if (!FlagSetters.TryGetValue(p.Key, out var vs)) FlagSetters[p.Key] = vs = new HashSet<int>();
                    vs.Add(p.Value);
                }
                FlagAnyValue.UnionWith(sc.AddFlags);
            }

            if (File.Exists(switchesFile))
                using (var doc = JsonDocument.Parse(File.ReadAllText(switchesFile)))
                    foreach (var p in doc.RootElement.EnumerateObject())
                    {
                        var list = new List<Switch>();
                        foreach (var sw in p.Value.EnumerateArray())
                        {
                            var x = new Switch { UseCount = sw.GetProperty("useCount").GetBoolean() };
                            foreach (var b in sw.GetProperty("branches").EnumerateArray())
                            {
                                var br = new SwitchBranch { Or = b.GetProperty("or").GetBoolean() };
                                foreach (var it in b.GetProperty("items").EnumerateArray())
                                    br.Items.Add((ReadValue(it[0]), it[1].GetInt32(), ReadValue(it[2])));
                                x.Branches.Add(br);
                            }
                            list.Add(x);
                        }
                        Switches[p.Name] = list;
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

        private static SwitchValue ReadValue(JsonElement e)
        {
            if (e.TryGetProperty("flag", out var f)) return new SwitchValue { Flag = f.GetString() };
            if (e.TryGetProperty("const", out var c)) return new SwitchValue { Const = c.GetInt32() };
            return new SwitchValue { Other = e.TryGetProperty("other", out var o) ? o.GetString() : "?" };
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
            foreach (var n in GuideMapData.Sheets.SelectMany(sh => sh.Nodes))
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

        /// <summary>
        /// 旗標可能出現的值：預設值＋任何劇本 SetFlag 寫入的值。
        /// 用 AddFlag 累加、或由遊戲程式碼寫入的旗標回傳 null（任何值都可能）。
        /// </summary>
        private static HashSet<int> PossibleValues(string flag)
        {
            if (FlagAnyValue.Contains(flag) || (Flags.TryGetValue(flag, out var cf) && cf.Code)) return null;
            var vs = new HashSet<int>();
            if (Flags.TryGetValue(flag, out var f)) vs.Add(f.Default);
            if (FlagSetters.TryGetValue(flag, out var set)) vs.UnionWith(set);
            return vs;
        }

        private static bool Compare(int a, int op, int b) => op switch
        {
            0 => a == b, 1 => a != b, 2 => a < b, 3 => a <= b, 4 => a > b, 5 => a >= b, _ => true,
        };

        /// <summary>「旗標 比較 常數」看旗標的可能值有沒有一個符合；其他組合（屬性、好感、兩個旗標互比）無法靜態判斷，視為可能。</summary>
        private static bool ItemPossible((SwitchValue Left, int Op, SwitchValue Right) it)
        {
            var (l, op, r) = it;
            if (l.Flag != null && r.Const is int c)
                return PossibleValues(l.Flag) is not { } vs || vs.Any(v => Compare(v, op, c));
            if (r.Flag != null && l.Const is int c2)
                return PossibleValues(r.Flag) is not { } vs2 || vs2.Any(v => Compare(c2, op, v));
            if (l.Const is int a && r.Const is int b) return Compare(a, op, b);
            return true;
        }

        private static bool BranchPossible(SwitchBranch b) =>
            b.Items.Count == 0 || (b.Or ? b.Items.Any(ItemPossible) : b.Items.All(ItemPossible));

        /// <summary>
        /// 劇情檢查點走到第 value 個分支的可能性。只檢查該分支本身能否成立（不要求前面的分支都不成立），
        /// 寧可放過也不誤殺；「分支數＋1」是全部不成立時的預設分支，一律視為可能。
        /// </summary>
        private static bool CheckpointPossible(string key, int value)
        {
            if (!Switches.TryGetValue(key, out var list)) return true;   // 不是 Switch（例：玩家選項）或沒有資料
            return list.Any(sw =>
            {
                if (sw.UseCount) return true;
                int n = sw.Branches.Count;
                if (value == n + 1) return true;
                if (value < 1 || value > n + 1) return false;
                return BranchPossible(sw.Branches[value - 1]);
            });
        }

        /// <summary>結局開啟點的分支條件是否可能同時成立：旗標條件要有人寫得出該值，劇情檢查點的分支條件要可能成立。</summary>
        private static bool Satisfiable(EndingOpen o) =>
            o.Guards.All(g => g.Kind switch
            {
                "flag" => PossibleValues(g.Key) is not { } vs || vs.Contains(g.Value),
                "checkpoint" => CheckpointPossible(g.Key, g.Value),
                _ => true,
            });

        /// <summary>說明檢查點分支的條件，例：Ch_8_6_3_2_001 分支3（G11001 ＝ 1）。</summary>
        private static string CheckpointText(string key, int value)
        {
            if (!Switches.TryGetValue(key, out var list) || list[0].UseCount) return null;
            var br = list[0].Branches;
            if (value < 1 || value > br.Count) return null;
            string[] ops = { "＝", "≠", "<", "≦", ">", "≧" };
            return string.Join(br[value - 1].Or ? " 或 " : " 且 ",
                br[value - 1].Items.Select(i => $"{i.Left.Text} {(i.Op is >= 0 and < 6 ? ops[i.Op] : "?")} {i.Right.Text}"));
        }

        /// <summary>列出開啟點裡不可能成立的檢查點條件，方便人工核對。</summary>
        private static string WhyNot(IEnumerable<EndingOpen> opens)
        {
            var why = opens.SelectMany(o => o.Guards)
                           .Where(g => g.Kind == "checkpoint" && !CheckpointPossible(g.Key, g.Value))
                           .Select(g => CheckpointText(g.Key, g.Value) is { } t ? $"{g.Key} 分支{g.Value}：{t}" : null)
                           .Where(t => t != null).Distinct().Take(2).ToList();
            return why.Count == 0 ? "" : "；不可能成立：" + string.Join("；", why);
        }

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
                    Console.WriteLine($"  結局{e.No}「{e.Name}」：腳本存在但分支條件不可達（{Describe(mine)}{WhyNot(mine)}）→ 與標記一致");
            }
            Console.WriteLine($"結局腳本：{live}/66 的開啟條件可成立；{unreachable} 個只出現在走不到的腳本（僅供參考）");
        }

        private static void CheckEndingTargets()
        {
            int n = 0;
            foreach (var node in GuideMapData.Sheets.SelectMany(sh => sh.Nodes))
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
