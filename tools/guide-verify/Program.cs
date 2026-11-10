using System;
using System.Linq;
using System.Collections.Generic;
using LegendOfMortal_InternalMod.Core;
class P {
  static int fails;
  static void Check(bool ok, string msg) { if (!ok) { fails++; Console.WriteLine("FAIL: " + msg); } }
  static void Main() {
    var all = GuideMapData.Sheets.SelectMany(s => s.Nodes).ToList();
    var ids = all.Select(n => n.Id).ToList();
    Check(ids.Count == ids.Distinct().Count(), "duplicate ids: " + string.Join(",", ids.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key)));
    Check(GuideMapData.Sheets[0] == GuideMapData.Main, "main sheet must be first");
    foreach (var sheet in GuideMapData.Sheets) CheckSheet(sheet);
    Console.WriteLine("地圖分頁：" + string.Join("、", GuideMapData.Sheets.Select(s => $"{s.Tab}({s.Nodes.Length})")));
    var nodes = GuideMapData.Nodes;
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
    // 繪圖不丟例外（每個分頁都畫一次）
    var view = typeof(LegendOfMortal_InternalMod.UI.GuideMapView);
    var sheetField = view.GetField("sheetIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
    var selField = view.GetField("selected", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
    for (int i = 0; i < GuideMapData.Sheets.Length; i++) {
      sheetField.SetValue(null, i); selField.SetValue(null, null);
      foreach (var type in new[] { UnityEngine.EventType.Layout, UnityEngine.EventType.Repaint }) {
        UnityEngine.Event.current.type = type;
        LegendOfMortal_InternalMod.UI.GuideMapView.DrawMap(now, routes);
      }
      // 每個節點的詳情也畫一次（選項、相關按鈕）
      foreach (var n in GuideMapData.Sheets[i].Nodes) { selField.SetValue(null, n.Id); UnityEngine.Event.current.type = UnityEngine.EventType.Repaint; LegendOfMortal_InternalMod.UI.GuideMapView.DrawMap(now, routes); }
    }
    sheetField.SetValue(null, 0); selField.SetValue(null, null);
    LegendOfMortal_InternalMod.UI.GuideMapView.DrawEndings(now, routes);
    // 各 now 不丟例外
    for (int r = 1; r <= 4*36; r++) { foreach (var s in GuideMapData.Sheets) GuideMap.FindCurrent(s, r, EventGuide.PlayerRoutes(r)); EventGuide.Evaluate(r, true); }
    // 遊戲本體抽出的資料（tools/game-extract/out）：有就比對，沒有就跳過
    var dataDir = GuideVerify.GameData.FindDir();
    if (dataDir != null && GuideVerify.GameData.Load(dataDir))
    {
      Console.WriteLine("-- 遊戲資料比對 --");
      GuideVerify.GameData.Run();
      fails += GuideVerify.GameData.Fails;
    }
    else Console.WriteLine("（找不到 tools/game-extract/out，跳過遊戲資料比對）");
    Console.WriteLine(fails == 0 ? "ALL OK" : fails + " failures");
  }

  /// <summary>單一分頁的版面與連線檢查：去向只能在同分頁、連線向下、不重疊、不穿過節點、皆可由第一個節點到達。</summary>
  static void CheckSheet(MapSheet sheet) {
    var nodes = sheet.Nodes;
    string tag = sheet.Tab + ": ";
    Check(sheet.Sections.Length > 0 && sheet.Sections[0].Row == 0, tag + "first section must start at row 0");
    Check(sheet.Sections.Select(s => s.Row).SequenceEqual(sheet.Sections.Select(s => s.Row).OrderBy(x => x).Distinct()), tag + "section rows must increase");
    Check(sheet.Sections.All(s => s.Lanes.Length == 5), tag + "each section needs 5 lane names");
    foreach (var n in nodes) {
      foreach (var t in n.AllTargets) {
        if (GuideMap.IsEnding(t, out int no)) Check(GuideMap.GetEnding(no) != null, $"{tag}{n.Id}: unknown ending {t}");
        else { var to = GuideMap.Get(t); Check(to != null, $"{tag}{n.Id}: unknown target {t}"); if (to != null) Check(GuideMap.SheetOf(to) == sheet, $"{tag}{n.Id}->{t} target must be on the same sheet (use See)"); }
      }
      foreach (var t in n.See) Check(GuideMap.Get(t) != null, $"{tag}{n.Id}: unknown See {t}");
      Check(n.Lane >= 0 && n.Lane < 5, tag + n.Id + " lane");
      Check(n.Start <= n.End, tag + n.Id + " time");
      foreach (var t in n.AllTargets) { var to = GuideMap.Get(t); if (to != null) Check(to.Row > n.Row, $"{tag}{n.Id}->{t} edge must go downward"); }
      if (n.Kind == NodeKind.Branch) Check(n.Options.Length >= 2, tag + n.Id + " branch needs >=2 options");
    }
    foreach (var g in nodes.GroupBy(n => (n.Row, n.Lane)).Where(g => g.Count() > 1)) Check(false, tag + "overlap " + string.Join(",", g.Select(n => n.Id)));
    var occ = nodes.GroupBy(n => (n.Row, n.Lane)).ToDictionary(g => g.Key, g => g.First());
    foreach (var n in nodes) foreach (var tid in n.AllTargets) { var to = GuideMap.Get(tid); if (to == null) continue;
      for (int r = n.Row + 1; r < to.Row; r++) if (occ.TryGetValue((r, n.Lane), out var hit)) Check(false, $"{tag}edge {n.Id}->{to.Id} crosses {hit.Id}"); }
    var seen = new HashSet<string>(); var q = new Queue<string>(); q.Enqueue(nodes[0].Id);
    while (q.Count > 0) { var id = q.Dequeue(); if (!seen.Add(id)) continue; var n = GuideMap.Get(id); if (n == null) continue; foreach (var t in n.AllTargets) if (!GuideMap.IsEnding(t, out _)) q.Enqueue(t); }
    foreach (var n in nodes) Check(seen.Contains(n.Id), tag + "unreachable " + n.Id);
    // 結緣線的心上人編號要對得上名字
    if (sheet.Lover >= 0) Check(sheet.Lover < LoverReq.Names.Length && sheet.Title.Contains(LoverReq.Names[sheet.Lover]), tag + "lover index/name mismatch");
  }
}
