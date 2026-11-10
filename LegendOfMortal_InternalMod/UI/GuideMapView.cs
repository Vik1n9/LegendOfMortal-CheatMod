using System;
using System.Collections.Generic;
using System.Linq;
using LegendOfMortal_InternalMod.Core;
using UnityEngine;
using S = LegendOfMortal_InternalMod.UI.GameStyleUI;

namespace LegendOfMortal_InternalMod.UI
{
    /// <summary>
    /// 「事件攻略」的路線地圖與結局圖鑑：用 IMGUI 畫出由上而下的時間軸，
    /// 分歧點往下長出多條路，點節點可看選項、條件（即時判定）與去向。
    /// </summary>
    public static class GuideMapView
    {
        // ---- 版面（以 1920x1080 為基準，經 S.Px 縮放） ----
        private const float BandH = 56f, Pitch = 98f, NodeH = 64f, LaneGap = 12f;
        private const int Lanes = 5;

        private static string selected;      // 節點 Id 或 "E<編號>"
        private static string pending;       // 點擊後在下一個 Layout 才套用，避免同一事件內版面不一致
        private static bool locate = true;   // 要求把捲動位置移到目前節點
        private static int sheetIndex;       // 目前顯示的分頁（GuideMapData.Sheets 的索引）
        private static int pendingSheet = -1;
        private static readonly Dictionary<MapSheet, string> lastSelected = new();
        private static bool endingAll;
        private static GUISkin stylesFor;
        private static GUIStyle labelStyle, bandTitle, bandLane, arrowStyle;

        /// <summary>給外層捲動區用：非 null 時把捲動位置設成此值後清除。</summary>
        public static float? ScrollRequest;

        private static readonly Color PaperBranch = Hex(0xF3DFB4);
        private static readonly Color PaperEnding = Hex(0xEBCFC8);
        private static readonly Color Good = Hex(0x2E7D32);

        private static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1f);

        // ================= 路線地圖 =================
        public static void DrawMap(int now, Route routes)
        {
            EnsureStyles();
            var sheets = GuideMapData.Sheets;
            if (Event.current.type == EventType.Layout) ApplyPending(sheets);
            var sheet = sheets[sheetIndex];

            int pick = GUILayout.Toolbar(sheetIndex, sheets.Select(s => s.Tab).ToArray(), S.Tab);
            if (pick != sheetIndex) pendingSheet = pick;

            var current = GuideMap.FindCurrent(sheet, now, routes);
            if (selected == null) selected = current?.Id ?? sheet.Nodes[0].Id;

            GUILayout.Label(sheet.Intro, S.Desc);
            if (sheet.Lover >= 0) BondLine(sheet);
            GUILayout.Label("點選地圖上的方塊查看分歧、路線與條件。" +
                            "金黃底＝分歧點，紅底＝結局群組，朱紅外框＝你現在的位置（或下一步），淡色＝不在你目前的路線上。", S.Desc);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("定位到目前", GUILayout.Width(S.Px(150))) && current != null) { pending = current.Id; locate = true; }
            GUILayout.Label(current != null ? $"{(GuideMap.StateOf(current, now, routes) == NodeState.Current ? "目前" : "下一步")}：<b>{current.Title}</b>（{TimeText(current)}）" : "你的路線上已沒有後續節點", S.RowDesc);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            DrawDetail(now, routes);
            GUILayout.Space(S.Px(8));
            DrawCanvas(sheet, now, routes, current);
        }

        /// <summary>套用上一輪的點擊：切換分頁、或選取節點（節點在別的分頁時一併切過去）。</summary>
        private static void ApplyPending(MapSheet[] sheets)
        {
            if (pendingSheet >= 0 && pendingSheet != sheetIndex)
            {
                lastSelected[sheets[sheetIndex]] = selected;
                sheetIndex = pendingSheet;
                lastSelected.TryGetValue(sheets[sheetIndex], out selected);
                locate = selected == null;
            }
            pendingSheet = -1;
            if (pending == null) return;
            if (GuideMap.Get(pending) is { } n && GuideMap.SheetOf(n) is { } owner && owner != sheets[sheetIndex])
            {
                lastSelected[sheets[sheetIndex]] = selected;
                sheetIndex = Array.IndexOf(sheets, owner);
            }
            selected = pending;
            pending = null;
        }

        private static void BondLine(MapSheet sheet)
        {
            string lover = LoverReq.NameOf(LoverReq.Current);
            bool isLover = LoverReq.Current == sheet.Lover;
            string bond;
            if (sheet.Bond == null) bond = RichText.Gray("目前版本無法結緣");
            else
            {
                bool? ok;
                try { ok = sheet.Bond.Check(); } catch { ok = null; }
                bond = ok == true ? RichText.Green("已結緣") : ok == false ? RichText.Red("尚未結緣") : RichText.Gray("無法判定");
            }
            string heart = isLover ? RichText.Green("心上人：" + lover) : RichText.Red("心上人：" + lover + "（不是" + LoverReq.Names[sheet.Lover] + "）");
            GUILayout.Label(OnPaper($"{sheet.Title}　·　{bond}　·　{heart}"), S.Desc);
        }

        private static void DrawCanvas(MapSheet sheet, int now, Route routes, MapNode current)
        {
            var nodes = sheet.Nodes;
            var secs = sheet.Sections;
            int maxRow = nodes.Max(n => n.Row);
            float totalH = S.Px(BandH) * secs.Length + S.Px(Pitch) * (maxRow + 1);
            var area = GUILayoutUtility.GetRect(1, totalH, GUILayout.ExpandWidth(true));

            float laneW = area.width / Lanes;
            Rect NodeRect(MapNode n)
            {
                int s = SectionOf(secs, n);
                float y = area.y + S.Px(BandH) * (s + 1) + S.Px(Pitch) * n.Row + (S.Px(Pitch) - S.Px(NodeH)) / 2f;
                return new Rect(area.x + laneW * n.Lane + S.Px(LaneGap) / 2f, y, laneW - S.Px(LaneGap), S.Px(NodeH));
            }

            float BandTop(int s) => area.y + S.Px(BandH) * s + S.Px(Pitch) * secs[s].Row;

            bool repaint = Event.current.type == EventType.Repaint;
            if (repaint)
            {
                DrawEdges(secs, nodes, NodeRect, BandTop, now, routes);
                for (int s = 0; s < secs.Length; s++) DrawBand(secs[s], s, area, laneW);
            }

            foreach (var n in nodes)
            {
                var r = NodeRect(n);
                var st = GuideMap.StateOf(n, now, routes);
                if (repaint) DrawNode(n, r, st, n == current);
                if (GUI.Button(r, GUIContent.none, GUIStyle.none)) pending = n.Id;
                if (locate && n == current && repaint)
                {
                    ScrollRequest = Mathf.Max(0, r.y - S.Px(330));
                    locate = false;
                }
            }
        }

        private static int SectionOf(MapSection[] secs, MapNode n)
        {
            int s = 0;
            for (int i = 0; i < secs.Length; i++) if (secs[i].Row <= n.Row) s = i;
            return s;
        }

        private static void DrawBand(MapSection sec, int index, Rect area, float laneW)
        {
            float y = area.y + S.Px(BandH) * index + S.Px(Pitch) * sec.Row;
            var r = new Rect(area.x, y, area.width, S.Px(BandH));
            Fill(r, S.PaperDark);
            Fill(new Rect(r.x, r.yMax - 2, r.width, 2), new Color(S.Vermilion.r, S.Vermilion.g, S.Vermilion.b, 0.6f));
            GUI.Label(new Rect(r.x + S.Px(10), r.y + S.Px(2), r.width - S.Px(20), S.Px(28)), sec.Title, bandTitle);
            for (int i = 0; i < Lanes && i < sec.Lanes.Length; i++)
                if (!string.IsNullOrEmpty(sec.Lanes[i]))
                    GUI.Label(new Rect(area.x + laneW * i, r.y + S.Px(30), laneW, S.Px(22)), sec.Lanes[i], bandLane);
        }

        private static void DrawEdges(MapSection[] secs, MapNode[] nodes, Func<MapNode, Rect> rectOf, Func<int, float> bandTop, int now, Route routes)
        {
            foreach (var from in nodes)
            {
                var taken = GuideMap.TakenOption(from);
                var fr = rectOf(from);
                foreach (var tid in from.AllTargets.Distinct())
                {
                    var to = GuideMap.Get(tid);
                    if (to == null || !nodes.Contains(to)) continue;
                    var tr = rectOf(to);
                    bool onTaken = taken != null && taken.To.Contains(tid);
                    bool touch = selected == from.Id || selected == to.Id;
                    bool off = (to.Route & routes) == 0 || (from.Route & routes) == 0;
                    Color c = onTaken ? S.Vermilion : touch ? S.Ink : new Color(S.Frame.r, S.Frame.g, S.Frame.b, off ? 0.22f : 0.6f);
                    float w = onTaken || touch ? S.Px(3) : S.Px(2);
                    // 跨到下一段時在段落標題上方匯流，標題底下只留一支箭頭，不在標題線下擠成一團
                    int ts = SectionOf(secs, to);
                    float? merge = ts != SectionOf(secs, from) ? bandTop(ts) - S.Px(8) : (float?)null;
                    DrawEdge(fr, tr, c, w, merge);
                }
            }
        }

        private static void DrawEdge(Rect a, Rect b, Color c, float w, float? merge = null)
        {
            float x1 = a.center.x, y1 = a.yMax, x2 = b.center.x, y2 = b.y;
            float ym = merge ?? y2 - S.Px(14);
            Fill(new Rect(x1 - w / 2, y1, w, Mathf.Max(0, ym - y1)), c);
            if (Mathf.Abs(x2 - x1) > 0.5f) Fill(new Rect(Mathf.Min(x1, x2) - w / 2, ym - w / 2, Mathf.Abs(x2 - x1) + w, w), c);
            Fill(new Rect(x2 - w / 2, ym, w, Mathf.Max(0, y2 - ym - S.Px(5))), c);
            var old = GUI.color;
            GUI.color = c;
            GUI.Label(new Rect(x2 - S.Px(10), y2 - S.Px(14), S.Px(20), S.Px(14)), "▼", arrowStyle);
            GUI.color = old;
        }

        private static void DrawNode(MapNode n, Rect r, NodeState st, bool isCurrent)
        {
            Color fill = n.Kind == NodeKind.Branch ? PaperBranch : n.Kind == NodeKind.EndingGroup ? PaperEnding : S.Paper;
            float alpha = st == NodeState.OffRoute ? 0.4f : st == NodeState.Past ? 0.72f : 1f;
            if (st == NodeState.Past) fill = Color.Lerp(fill, new Color(0.62f, 0.6f, 0.57f), 0.4f);
            bool sel = selected == n.Id;
            Color border = isCurrent ? S.Vermilion : sel ? S.Ink : new Color(S.Frame.r, S.Frame.g, S.Frame.b, 0.7f);
            float bw = isCurrent || sel ? S.Px(3) : S.Px(1.5f);
            Fill(r, new Color(border.r, border.g, border.b, border.a * alpha));
            Fill(new Rect(r.x + bw, r.y + bw, r.width - bw * 2, r.height - bw * 2), new Color(fill.r, fill.g, fill.b, alpha));

            string sub = SubText(n);
            // 標題與日期區間（例：一年四月下旬～九月上旬）比格子寬時縮小字級，避免折行後被裁掉
            int titleSize = FitSize(n.Title, r.width, S.Px(15), S.Px(11));
            int subSize = FitSize(sub, r.width, S.Px(12), S.Px(9));
            var old = GUI.color;
            GUI.color = new Color(1, 1, 1, alpha);
            GUI.Label(new Rect(r.x + S.Px(4), r.y, r.width - S.Px(8), r.height),
                      $"<size={titleSize}><b>{n.Title}</b></size>\n<size={subSize}><color=#6B5D50>{sub}</color></size>", labelStyle);
            GUI.color = old;
            if (isCurrent)
            {
                // 標籤掛在外框右上角上方，不壓到節點標題
                string tag = st == NodeState.Current ? "目前" : "下一步";
                float tw = S.Px(14) * tag.Length + S.Px(10), th = S.Px(16);
                var tr = new Rect(r.xMax - tw, r.y - th + S.Px(1), tw, th);
                Fill(tr, S.Vermilion);
                GUI.Label(tr, "<b>" + tag + "</b>", arrowStyle);
            }
        }

        /// <summary>全形字約一字一個字級寬：放不下就縮小，但不小於 min。</summary>
        private static int FitSize(string text, float width, int size, int min)
        {
            int len = System.Text.RegularExpressions.Regex.Replace(text ?? "", "<[^>]+>", "").Length;
            return len == 0 ? size : Mathf.Max(min, Mathf.Min(size, Mathf.FloorToInt((width - S.Px(10)) / len)));
        }

        private static string SubText(MapNode n)
        {
            if (n.Kind == NodeKind.Branch)
            {
                var taken = GuideMap.TakenOption(n);
                if (taken != null) return "<color=#2E7D32>✔ " + Clip(taken.Label, 9) + "</color>";
                return "◆分歧　" + n.Options.Length + " 條路";
            }
            if (n.Kind == NodeKind.EndingGroup) return "◆結局群組";
            return TimeText(n);
        }

        // ================= 詳情 =================
        private static void DrawDetail(int now, Route routes)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            try
            {
                if (GuideMap.IsEnding(selected, out int no))
                {
                    var e = GuideMap.GetEnding(no);
                    if (e != null) EndingCard(e, now, routes, false);
                    else GUILayout.Label("找不到結局 " + no, S.Desc);
                    return;
                }
                var n = GuideMap.Get(selected);
                if (n == null) { GUILayout.Label("請點選地圖上的方塊。", S.Desc); return; }
                NodeDetail(n, now, routes);
            }
            finally { GUILayout.EndVertical(); }
        }

        private static void NodeDetail(MapNode n, int now, Route routes)
        {
            var st = GuideMap.StateOf(n, now, routes);
            string kind = n.Kind == NodeKind.Branch ? "◆分歧點" : n.Kind == NodeKind.EndingGroup ? "◆結局群組" : "事件";
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{n.Title}　<size={S.Px(15)}><color=#6B5D50>{kind}</color></size>", S.Entry);
            GUILayout.FlexibleSpace();
            GUILayout.Label(StateText(st), S.Desc);
            GUILayout.EndHorizontal();

            GUILayout.Label(OnPaper($"時間：{TimeText(n)}　·　路線：{RouteMask(n.Route)}"), S.Desc);
            if (!string.IsNullOrEmpty(n.Where)) GUILayout.Label(OnPaper(n.Where), S.Desc);
            if (n.Kind != NodeKind.EndingGroup) ReqLines(n.Reqs, n.Kind == NodeKind.Branch ? "相關狀態" : "條件");
            if (!string.IsNullOrEmpty(n.Note)) GUILayout.Label(OnPaper(RichText.Gray("備註：" + n.Note)), S.Desc);

            if (n.Kind == NodeKind.EndingGroup)
            {
                foreach (var e in GuideMap.EndingsOf(n)) EndingRow(e, now, routes);
                return;
            }
            foreach (var o in n.Options) OptionCard(o);
            TargetRow("接下來：", n.Next);
            TargetRow("相關：", n.See);
        }

        private static void TargetRow(string title, string[] ids)
        {
            if (ids.Length == 0) return;
            GUILayout.BeginHorizontal();
            GUILayout.Label(title, S.Row, GUILayout.Width(S.Px(90)));
            foreach (var id in ids) TargetButton(id);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private static void OptionCard(MapOption o)
        {
            GUILayout.Space(S.Px(6));
            GUILayout.BeginVertical(GUI.skin.box);
            bool? taken = GuideMap.IsTaken(o);
            string mark = taken == true ? OnPaper(RichText.Green("✔ 已選　")) : "";
            GUILayout.Label(mark + "▸ " + o.Label, S.Entry);
            if (!string.IsNullOrEmpty(o.Result)) GUILayout.Label(OnPaper(o.Result), S.Desc);
            ReqLines(o.Reqs, null);
            if (o.To.Length > 0)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("去向：", S.Row, GUILayout.Width(S.Px(70)));
                GUILayout.BeginVertical();
                float used = 0, max = S.Px(560);
                GUILayout.BeginHorizontal();
                foreach (var id in o.To)
                {
                    float w = TargetWidth(id);
                    if (used > 0 && used + w > max)
                    {
                        GUILayout.FlexibleSpace(); GUILayout.EndHorizontal();
                        GUILayout.BeginHorizontal(); used = 0;
                    }
                    TargetButton(id);
                    used += w + S.Px(6);
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
        }

        private static string TargetLabel(string id)
        {
            if (GuideMap.IsEnding(id, out int no)) return "結局" + no + (GuideMap.GetEnding(no) is { } e ? " " + e.Name : "");
            var n = GuideMap.Get(id);
            if (n == null) return id;
            // 連到別的分頁時標出分頁名稱
            var owner = GuideMap.SheetOf(n);
            return owner != null && owner != GuideMapData.Sheets[sheetIndex] ? owner.Tab + "：" + n.Title : n.Title;
        }

        private static float TargetWidth(string id) => S.Px(34 + 21 * TargetLabel(id).Length);

        private static void TargetButton(string id)
        {
            string label = TargetLabel(id);
            if (GUILayout.Button(label, GUILayout.Width(TargetWidth(id)))) pending = id;
        }

        private static void ReqLines(Req[] reqs, string title)
        {
            var res = GuideMap.Classify(reqs);
            if (!res.Any) return;
            string prefix = title == null ? "" : title + "：";
            if (res.Missing.Count > 0) GUILayout.Label(OnPaper(RichText.Red(prefix + "缺 " + string.Join("、", res.Missing))), S.Desc);
            if (res.Met.Count > 0) GUILayout.Label(OnPaper(RichText.Green((res.Missing.Count > 0 || title == null ? "" : prefix) + "已達 " + string.Join("、", res.Met))), S.Desc);
            if (res.Notes.Count > 0) GUILayout.Label(OnPaper(RichText.Gray("另需 " + string.Join("、", res.Notes))), S.Desc);
        }

        // ================= 結局 =================
        private static void EndingRow(Ending e, int now, Route routes)
        {
            var st = GuideMap.StateOf(e, now, routes);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"結局{e.No} {e.Name}", GUILayout.Width(S.Px(330)))) pending = "E" + e.No;
            GUILayout.Label(EndingStateText(st), S.RowDesc);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private static void EndingCard(Ending e, int now, Route routes, bool boxed)
        {
            var st = GuideMap.StateOf(e, now, routes);
            if (boxed) GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"汗青書 {e.No}　{e.Name}", S.Entry);
            GUILayout.FlexibleSpace();
            GUILayout.Label(EndingStateText(st), S.Desc);
            GUILayout.EndHorizontal();
            string tags = string.IsNullOrEmpty(e.Tags) ? "" : "【" + e.Tags.Replace("、", "】【") + "】";
            GUILayout.Label(OnPaper($"路線：{RouteMask(e.Route)}　{tags}"), S.Desc);
            if (!string.IsNullOrEmpty(e.Cond)) GUILayout.Label(OnPaper(e.Cond), S.Desc);
            else if (e.Unavailable) GUILayout.Label(OnPaper(RichText.Gray("wiki 標示目前版本無法達成。")), S.Desc);
            ReqLines(e.Reqs, "條件");
            if (e.Deadline != int.MaxValue) GUILayout.Label(OnPaper(RichText.Gray("最晚決定時間：" + EventGuide.FormatRound(e.Deadline))), S.Desc);
            if (boxed) { GUILayout.EndVertical(); GUILayout.Space(S.Px(6)); }
        }

        public static void DrawEndings(int now, Route routes)
        {
            EnsureStyles();
            GUILayout.Label("依 wiki〈汗青書〉整理。已錯過或不在你路線上的結局預設隱藏；結局群組與條件請搭配「路線地圖」查看。", S.Desc);
            bool all = GUILayout.Toolbar(endingAll ? 1 : 0, new[] { "仍可達成", "全部" }, GUILayout.Width(S.Px(260))) == 1;
            endingAll = all;
            int shown = 0;
            foreach (var e in GuideEndings.All)
            {
                var st = GuideMap.StateOf(e, now, routes);
                if (!endingAll && st != EndingState.Open) continue;
                EndingCard(e, now, routes, true);
                shown++;
            }
            if (shown == 0) GUILayout.Label("目前沒有對應的結局", S.Desc);
        }

        // ================= 小工具 =================
        private static string StateText(NodeState s) => OnPaper(s switch
        {
            NodeState.Current => RichText.Green("▶ 現在／即將發生"),
            NodeState.Past => RichText.Gray("已過"),
            NodeState.OffRoute => RichText.Red("不在你目前的路線上"),
            _ => RichText.Gray("尚未到"),
        });

        private static string EndingStateText(EndingState s) => OnPaper(s switch
        {
            EndingState.Open => RichText.Green("仍有機會"),
            EndingState.WrongRoute => RichText.Red("不在你的路線"),
            EndingState.Missed => RichText.Red("已錯過"),
            _ => RichText.Gray("目前版本無法達成"),
        });

        private static string OnPaper(string s) => CheatWindow.OnPaper(s);

        public static string RouteMask(Route r)
        {
            if ((r & Route.Any) == Route.Any) return "任何路線";
            var parts = new List<string>();
            var y2 = r & Route.留學線;
            if (y2 != 0 && y2 != Route.留學線)
            {
                if ((y2 & Route.崆峒) != 0) parts.Add("崆峒留學");
                if ((y2 & Route.客棧) != 0) parts.Add("不留學・客棧");
                if ((y2 & Route.破廟) != 0) parts.Add("不留學・破廟");
                if ((y2 & Route.青城) != 0) parts.Add("青城留學");
            }
            var end = r & Route.決策線;
            if (end != 0 && end != Route.決策線)
            {
                if ((end & Route.退隱) != 0) parts.Add("退隱");
                if ((end & Route.頑抗) != 0) parts.Add("頑抗到底");
                if ((end & Route.西武林) != 0) parts.Add("西武林盟");
            }
            return parts.Count > 0 ? string.Join("／", parts) : "任何路線";
        }

        private static string TimeText(MapNode n)
        {
            if (n.Start == n.End) return EventGuide.FormatRoundShort(n.Start);
            if (n.End == int.MaxValue) return EventGuide.FormatRoundShort(n.Start) + "起";
            string a = EventGuide.FormatRoundShort(n.Start), b = EventGuide.FormatRoundShort(n.End);
            int idx = b.IndexOf('年');
            if (idx > 0 && a.StartsWith(b.Substring(0, idx + 1))) b = b.Substring(idx + 1);
            return a + "～" + b;
        }

        private static string Clip(string s, int n) => s.Length <= n ? s : s.Substring(0, n) + "…";

        private static void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        private static void EnsureStyles()
        {
            if (labelStyle != null && stylesFor == S.Skin) return;
            stylesFor = S.Skin;
            labelStyle = new GUIStyle(S.Label)
            {
                fontSize = S.Px(15), alignment = TextAnchor.MiddleCenter, wordWrap = true, richText = true, clipping = TextClipping.Clip,
                padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0),
            };
            bandTitle = new GUIStyle(S.Label)
            {
                fontSize = S.Px(18), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, wordWrap = false, richText = true,
                padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0),
            };
            bandLane = new GUIStyle(S.Label)
            {
                fontSize = S.Px(14), alignment = TextAnchor.MiddleCenter, wordWrap = false, richText = true,
                padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0),
            };
            bandLane.normal.textColor = S.InkSoft;
            arrowStyle = new GUIStyle(S.Label)
            {
                fontSize = S.Px(12), alignment = TextAnchor.MiddleCenter, wordWrap = false, richText = true,
                padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0),
            };
            arrowStyle.normal.textColor = Color.white;
        }
    }
}
