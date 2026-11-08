using System;
using System.Linq;
using System.Collections.Generic;
using LegendOfMortal_InternalMod.Core;
class P {
  static int fails;
  static void Check(bool ok, string msg) { if (!ok) { fails++; Console.WriteLine("FAIL: " + msg); } }
  static void Main() {
    var nodes = GuideMapData.Nodes;
    var ids = nodes.Select(n => n.Id).ToList();
    Check(ids.Count == ids.Distinct().Count(), "duplicate ids");
    foreach (var n in nodes) {
      foreach (var t in n.AllTargets) {
        if (GuideMap.IsEnding(t, out int no)) Check(GuideMap.GetEnding(no) != null, $"{n.Id}: unknown ending {t}");
        else Check(GuideMap.Get(t) != null, $"{n.Id}: unknown target {t}");
      }
      Check(n.Lane >= 0 && n.Lane < 5, n.Id + " lane");
      Check(n.Start <= n.End, n.Id + " time");
      foreach (var t in n.AllTargets) { var to = GuideMap.Get(t); if (to != null) Check(to.Row > n.Row, $"{n.Id}->{t} edge must go downward"); }
      if (n.Kind == NodeKind.Branch) Check(n.Options.Length >= 2, n.Id + " branch needs >=2 options");
    }
    // 重疊檢查
    foreach (var g in nodes.GroupBy(n => (n.Row, n.Lane)).Where(g => g.Count() > 1)) Check(false, "overlap " + string.Join(",", g.Select(n => n.Id)));
    // 連線的垂直段不得穿過其他節點
    var occ = nodes.ToDictionary(n => (n.Row, n.Lane));
    foreach (var n in nodes) foreach (var tid in n.AllTargets) { var to = GuideMap.Get(tid); if (to == null) continue;
      for (int r = n.Row + 1; r < to.Row; r++) { if (occ.TryGetValue((r, n.Lane), out var hit)) Check(false, $"edge {n.Id}->{to.Id} crosses {hit.Id}"); }
      if (to.Row - n.Row == 1) { } }
    // 每個節點都要可從起點到達
    var seen = new HashSet<string>(); var q = new Queue<string>(); q.Enqueue(nodes[0].Id);
    while (q.Count > 0) { var id = q.Dequeue(); if (!seen.Add(id)) continue; var n = GuideMap.Get(id); if (n == null) continue; foreach (var t in n.AllTargets) if (!GuideMap.IsEnding(t, out _)) q.Enqueue(t); }
    foreach (var n in nodes) Check(seen.Contains(n.Id), "unreachable " + n.Id);
    // 結局
    Check(GuideEndings.All.Length == 66, "ending count " + GuideEndings.All.Length);
    Check(GuideEndings.All.Select(e => e.No).Distinct().Count() == 66, "ending dup");
    var mapped = new HashSet<int>(nodes.SelectMany(n => n.AllTargets).Select(t => GuideMap.IsEnding(t, out int no) ? no : -1));
    var viaGroup = nodes.Where(n => n.Kind == NodeKind.EndingGroup).SelectMany(n => GuideMap.EndingsOf(n).Select(e => e.No)).ToHashSet();
    Console.WriteLine("地圖直接連到結局: " + string.Join(",", mapped.Where(x => x > 0).OrderBy(x => x)));
    Console.WriteLine("結局群組含結局: " + string.Join(",", viaGroup.OrderBy(x => x)));
    // 狀態與流程模擬
    int now = EventGuide.Round(2, 8, 2);
    var routes = Route.破廟 | Route.青城 | Route.決策線;
    var cur = GuideMap.FindCurrent(now, routes);
    Console.WriteLine("now=2-8-2 current=" + cur?.Id);
    foreach (var n in nodes) { var st = GuideMap.StateOf(n, now, routes); GuideMap.Classify(n.Reqs); foreach (var o in n.Options) { GuideMap.Classify(o.Reqs); GuideMap.IsTaken(o); } }
    // 繪圖不丟例外
    foreach (var type in new[] { UnityEngine.EventType.Layout, UnityEngine.EventType.Repaint }) {
      UnityEngine.Event.current.type = type;
      LegendOfMortal_InternalMod.UI.GuideMapView.DrawMap(now, routes);
      LegendOfMortal_InternalMod.UI.GuideMapView.DrawEndings(now, routes);
    }
    // 各 now 不丟例外
    for (int r = 1; r <= 4*36; r++) { GuideMap.FindCurrent(r, EventGuide.PlayerRoutes(r)); EventGuide.Evaluate(r, true); }
    Console.WriteLine(fails == 0 ? "ALL OK" : fails + " failures");
  }
}
