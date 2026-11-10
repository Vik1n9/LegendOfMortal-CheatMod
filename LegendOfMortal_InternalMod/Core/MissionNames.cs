using System.Collections.Generic;

namespace LegendOfMortal_InternalMod.Core
{
    /// <summary>
    /// 沒有任務說明的支線檢查點 → 活俠傳 wiki 事件名稱（由 tools/game-extract/names 產生，請勿手改）。
    /// 以 wiki 各事件「事件時間」與劇本寫入的旗標比對，只收明顯吻合者；其餘顯示「未收錄的支線」。
    /// </summary>
    public static class MissionNames
    {
        public static readonly Dictionary<string, string> ByCheck = new()
        {
            ["M6002_09_01"] = "告別唐門",
            ["M6002_10_01"] = "告別唐門",
            ["S0019_01"] = "龍湘夜遊",
            ["S0028_01"] = "葉雲舟歸來",
            ["S0202_01"] = "唐錚診斷",
            ["S0503_01"] = "正心修身",
            ["S0603_01"] = "邀請葉雲舟",
            ["S2100_01"] = "唐門段考",
            ["S2101_01"] = "唐門段考",
            ["S2102_01"] = "唐門段考",
            ["S2200_02"] = "崆峒季試",
            ["S2300_01"] = "小梅投靠",
            ["S2300_1_01"] = "小梅投靠",
            ["S2321_01"] = "小梅投靠",
            ["S2500_02"] = "夏侯蘭離去",
            ["S2504_02"] = "雪山之約",
            ["S2913_01"] = "神兵鍛成",
            ["S2917_01"] = "雲裳倒地",
            ["S8500_01"] = "變心夢",
        };
    }
}
