using static LegendOfMortal_InternalMod.Core.Route;
using static LegendOfMortal_InternalMod.Core.EventGuideData;
using R = Mortal.Core.RelationshipStatType;
using G = Mortal.Core.GameStatType;

namespace LegendOfMortal_InternalMod.Core
{
    /// <summary>
    /// 路線地圖的角色分頁：每位心上人一張地圖，中間欄（Lane 2）是結緣必要條件，
    /// 兩側是不影響結緣、但容易錯過的分支事件。
    /// 來源：活俠傳 wiki 各角色〈結緣攻略〉與事件頁、4Gamers〈崆峒神威更新攻略〉；
    /// 小竹「秘密武器」等崆峒神威新增分支另以遊戲劇本（ch7_3_2_5、back_03_502）對照。
    /// 版面規則同主線：連線一律向下，側欄節點放在來源節點的下一列，避免連線穿過中間欄。
    /// </summary>
    public static partial class GuideMapData
    {
        private static readonly string[] 必要 = { "", "", "結緣必要", "", "" };

        private static MapSheet[] romances;
        public static MapSheet[] Romances => romances ??= new[] { 小竹(), 小梅(), 小師妹(), 葉雲裳(), 夏侯蘭(), 龍湘(), 上官螢(), 魏菊(), 瑞杏() };

        // 幾條結緣線共用的分歧
        private static MapNode 臥雲崗(string id, int row, string next) => new MapNode
        {
            Id = id, Title = "臥雲崗・大師兄重傷", Kind = NodeKind.Branch, Lane = 2, Row = row,
            Start = T(3, 1, 2), End = T(3, 1, 2), Where = "第三年一月中旬",
            See = To("臥雲崗"),
            Options = new[]
            {
                O("留守山上", "之後「大師兄重傷」時必須成功進入治療大師兄的階段（治療成敗不影響）；留守線有較多結緣對象的劇情。", To(next)),
                O("陪大師兄下山", "沒有特別需求。", To(next)),
            },
        };

        private static MapOption 決策退隱(string result, string[] endings, Req bond) =>
            O("決策：退隱江湖（遣散唐門）", result, endings, Rq(bond), FlagIs("M70015", 1, "決策＝退隱江湖"));

        private static MapOption 決策頑抗(string result, string[] to) =>
            O("決策：戰他娘親（頑抗到底）", result, to, null,
              Either(FlagIs("M70015", 2, "決策＝戰他娘親"), FlagIs("M70015", 3, "決策＝響應武林盟（同頑抗線）")));

        private static MapOption 決策西武林(string result, string[] to) =>
            O("決策：成立西武林盟", result, to, null, FlagIs("M70015", 4, "決策＝西武林盟"));

        // ================= 小竹（郁竹） =================
        private static MapSheet 小竹() => new MapSheet
        {
            Tab = "小竹", Title = "郁竹（小竹）結緣線", Lover = 6, Bond = Flag("G11006", "與郁竹結緣"),
            Intro = "崆峒神威更新開放的結緣線：崆峒留學期間在鐵拳巷培養感情、江陵後收留她回唐門、第三年五月再赴崆峒完成結緣。" +
                    "兩側是打造相關的分支：逆鱗寶衣、虎戰車、老虎戰衣（小竹的秘密武器）等。",
            Sections = new[]
            {
                new MapSection { Row = 0, Title = "第一年　前置", Lanes = new[] { "", "", "大師兄的塗鴉", "", "" } },
                new MapSection { Row = 1, Title = "第二年　崆峒留學（三月～十月）", Lanes = new[] { "逆鱗寶衣", "竹弟的木劍", "結緣必要", "神兵帖・虎戰車", "大鐵鋪遭竊" } },
                new MapSection { Row = 5, Title = "第二年十月～第三年四月　唐門", Lanes = new[] { "教小竹武功", "", "結緣必要", "", "四師兄離去" } },
                new MapSection { Row = 9, Title = "第三年五月～十月　崆峒爭奪戰", Lanes = new[] { "", "解屍心丹毒", "結緣必要", "仙令的真相", "搥背券" } },
                new MapSection { Row = 11, Title = "第三年十二月起　鐵鋪事件與終局", Lanes = new[] { "老虎戰衣", "", "結緣必要", "", "西武林盟" } },
            },
            Nodes = new[]
            {
                new MapNode
                {
                    Id = "竹_塗鴉", Title = "大師兄找樂子", Kind = NodeKind.Branch, Lane = 2, Row = 0,
                    Start = T(1, 4, 1), End = T(1, 5, 2), Where = "第一年第一次段考前，心相低時回男弟子房遇到大師兄",
                    Reqs = Rq(心相(Op.Le, 30)),
                    Note = "只影響逆鱗寶衣，與結緣無關。天運可用「骰子選項」頁指定點數。",
                    Options = new[]
                    {
                        O("天運 >60「琴棋書畫」", "大師兄談起南宮肩甲，不論回答哪一句都會拿到貴重品《大師兄的塗鴉》，之後才能和小竹開發逆鱗寶衣。", To("竹_留學")),
                        O("其他天運結果", "鬧小師妹或下山搶賊，拿不到塗鴉（不影響結緣）。", To("竹_留學")),
                    },
                },
                new MapNode
                {
                    Id = "竹_留學", Title = "留學討論・門派", Kind = NodeKind.Branch, Lane = 2, Row = 1,
                    Start = T(2, 2, 1), End = T(2, 3, 1), Where = "第二年二月上旬唐門月會 → 三月上旬崆峒門派",
                    Reqs = Rq(貢獻度(Op.Ge, 50)),
                    See = To("留學討論", "崆峒留學"),
                    Options = new[]
                    {
                        O("崆峒留學，進鐵拳門", "鐵拳巷的鍛造互動最多，逆鱗寶衣也要用鐵拳門的打鐵筆記。", To("竹_返鄉"), null, FlagIs("M20011", 4, "崆峒門派＝鐵拳門")),
                        O("崆峒留學，其他門派", "門派不是結緣必要條件，只是小竹的劇情比較少。", To("竹_返鄉")),
                        O("「退席」不留學", "沒有崆峒留學就遇不到小竹，結緣線無法開始。"),
                    },
                    Next = To("竹_逆鱗", "竹_遭竊"),
                },
                new MapNode
                {
                    Id = "竹_逆鱗", Title = "開發逆鱗寶衣", Kind = NodeKind.Event, Lane = 0, Row = 2, Route = 崆峒,
                    Start = T(2, 3, 1), End = T(2, 7, 3), Where = "崆峒留學期間，鐵拳巷製作、販售玩具時觸發",
                    Reqs = Rq(FlagIs("M20011", 4, "崆峒門派＝鐵拳門"), Note("持有《大師兄的塗鴉》"), Note("入門取得的打鐵筆記練到 10 級")),
                    Note = "和小竹把大師兄的塗鴉改成可行的圖紙；拿到圖紙後，防具「輕量化」可再升級為逆鱗寶衣（受攻擊時自動發射暗器反擊）。不影響結緣。",
                },
                new MapNode
                {
                    Id = "竹_遭竊", Title = "大鐵鋪遭竊（顧店）", Kind = NodeKind.Branch, Lane = 4, Row = 2, Route = 崆峒,
                    Start = T(2, 3, 3), End = T(2, 3, 3), Where = "第二年三月下旬，造訪鐵拳巷≥3 次後點選「鐵拳巷」",
                    Note = "追哪個犯人只影響好感與後續小事件，不影響結緣。",
                    Options = new[]
                    {
                        O("「追左邊那個」", "一般結果。"),
                        O("「追中間那個」", "接一系列入獄事件，可加深瑞笙好感。"),
                        O("「追右邊那個」", "逮到南宮淺，有助南宮淺好感（與入獄事件互斥）；南宮淺好感也是崆峒探險隊的條件。"),
                    },
                },
                new MapNode
                {
                    Id = "竹_返鄉", Title = "郁竹返家準備・返家", Kind = NodeKind.Branch, Lane = 2, Row = 2, Route = 崆峒,
                    Start = T(2, 4, 1), End = T(2, 6, 3), Where = "第二年四月上～下旬，或五月下旬～六月下旬；一旬後自動接「郁竹返家」",
                    Reqs = Rq(Flag("E21004", "已完成郁竹返家")),
                    Note = "陪小竹返鄉完成家庭／身世劇情，是結緣的第一個關卡。建議走郁竹版本。",
                    Options = new[]
                    {
                        O("郁竹觸發：答應扮演家丁「好吧」", "行動次數≥2 時點「鐵拳巷」觸發。", To("竹_馳援"),
                          Rq(Rel(R.郁竹, Op.Ge, 20), 行動次數(Op.Ge, 2), Note("造訪鐵拳巷≥4 次"))),
                        O("小梅觸發：跟小梅跑步 →「追上去看看」", "門派修練時隨機遇到「小梅跑步」，余公子被帶走後追上去，並打贏對決。", To("竹_馳援"),
                          Rq(Flag("G10006", "認識郁竹"), Rel(R.虞小梅, Op.Ge, 20))),
                    },
                    Next = To("竹_木劍", "竹_神兵"),
                },
                new MapNode
                {
                    Id = "竹_木劍", Title = "竹弟討禮物：木劍", Kind = NodeKind.Event, Lane = 1, Row = 3, Route = 崆峒,
                    Start = T(2, 4, 2), End = T(2, 6, 3), Where = "郁竹返家，弟弟討要禮物時",
                    Reqs = Rq(Flag("M10001", "曾收留葉氏兄妹"), Flag("M00015", "葉雲舟雕過木劍")),
                    Note = "木劍來自外堡葉雲舟的雕刻互動，要在崆峒留學前完成。前兩把會被嫌棄，第三把較好看的劍趙活厚臉皮要來，才能讓竹弟滿意。不影響結緣。",
                },
                new MapNode
                {
                    Id = "竹_神兵", Title = "神兵帖・神兵鍛成", Kind = NodeKind.Branch, Lane = 3, Row = 3, Route = 崆峒,
                    Start = T(2, 5, 3), End = T(2, 8, 1), Where = "五月下旬神兵帖系列事件 → 八月上旬「神兵鍛成」擲天命",
                    Reqs = Rq(Note("神兵帖進度達 2 或 3")),
                    Note = "天命可用「骰子選項」頁指定點數。不影響結緣。",
                    Options = new[]
                    {
                        O("天命 <33「一柄寶刀」", "發現六戒刀。"),
                        O("天命 33～65「未公開的兵器圖紙」", "獲得虎戰車圖紙，江陵可和小竹組裝虎戰車。", To("竹_虎戰車")),
                        O("天命 ≥66「滯銷的發明」", "沒有圖紙。"),
                    },
                },
                new MapNode
                {
                    Id = "竹_馳援", Title = "馳援南宮（離開崆峒）", Kind = NodeKind.Event, Lane = 2, Row = 3, Route = 崆峒,
                    Start = T(2, 8, 1), End = T(2, 8, 1), Where = "第二年八月上旬，崆峒留學結束",
                    Reqs = Rq(Rel(R.郁竹, Op.Ge, 30), Lover(6)),
                    Note = "離開崆峒前郁竹好感至少 30（建議 40），並把心上人定為郁竹，之後很難再拿到變心點。" +
                           "好感不足時：江陵圍城期間「無所事事」觸發「郁竹在江陵」，崆峒派抵江陵後「上街消遣」可隨機遇到郁竹。",
                    See = To("江陵決戰"),
                    Next = To("竹_拋棄", "竹_虎戰車"),
                },
                new MapNode
                {
                    Id = "竹_虎戰車", Title = "組裝虎戰車", Kind = NodeKind.Event, Lane = 3, Row = 4, Route = 崆峒,
                    Start = T(2, 9, 2), End = T(2, 9, 3), Where = "江陵圍城期間，接續「重逢郁竹」",
                    Reqs = Rq(Note("持有虎戰車圖紙"), Flag("M30076", "已製作虎戰車")),
                    Note = "和小竹、南宮淺借南宮別院組裝能噴火、自走的虎戰車，江陵大戰會參戰；唐門圍攻戰另有加強版二號機登場。不影響結緣。",
                },
                new MapNode
                {
                    Id = "竹_拋棄", Title = "江陵決戰前夜・南宮訃音", Kind = NodeKind.Branch, Lane = 2, Row = 4, Route = 崆峒,
                    Start = T(2, 10, 1), End = T(2, 10, 1), Where = "第二年十月上旬",
                    Reqs = Rq(Lover(6), Rel(R.郁竹, Op.Ge, 40)),
                    Note = "另有「不可與小梅前往襄陽」的條件，心上人不是虞小梅時必定成立。",
                    See = To("江陵決戰", "小梅投靠"),
                    Options = new[]
                    {
                        O("心上人為郁竹且好感≥40", "南宮訃音時觸發「郁竹被拋棄」，接著自動「收留郁竹」，她隨你回唐門。", To("竹_唐門"),
                          Rq(Lover(6), Rel(R.郁竹, Op.Ge, 40)), Flag("E13004", "已收留郁竹")),
                        O("條件不足，或跟小梅去襄陽", "郁竹留在崆峒，結緣線中斷。"),
                    },
                },

                // ---- 唐門 ----
                new MapNode
                {
                    Id = "竹_唐門", Title = "郁竹在唐門", Kind = NodeKind.Event, Lane = 2, Row = 5,
                    Start = T(2, 10, 2), End = T(3, 1, 1), Where = "第二年十月中旬起：鍛冶場升級、郁竹學武、郁竹受歡迎、竹之心",
                    Reqs = Rq(Flag("E12004", "郁竹在唐門")),
                    Note = "除了打鐵鋪，後山也能觸發和小竹練功的事件。",
                    Next = To("竹_臥雲", "竹_教武"),
                },
                new MapNode
                {
                    Id = "竹_教武", Title = "後山：小竹請你點撥武功", Kind = NodeKind.Branch, Lane = 0, Row = 6,
                    Start = T(2, 10, 2), End = T(3, 11, 3), Where = "郁竹在唐門時，後山練功隨機出現",
                    Reqs = Rq(Flag("E12004", "郁竹在唐門")),
                    Note = "第三年十二月鐵鋪事件能不能做出老虎戰衣，只看這裡有沒有選過「來吧」（遊戲旗標 M00045「小竹學武功」）。選一次就夠了。",
                    Options = new[]
                    {
                        O("「來吧」", "郁竹好感+1，但你自己的武學進境會耽擱。", To("竹_鐵鋪"), null, Flag("M00045", "教過小竹武功")),
                        O("「我也不能耽誤自己進境」", "她扛起斧頭去砍柴。沒教過她武功，之後就打造不出老虎戰衣。"),
                    },
                },
                臥雲崗("竹_臥雲", 6, "竹_辭別"),
                new MapNode
                {
                    Id = "竹_辭別", Title = "郁竹的家書・梅竹辭別", Kind = NodeKind.Branch, Lane = 2, Row = 7,
                    Start = T(3, 2, 1), End = T(3, 2, 1), Where = "第三年二月上旬",
                    Reqs = Rq(Flag("E13004", "曾收留郁竹")),
                    Note = "大師兄相關事件後她收到信件返回崆峒，這是正常流程，不是攻略失敗。",
                    Options = new[]
                    {
                        O("讓她回崆峒", "第三年五月金烏來襲後可再赴崆峒。", To("竹_金烏")),
                        O("她說想嫁給你時「精明一點為自己打算」", "汗青書62《新家》。", To("E62"), Rq(Lover(6))),
                    },
                    Next = To("竹_行商"),
                },
                new MapNode
                {
                    Id = "竹_行商", Title = "四師兄離去：隨行商", Kind = NodeKind.Event, Lane = 4, Row = 8,
                    Start = T(3, 3, 1), End = T(3, 3, 3), Where = "後山或練功場修煉五次後自動觸發",
                    Reqs = Rq(Flag("E13004", "曾收留郁竹"), Lover(6), 四師兄在唐門),
                    See = To("四師兄離去"),
                    Options = new[] { O("「好。」隨四師兄西域行商", "汗青書55《活著》。", To("E55")) },
                },
                new MapNode
                {
                    Id = "竹_金烏", Title = "師妹招親・金烏鬧事", Kind = NodeKind.Branch, Lane = 2, Row = 8,
                    Start = T(3, 5, 1), End = T(3, 5, 1), Where = "第三年五月上旬 → 郁城的請求",
                    Reqs = Rq(Lover(6)),
                    See = To("金烏鬧事"),
                    Options = new[]
                    {
                        O("☯「決鬥」", "對決金烏初戰須獲勝；遇到金烏偷襲需天命≥90 迴避成功。之後「郁城的請求」選「我得去」。", To("竹_崆峒")),
                        O("☯「不講武德群而毆之」", "連戰皆須勝利，其餘同上。", To("竹_崆峒")),
                        O("郁城的請求：不去", "不上崆峒，結緣線中斷。"),
                    },
                },

                // ---- 崆峒爭奪戰 ----
                new MapNode
                {
                    Id = "竹_崆峒", Title = "崆峒爭奪戰", Kind = NodeKind.Branch, Lane = 2, Row = 9,
                    Start = T(3, 5, 1), End = T(3, 5, 1), Where = "第三年五月上旬",
                    Reqs = Rq(Lover(6), FlagIs("M50046", 2, "崆峒神威走小竹分支")),
                    Note = "進入崆峒地界擲骰 <60「保護郁城」要打贏，≥60「入陣衝殺」勝負不拘；鎮魂獄戰荊紅要獲勝。" +
                           "強化金烏會全力防禦：打不動時持續普攻觸發打斷武器，斷三次後小竹協助破防、金烏閒置，趁機擊殺。" +
                           "這段有很多 bad end，記得備份存檔。",
                    Options = new[]
                    {
                        O("得知戰局惡化時「我們逃跑」", "汗青書56《平凡的悲劇》。", To("E56")),
                        O("對決落敗或拖到 15 回合", "對崆峒弟子眾落敗，或對決金烏上人落敗／拖滿 15 回合，崆峒之行失敗。"),
                        O("探望郁竹：「掌派人迎娶四姝是傳統美德」", "汗青書59《自創武功》。", To("E59")),
                        O("辭別崆峒：接任掌派人", "汗青書61《崆峒掌派人》。", To("E61"), null, Flag("M60015", "已成為崆峒掌派人")),
                        O("探望時「我有得選?」，辭別時「執意不受」", "完成結緣，之後自動獲得「小竹的手帕」。部分劇情需風雲史20《折梅手》且命運≥50。",
                          To("竹_邀請"), null, Flag("G11006", "已與郁竹結緣")),
                    },
                    Next = To("竹_屍心", "竹_仙令", "竹_搥背"),
                },
                new MapNode
                {
                    Id = "竹_屍心", Title = "解小梅屍心丹毒", Kind = NodeKind.Event, Lane = 1, Row = 10,
                    Start = T(3, 5, 1), End = T(3, 5, 3), Where = "崆峒爭奪戰結束後",
                    Reqs = Rq(Stat(G.命運, "命運", Op.Ge, 50)),
                    Note = "小梅線會自然解開；小竹線要在崆峒之後還有命運 50 才會觸發。夢中向瑞杏許願「希望人間溫柔點」可得 30 點，開局保留一些命運點也行。",
                },
                new MapNode
                {
                    Id = "竹_仙令", Title = "仙令的真相", Kind = NodeKind.Event, Lane = 3, Row = 10,
                    Start = T(3, 5, 1), End = T(3, 5, 3), Where = "平定崆峒後，等趙活睡醒時擲骰「直接爆睡」",
                    Note = "四掌門先行離去私下喝酒，酒席間透露大量劇情設定。不影響結緣。",
                },
                new MapNode
                {
                    Id = "竹_搥背", Title = "小竹搥背券", Kind = NodeKind.Branch, Lane = 4, Row = 10,
                    Start = T(3, 5, 2), End = T(3, 8, 1), Where = "崆峒戰後回到唐門",
                    Reqs = Rq(Flag("G11006", "與郁竹結緣")),
                    Note = "不是必拿，兩種結果都不影響結緣。",
                    Options = new[]
                    {
                        O("有搥背券", "第二年鐵拳門比試全勝獲得，回唐門後觸發搥背事件。"),
                        O("沒有搥背券", "改觸發另一個事件。"),
                    },
                },
                new MapNode
                {
                    Id = "竹_邀請", Title = "邀請郁竹・武林大會", Kind = NodeKind.Event, Lane = 2, Row = 10,
                    Start = T(3, 8, 2), End = T(3, 10, 2), Where = "第三年八月中旬大會請帖後前往鍛冶場",
                    Reqs = Rq(Flag("E12004", "郁竹在唐門"), Flag("G11006", "與郁竹結緣"), Flag("M60005", "郁竹同行武林大會")),
                    See = To("大會請帖", "武林大會"),
                    Next = To("竹_鐵鋪"),
                },

                // ---- 終局 ----
                new MapNode
                {
                    Id = "竹_鐵鋪", Title = "集思廣益：鐵鋪事件", Kind = NodeKind.Branch, Lane = 2, Row = 11,
                    Start = T(3, 12, 1), End = T(3, 12, 1), Where = "第三年十二月上旬剩一次行動「集思廣益」→ 訪問郁竹",
                    Reqs = Rq(Flag("E12004", "郁竹在唐門")),
                    Note = "老虎戰衣只看有沒有在後山教過她武功：劇本在「為妳自己打造兵器吧」之後檢查「小竹學武功」≥1，跟鐵劍令、唐門出擊或乞丐戰無關。",
                    See = To("眾人的決策"),
                    Options = new[]
                    {
                        O("留下圖紙", "她畫的連環火箭交給你打造，外堡圍攻戰可看到連發火砲。"),
                        O("銷毀圖紙 →「為我造一柄劍吧」", "替你打造完竟劍。", null, null, Flag("E00017", "選了完竟劍")),
                        O("銷毀圖紙 →「為妳自己打造兵器吧」（教過她武功）", "設置「小竹的秘密武器」：她和火炎山劍閣打造專屬的老虎戰衣。",
                          To("竹_戰衣"), Rq(Flag("M00045", "教過小竹武功")), Flag("M70069", "小竹的秘密武器")),
                        O("銷毀圖紙 →「為妳自己打造兵器吧」（沒教過她武功）", "她說自己不會武功，改成替唐門弟子保養兵器：鍛造+5、門人血量+30，沒有戰衣。",
                          null, Rq(NoFlag("M00045", "沒教過小竹武功"))),
                    },
                    Next = To("竹_決策"),
                },
                new MapNode
                {
                    Id = "竹_戰衣", Title = "老虎戰衣登場", Kind = NodeKind.Event, Lane = 0, Row = 12, Route = 頑抗 | 西武林,
                    Start = T(4, 1, 1), End = T(4, 2, 3), Where = "唐門圍攻戰／東西武林盟會戰期間",
                    Reqs = Rq(Flag("M70069", "小竹的秘密武器")),
                    Note = "小竹穿上老虎戰衣「老虎大王」參戰：戰役中投擲斬馬劍秒殺敵軍，決鬥中可承受傷害直到耐用耗盡，撤退時擊出火箭飛拳。",
                },
                new MapNode
                {
                    Id = "竹_決策", Title = "你的決策（郁竹結緣後）", Kind = NodeKind.Branch, Lane = 2, Row = 12,
                    Start = T(3, 12, 1), End = T(4, 1, 3), Where = "第三年十二月上旬武林盟對策",
                    See = To("眾人的決策"),
                    Options = new[]
                    {
                        決策退隱("依道德進入汗青書28《血河刀》（低）或63《醜神仙》（高）；遣散後前往崆峒接掌則為65。", To("E28", "E63", "E65"), Flag("G11006", "與郁竹結緣")),
                        決策頑抗("唐門圍攻戰終戰獲勝 → 汗青書41《千古名匠》。", To("E41")),
                        決策西武林("小竹以掌派夫人身分出使崆峒，必定支援。", To("竹_會戰")),
                    },
                },
                new MapNode
                {
                    Id = "竹_會戰", Title = "東西武林盟會戰", Kind = NodeKind.Event, Lane = 4, Row = 13, Route = 西武林,
                    Start = T(4, 2, 2), End = T(4, 3, 1), Where = "第四年二月中旬會戰 → 三月上旬眉山決戰",
                    Reqs = Rq(Flag("M70031", "小竹出使崆峒派")),
                    Note = "要拿到小竹的結緣 CG 結局與成就，必須完成《武林傳奇》結局。",
                    See = To("東西武林盟會戰"),
                    Options = new[]
                    {
                        O("全力固守外堡並打贏", "汗青書66《青山仍在》。", To("E66")),
                        O("眉山決戰擊敗武林盟主", "汗青書48《武林傳奇》。", To("E48")),
                    },
                },
            },
        };

        // ================= 小梅（虞小梅） =================
        private static MapSheet 小梅() => new MapSheet
        {
            Tab = "小梅", Title = "虞小梅（小梅）結緣線", Lover = 3, Bond = Flag("G11004", "與虞小梅結緣"),
            Intro = "崆峒神威更新開放的結緣線：崆峒留學期間累積好感、江陵決戰前夜隨小梅去襄陽、第三年五月崆峒爭奪戰喚醒她。" +
                    "與小竹線劇情重疊很多，攻略小梅也要把小竹的返鄉劇情跑完。",
            Sections = new[]
            {
                new MapSection { Row = 0, Title = "第二年　崆峒留學（三月～八月）", Lanes = new[] { "鶴崖・共舞", "崆峒探險隊", "結緣必要", "", "" } },
                new MapSection { Row = 3, Title = "第二年十月　江陵決戰前夜・襄陽行", Lanes = 必要 },
                new MapSection { Row = 6, Title = "第三年　唐門～崆峒爭奪戰", Lanes = 必要 },
                new MapSection { Row = 9, Title = "第三年八月起　結緣之後", Lanes = new[] { "", "孟仙謠", "結緣必要", "", "" } },
            },
            Nodes = new[]
            {
                new MapNode
                {
                    Id = "梅_留學", Title = "留學討論・門派", Kind = NodeKind.Branch, Lane = 2, Row = 0,
                    Start = T(2, 2, 1), End = T(2, 3, 1), Where = "第二年二月上旬唐門月會 → 三月上旬崆峒門派",
                    Reqs = Rq(貢獻度(Op.Ge, 50)),
                    Note = "曾在飛天殿修仙至少一次，崆峒爭奪戰的對話會稍有不同。",
                    See = To("留學討論", "崆峒留學"),
                    Options = new[]
                    {
                        O("崆峒留學，進飛天門", "小梅的故事最多（飛天殿修練、鶴崖）。", To("梅_返鄉"), null, FlagIs("M20011", 1, "崆峒門派＝飛天門")),
                        O("崆峒留學，其他門派", "門派不是結緣必要條件；記得也去鐵拳巷跑小竹事件。", To("梅_返鄉")),
                        O("「退席」不留學", "遇不到小梅，結緣線無法開始。"),
                    },
                    Next = To("梅_鶴崖", "梅_探險"),
                },
                new MapNode
                {
                    Id = "梅_鶴崖", Title = "鶴崖事件集・小梅共舞", Kind = NodeKind.Event, Lane = 0, Row = 1, Route = 崆峒,
                    Start = T(2, 3, 3), End = T(2, 7, 3), Where = "飛天殿修練與護法顏疆衝突、小梅介入 → 逃跑／牢房分支後開啟鶴崖",
                    Reqs = Rq(FlagIs("M20011", 1, "崆峒門派＝飛天門"), Flag("M00030", "已開啟鶴崖")),
                    Note = "與火閃電交手一律拖時間（打贏扣好感、打輸死亡）；練三、四次看過幾種劇情即可。七月來會觸發與小梅共舞並得《仙鶴迷蹤拳》。比武全勝會提前關閉鶴崖。不影響結緣。",
                },
                new MapNode
                {
                    Id = "梅_探險", Title = "崆峒探險隊", Kind = NodeKind.Event, Lane = 1, Row = 1, Route = 崆峒,
                    Start = T(2, 4, 1), End = T(2, 7, 3), Where = "第二年四月上旬起",
                    Reqs = Rq(Flag("G10004", "認識虞小梅"), Rel(R.虞小梅, Op.Ge, 20), Rel(R.瑞笙, Op.Ge, 7), Rel(R.南宮淺, Op.Ge, 7)),
                    Note = "小梅、瑞笙、南宮淺與趙活組成探險隊：飛天殿修練會新增探險隊出發活動，之後隨小梅去襄陽時可觸發泥教劇情。" +
                           "瑞笙好感可靠崆峒戰隊、坐牢事件補；南宮淺好感可靠南宮祝壽選靈丹、或大鐵鋪遭竊追右邊。不在飛天門也能觸發。",
                },
                new MapNode
                {
                    Id = "梅_返鄉", Title = "郁竹返家準備（小梅版）", Kind = NodeKind.Branch, Lane = 2, Row = 1, Route = 崆峒,
                    Start = T(2, 4, 1), End = T(2, 6, 3), Where = "第二年四月上～下旬，或五月下旬～六月下旬",
                    Reqs = Rq(Flag("E21004", "已完成郁竹返家")),
                    Note = "攻略小梅也要完成「郁竹返家」；建議走小梅版本。",
                    See = To("竹_返鄉"),
                    Options = new[]
                    {
                        O("小梅觸發：跟小梅跑步 →「追上去看看」", "門派修練時隨機遇到「小梅跑步」，余公子被帶走後追上去，並打贏對決。", To("梅_送行"),
                          Rq(Flag("G10006", "認識郁竹"), Rel(R.虞小梅, Op.Ge, 20))),
                        O("郁竹觸發：答應扮演家丁「好吧」", "造訪鐵拳巷≥4 次、郁竹好感≥20，行動次數≥2 時點「鐵拳巷」。", To("梅_送行"),
                          Rq(Rel(R.郁竹, Op.Ge, 20))),
                    },
                },
                new MapNode
                {
                    Id = "梅_送行", Title = "離開崆峒・虞小梅送行", Kind = NodeKind.Event, Lane = 2, Row = 2, Route = 崆峒,
                    Start = T(2, 8, 1), End = T(2, 8, 1), Where = "第二年八月上旬馳援南宮",
                    Reqs = Rq(Rel(R.虞小梅, Op.Ge, 35), Lover(3)),
                    Note = "離開崆峒前虞小梅好感≥35（之後沒機會增加），並把心上人定為虞小梅。" +
                           "沒有拜師夏侯蘭、且不是「心上人為郁竹並且郁竹好感在崆峒四姝中最高」時，會有「虞小梅送行」，影響下一步能否進入林中修練。",
                    Next = To("梅_前夜"),
                },

                // ---- 江陵・襄陽 ----
                new MapNode
                {
                    Id = "梅_前夜", Title = "江陵決戰前夜：打拳散心", Kind = NodeKind.Branch, Lane = 2, Row = 3, Route = 崆峒,
                    Start = T(2, 10, 1), End = T(2, 10, 1), Where = "第二年十月上旬，睡不著時選「打拳散心」",
                    Reqs = Rq(Lover(3), Rel(R.虞小梅, Op.Ge, 35)),
                    Note = "若觸發「南宮橫出城」，選「我還有點事」避免進入釣魚事件。",
                    See = To("江陵決戰"),
                    Options = new[]
                    {
                        O("林中修練 → 小梅告知襄陽行 →「我跟你去」",
                          "進入林中修練需「虞小梅送行」或處世<40。告知襄陽行：有收留葉氏兄妹時，遇到葉雲舟後選「再練一下功」；沒收留時需心相>30。",
                          To("梅_襄陽"), Rq(Either(Flag("M10001", "曾收留葉氏兄妹"), 心相(Op.Gt, 30))), Flag("M20062", "已經歷襄陽行")),
                        O("遇到女飛賊時打贏", "虞小梅好感-1；跌破 35 就進不了告知襄陽行。打輸或拖時間都可以。"),
                        O("沒有跟小梅去襄陽", "回唐門；小梅仍可能投靠唐門，但結緣線中斷。"),
                    },
                },
                new MapNode
                {
                    Id = "梅_襄陽", Title = "襄陽行", Kind = NodeKind.Event, Lane = 2, Row = 4,
                    Start = T(2, 10, 1), End = T(2, 10, 3), Where = "第二年十月",
                    Reqs = Rq(Flag("M20062", "已經歷襄陽行")),
                    Note = "沒有必要條件，只要活下來。建議提升孟仙謠與孟珙好感（隱藏數值），可解鎖之後的孟仙謠劇情。",
                    Next = To("梅_唐門"),
                },
                new MapNode
                {
                    Id = "梅_唐門", Title = "小梅在唐門", Kind = NodeKind.Event, Lane = 2, Row = 5,
                    Start = T(2, 10, 3), End = T(3, 1, 1), Where = "襄陽行後小梅來到唐門",
                    Reqs = Rq(Flag("E12003", "虞小梅在唐門")),
                    Note = "相關劇情大多在伙房。",
                    See = To("小梅投靠"),
                    Next = To("梅_臥雲"),
                },

                // ---- 第三年 ----
                臥雲崗("梅_臥雲", 6, "梅_金烏"),
                new MapNode
                {
                    Id = "梅_金烏", Title = "師妹招親・金烏鬧事", Kind = NodeKind.Branch, Lane = 2, Row = 7,
                    Start = T(3, 5, 1), End = T(3, 5, 1), Where = "第三年五月上旬",
                    Reqs = Rq(Lover(3)),
                    See = To("金烏鬧事"),
                    Options = new[]
                    {
                        O("擊退金烏 → 跟小竹弟弟一同上山", "對決金烏初戰須獲勝（群毆則連戰皆勝），金烏偷襲需天命≥90 迴避；「郁城的請求」選「我得去」。", To("梅_崆峒")),
                        O("沒有擊退金烏或不去", "不上崆峒，結緣線中斷。"),
                    },
                },
                new MapNode
                {
                    Id = "梅_崆峒", Title = "崆峒爭奪戰：半雨屏", Kind = NodeKind.Branch, Lane = 2, Row = 8,
                    Start = T(3, 5, 1), End = T(3, 5, 1), Where = "第三年五月上旬",
                    Reqs = Rq(Lover(3), FlagIs("M50046", 1, "崆峒神威走小梅分支")),
                    Note = "進入崆峒地界擲骰 <60「保護郁城」要打贏，≥60「入陣衝殺」勝負不拘；鎮魂獄戰荊紅要獲勝。" +
                           "二戰嘴攻原本就剋絕招，但學了金鐘罩或蒼龍吟可能失手打死小梅，可到設定開「勇者模式」減半傷害。",
                    Options = new[]
                    {
                        O("半雨屏初戰小梅落敗", "汗青書57《神不在的證明》。", To("E57")),
                        O("二戰沒能喚醒小梅", "嘴攻少於 4 次就獲勝、落敗，或第 15 回合「屍心蠱反噬」後讓她生命歸零 → 汗青書58《修仙》。", To("E58")),
                        O("喚醒後探望時不讓她離開", "汗青書60《崆峒掌派人》。", To("E60")),
                        O("辭別崆峒：接任掌派人", "汗青書61《崆峒掌派人》。", To("E61")),
                        O("初戰獲勝或撐 8 回合，二戰嘴攻≥4 次；探望時「妳可以去任何地方」、辭別「執意不受」",
                          "完成結緣，之後自動獲得「小梅的髮帶」。", To("梅_邀請"), null, Flag("G11004", "已與虞小梅結緣")),
                    },
                },

                // ---- 結緣後 ----
                new MapNode
                {
                    Id = "梅_邀請", Title = "邀請虞小梅・武林大會", Kind = NodeKind.Event, Lane = 2, Row = 9,
                    Start = T(3, 8, 2), End = T(3, 10, 2), Where = "第三年八月中旬大會請帖後前往伙房",
                    Reqs = Rq(Flag("E12003", "虞小梅在唐門"), Flag("G11004", "與虞小梅結緣"), Flag("M60004", "虞小梅同行武林大會")),
                    See = To("大會請帖", "武林大會"),
                    Next = To("梅_決策", "梅_孟"),
                },
                new MapNode
                {
                    Id = "梅_孟", Title = "孟仙謠前往崆峒", Kind = NodeKind.Event, Lane = 1, Row = 10,
                    Start = T(3, 10, 2), End = T(3, 11, 3), Where = "武林大會拜訪瑞笙時孟仙謠出來打招呼 → 客棧休息選「下樓喝酒」",
                    Reqs = Rq(Flag("M20062", "經歷過襄陽行"), Note("襄陽逛街盡量骰出共同出遊、衝撞事件讓她耐心等待"), Note("崆峒期間和瑞笙混熟")),
                    Note = "傳記沒有孟仙謠，但她有隱藏好感。不影響結緣。",
                },
                new MapNode
                {
                    Id = "梅_決策", Title = "你的決策（小梅結緣後）", Kind = NodeKind.Branch, Lane = 2, Row = 10,
                    Start = T(3, 12, 1), End = T(4, 3, 1), Where = "第三年十二月上旬武林盟對策",
                    Note = "要拿到小梅的結緣成就，必須完成《武林傳奇》結局。",
                    See = To("眾人的決策"),
                    Options = new[]
                    {
                        決策退隱("依道德進入汗青書26《山中老人》（低）或27《世外桃源》（高）；遣散後前往崆峒則為64《賣國賊》或65《崆峒掌派人》。",
                             To("E26", "E27", "E64", "E65"), Flag("G11004", "與虞小梅結緣")),
                        決策頑抗("唐門圍攻戰終戰獲勝 → 汗青書40《小梅私藏》。", To("E40")),
                        決策西武林("小梅以掌派夫人身分出使崆峒，必定支援；眉山決戰擊敗武林盟主 → 汗青書48《武林傳奇》（風雲史20《折梅手》）。", To("E48")),
                    },
                },
            },
        };

        // ================= 小師妹（唐默鈴） =================
        private static MapSheet 小師妹() => new MapSheet
        {
            Tab = "小師妹", Title = "唐默鈴（小師妹）結緣線", Lover = 0, Bond = Flag("G11000", "與小師妹結緣"),
            Intro = "結緣集中在第三年五月的師妹招親：心上人與好感達標，金烏來襲時她隨你下山，打贏金烏後在後山決定她的歸宿。",
            Sections = new[]
            {
                new MapSection { Row = 0, Title = "第一年～第三年四月　準備", Lanes = new[] { "", "", "好感與心上人", "", "" } },
                new MapSection { Row = 1, Title = "第三年五月　師妹招親", Lanes = 必要 },
                new MapSection { Row = 3, Title = "第三年八月～十月　武林大會", Lanes = new[] { "", "", "結緣之後", "", "" } },
                new MapSection { Row = 5, Title = "第三年十二月起　終局", Lanes = new[] { "", "", "結緣之後", "", "西武林盟" } },
            },
            Nodes = new[]
            {
                new MapNode
                {
                    Id = "妹_準備", Title = "累積好感・心上人", Kind = NodeKind.Event, Lane = 2, Row = 0,
                    Start = T(1, 4, 1), End = T(3, 4, 3), Where = "第三年五月前",
                    Reqs = Rq(Lover(0), Rel(R.小師妹, Op.Ge, 50)),
                    Note = "心上人不是小師妹時，可在男弟子房摺紙增加變心點。好感刷法：後山練功隨機觸發「小師妹烤魚」。" +
                           "第一年十月壽宴討論選「(小師妹好像欲言又止？)」可讓她參加南宮壽宴（劇情差分）。",
                    Next = To("妹_金烏"),
                },
                new MapNode
                {
                    Id = "妹_金烏", Title = "師妹招親・對決金烏", Kind = NodeKind.Branch, Lane = 2, Row = 1,
                    Start = T(3, 5, 1), End = T(3, 5, 1), Where = "第三年五月上旬，收到金烏鬧事消息後下山",
                    Reqs = Rq(Lover(0), Rel(R.小師妹, Op.Ge, 50)),
                    Note = "修養中庸以上比較好打。",
                    See = To("金烏鬧事"),
                    Options = new[]
                    {
                        O("☯「決鬥」並獲勝", "心上人為小師妹且好感≥50 時她會跟隨你，對決金烏只有一場。", To("妹_歸宿")),
                        O("☯「不講武德群而毆之」", "連戰皆須勝利；落敗依情況進入汗青書16 或 22。", To("妹_歸宿", "E16", "E22")),
                        O("對決金烏落敗", "結緣失敗。"),
                    },
                },
                new MapNode
                {
                    Id = "妹_歸宿", Title = "師妹的歸宿", Kind = NodeKind.Branch, Lane = 2, Row = 2,
                    Start = T(3, 5, 1), End = T(3, 5, 1), Where = "第三年五月上旬，後山",
                    Note = "天命可用「骰子選項」頁指定點數。只要前兩步達成並正常遊玩，好感≥60 通常都會達標。",
                    Options = new[]
                    {
                        O("天命≥50「小師妹是我的!」且好感≥60", "對話後得到「小師妹的香囊」，完成結緣。", To("妹_請帖"),
                          Rq(Rel(R.小師妹, Op.Ge, 60)), Flag("G11000", "已與小師妹結緣")),
                        O("天命 <50", "小師妹出嫁，結緣失敗。"),
                    },
                },
                new MapNode
                {
                    Id = "妹_請帖", Title = "大會請帖：帶小師妹同行", Kind = NodeKind.Branch, Lane = 2, Row = 3,
                    Start = T(3, 8, 2), End = T(3, 8, 2), Where = "第三年八月中旬",
                    Reqs = Rq(Rel(R.三師兄, Op.Ge, 35)),
                    Note = "建議帶她去，可以加一堆好感。",
                    See = To("大會請帖"),
                    Options = new[]
                    {
                        O("道德≥60", "三師兄直接讓你帶她去。", To("妹_大會"), Rq(Stat(G.道德, "道德", Op.Ge, 60)), Flag("M60003", "小師妹同行武林大會")),
                        O("道德<60：選「可以（撒謊）」", "同樣能帶她去。", To("妹_大會")),
                        O("不帶她", "錯過旅途、錦香宮與武林大會的劇情與好感。", To("妹_大會")),
                    },
                },
                new MapNode
                {
                    Id = "妹_大會", Title = "前往武林大會・錦香宮・武林大會", Kind = NodeKind.Event, Lane = 2, Row = 4,
                    Start = T(3, 9, 1), End = T(3, 10, 2), Where = "第三年九月上旬～十月中旬",
                    Note = "旅途戰役可讓她參加；錦香宮華音閣新增「小師妹鼓掌」；武林大會她可參與戰役及決鬥。" +
                           "結緣後任意時間：伙房「小師妹代班」插圖改變、鬧市逛街新增「得意洋洋」（心相+30）、後山烤魚拒絕改為一起練功。",
                    See = To("錦香宮", "武林大會"),
                    Next = To("妹_決策"),
                },
                new MapNode
                {
                    Id = "妹_決策", Title = "你的決策（小師妹結緣後）", Kind = NodeKind.Branch, Lane = 2, Row = 5,
                    Start = T(3, 12, 1), End = T(4, 1, 3), Where = "第三年十二月上旬武林盟對策",
                    See = To("眾人的決策"),
                    Options = new[]
                    {
                        決策退隱("告別唐門時女弟子房劇情差分，汗青書25《退隱江湖》為小師妹陪伴版本。", To("E25"), Flag("G11000", "與小師妹結緣")),
                        決策頑抗("進入伴侶之約與唐門圍攻戰。", To("妹_圍攻")),
                        決策西武林("進入東西武林盟會戰。", To("妹_會戰")),
                    },
                },
                new MapNode
                {
                    Id = "妹_圍攻", Title = "伴侶之約・唐門圍攻戰", Kind = NodeKind.Event, Lane = 2, Row = 6, Route = 頑抗,
                    Start = T(4, 1, 2), End = T(4, 2, 2), Where = "第四年一月中旬（西武林失敗則二月中旬）",
                    Reqs = Rq(Flag("G11000", "與小師妹結緣")),
                    Note = "伴侶之約到女弟子房可為小師妹梳頭；另有結緣者時會被修羅場取代；沒觸發則剩 2 行動點時自動「失約」。" +
                           "多人結緣時滅門線結局優先度：唐默鈴＞葉雲裳＞上官螢＞虞小梅＞郁竹＞魏菊＞夏侯蘭＞龍湘。",
                    See = To("圍攻戰"),
                    Options = new[] { O("終戰獲勝", "汗青書37《唐門薪火》。", To("E37")) },
                },
                new MapNode
                {
                    Id = "妹_會戰", Title = "東西武林盟會戰・眉山決戰", Kind = NodeKind.Event, Lane = 4, Row = 6, Route = 西武林,
                    Start = T(4, 2, 2), End = T(4, 3, 1), Where = "第四年二月中旬會戰 → 三月上旬眉山決戰",
                    Note = "可讓她同行或護衛三師兄；護衛三師兄並決策出擊外堡可得技能「勇氣2」。眉山決戰戰勝可達成風雲史《唐門女婿》。",
                    See = To("東西武林盟會戰"),
                    Options = new[] { O("眉山決戰擊敗武林盟主", "汗青書48《武林傳奇》，結局畫面與小師妹相伴。", To("E48")) },
                },
            },
        };

        // ================= 葉雲裳 =================
        private static MapSheet 葉雲裳() => new MapSheet
        {
            Tab = "葉雲裳", Title = "葉雲裳結緣線", Lover = 2, Bond = Flag("G11002", "與葉雲裳結緣"),
            Intro = "要讓葉氏兄妹入住唐門、第二年不留學走破廟並轉入青城留學，第三年練成《九轉殘篇》，在雲裳結緣時救回她。" +
                    "此線要練的武學多、花錢的支線也多，建議二周目以上再攻略。",
            Sections = new[]
            {
                new MapSection { Row = 0, Title = "第一年　葉氏兄妹入住唐門", Lanes = 必要 },
                new MapSection { Row = 2, Title = "第二年　不留學・破廟・青城留學", Lanes = 必要 },
                new MapSection { Row = 6, Title = "第三年一月～八月　雲裳結緣", Lanes = new[] { "兄妹對決", "", "結緣必要", "", "" } },
                new MapSection { Row = 9, Title = "第三年九月起　結緣之後", Lanes = new[] { "", "", "結緣之後", "", "西武林盟" } },
            },
            Nodes = new[]
            {
                new MapNode
                {
                    Id = "雲_寶藏", Title = "談論武穆王寶藏", Kind = NodeKind.Event, Lane = 2, Row = 0,
                    Start = T(1, 8, 1), End = T(2, 1, 3), Where = "第一年八月上旬到第二年一月下旬剩兩次行動時，自動觸發",
                    Reqs = Rq(Either(Rel(R.三師兄, Op.Ge, 25), Note("購買三份以上江湖快報（每月限一份）"))),
                    Next = To("雲_風波"),
                },
                new MapNode
                {
                    Id = "雲_風波", Title = "葉氏行蹤風波", Kind = NodeKind.Branch, Lane = 2, Row = 1,
                    Start = T(1, 8, 1), End = T(2, 2, 3), Where = "談論寶藏後一個月內，點選大門自動觸發",
                    Reqs = Rq(名聲(Op.Ge, 10)),
                    Note = "名聲不足時：低心相打鐵、高心相行商（嘴力20以下）、鬧市逛街比較好拿名聲。",
                    See = To("葉氏兄妹"),
                    Options = new[]
                    {
                        O("葉氏兄妹入住唐門", "之後的雲裳劇情都以此為前提。", To("雲_離家"), null, Flag("M10001", "已收留葉氏兄妹")),
                        O("錯過期限或結果不是入住", "一個月的期限可能被初識李富貴、初識福韞、出發夔州等事件推遲而錯過；結緣線中斷。"),
                    },
                },
                new MapNode
                {
                    Id = "雲_離家", Title = "留學討論「退席」→ 住破廟", Kind = NodeKind.Branch, Lane = 2, Row = 2,
                    Start = T(2, 2, 1), End = T(2, 6, 1), Where = "第二年二月上旬月會 → 六月上旬離家出走",
                    Reqs = Rq(Lover(2)),
                    Note = "留學討論前建議向四師兄購買醫術秘笈（要練到醫術3）、《羅漢拳譜》（佛學2）；想在第四年學同舟劍法或劍聖極招要買《點蒼劍譜》。" +
                           "離家出走時葉雲裳好感≥30，就算選住客棧也會被迫住破廟。",
                    See = To("留學討論", "離家出走"),
                    Options = new[]
                    {
                        O("「退席」→「住破廟」（心上人葉雲裳）", "之後才能轉入青城留學。", To("雲_破廟"), Rq(Lover(2)), FlagIs("M20028", 1, "已選住破廟")),
                        O("崆峒留學或住客棧", "不會進入青城留學，結緣線中斷。"),
                    },
                },
                new MapNode
                {
                    Id = "雲_破廟", Title = "破廟密謀・唐門迎敵", Kind = NodeKind.Branch, Lane = 2, Row = 3,
                    Start = T(2, 8, 2), End = T(2, 8, 2), Where = "第二年八月中旬",
                    See = To("破廟密謀", "唐門迎敵"),
                    Options = new[]
                    {
                        O("報信、迎敵，戰後心上人為葉雲裳", "院內大戰勝負不拘，之後進入青城留學。", To("雲_青城"), Rq(Lover(2)), FlagIs("M20008", 1, "已進入青城留學")),
                        O("「攻打唐門」或「跑路」", "結緣線中斷（結局見主線破廟密謀）。"),
                        O("唐門迎敵時離開山路", "只選大門可能離開山路；結緣線中斷。"),
                    },
                },
                new MapNode
                {
                    Id = "雲_青城", Title = "青城留學", Kind = NodeKind.Event, Lane = 2, Row = 4, Route = 青城,
                    Start = T(2, 8, 3), End = T(2, 11, 3), Where = "第二年八月下旬～十一月下旬",
                    Reqs = Rq(Rel(R.葉雲裳, Op.Ge, 30), Note("醫術練到等級 3")),
                    Note = "主線固定，只要活下來。留學歸來後主線很緊湊，強烈建議在這段期間好感≥30、醫術練到 3。",
                    See = To("青城留學"),
                    Next = To("雲_歸來"),
                },
                new MapNode
                {
                    Id = "雲_歸來", Title = "葉雲舟歸來", Kind = NodeKind.Event, Lane = 2, Row = 5,
                    Start = T(2, 12, 1), End = T(2, 12, 1), Where = "第二年十二月上旬",
                    Reqs = Rq(Rel(R.葉雲裳, Op.Ge, 30), Note("醫術等級≥3")),
                    Note = "觸發「聽說寂滅三摩地」。建議同時練到佛學2（可買《太祖長拳》或《金鐘罩》）。",
                    Next = To("雲_離去"),
                },
                new MapNode
                {
                    Id = "雲_離去", Title = "葉氏兄妹離去", Kind = NodeKind.Branch, Lane = 2, Row = 6,
                    Start = T(3, 1, 1), End = T(3, 1, 1), Where = "第三年一月上旬",
                    Reqs = Rq(Lover(2)),
                    Note = "只想讓她活下來而不結緣：完成寶藏、風波、不去青城，加上這裡的「抵抗」與「勸留」即可，但她只會在西武林線終戰與唐門薪火結局再出現。",
                    Options = new[]
                    {
                        O("天命≥80「葉雲裳的抵抗」→「勸葉雲舟留」", "與唐芳談話後獲得《九轉殘篇》，要在五月上旬前練完（需佛學2）；有餘裕也建議練《南宮心法》。",
                          To("雲_結緣"), Rq(Either(Rel(R.葉雲裳, Op.Ge, 30), Note("命運收束之力")))),
                        O("其他結果", "葉雲裳離開，結緣線中斷。"),
                    },
                },
                new MapNode
                {
                    Id = "雲_結緣", Title = "雲裳結緣", Kind = NodeKind.Branch, Lane = 2, Row = 7,
                    Start = T(3, 5, 1), End = T(3, 5, 1), Where = "第三年五月上旬（與師妹招親同期）",
                    Reqs = Rq(Lover(2), Rel(R.葉雲裳, Op.Ge, 50), Note("《九轉殘篇》修練完畢")),
                    Note = "劍聖傳承前置：比拚內力擲到 <99「略遜一籌」，且谷底生活閒聊「葉雲裳的武功」。有《南宮護心訣》3 時，幻境南宮遠獲勝後會變成新技能《偕老誓》。",
                    Options = new[]
                    {
                        O("三次「軟性對策」→ 山道之戰 → 九轉療傷全勝",
                          "對決段智秀獲勝時，對決鐵冠道人要撐 4 回合以上並落敗；比拚內力不可擲到 <40「死」；幻境連戰全勝 → 獲得「葉雲裳的梳子」完成結緣。",
                          To("雲_請帖"), null, Flag("G11002", "已與葉雲裳結緣")),
                        O("幻境連戰輸掉任何一場", "汗青書54《與妳同行》。", To("E54")),
                        O("沒有結緣（她仍在唐門）", "五月中旬「雲裳之死」。"),
                    },
                    Next = To("雲_對決"),
                },
                new MapNode
                {
                    Id = "雲_對決", Title = "兄妹對決", Kind = NodeKind.Event, Lane = 0, Row = 8,
                    Start = T(3, 7, 3), End = T(3, 7, 3), Where = "第三年七月下旬",
                    Reqs = Rq(Flag("G11002", "與葉雲裳結緣"), Rel(R.葉雲舟, Op.Ge, 40)),
                },
                new MapNode
                {
                    Id = "雲_請帖", Title = "邀請葉雲舟同行", Kind = NodeKind.Branch, Lane = 2, Row = 8,
                    Start = T(3, 8, 2), End = T(3, 8, 2), Where = "第三年八月中旬前往大門",
                    See = To("大會請帖"),
                    Options = new[]
                    {
                        O("「幫她求情」", "葉雲裳也同行武林大會（建議）。", To("雲_大會"), null, Flag("M60007", "葉雲裳同行武林大會")),
                        O("「妳問妳哥」", "另有劇情，只有葉雲舟同行。", To("雲_大會")),
                    },
                },
                new MapNode
                {
                    Id = "雲_大會", Title = "錦香宮・武林大會", Kind = NodeKind.Event, Lane = 2, Row = 9,
                    Start = T(3, 9, 1), End = T(3, 10, 2), Where = "第三年九月上旬～十月中旬",
                    Note = "錦香宮華音閣學琴三次 → 西武林線「護衛三師兄」時雲裳彈琴協助。武林大會讓葉雲舟代表唐門出戰（趙活未挑戰上官隼），否則他會離開唐門、看不到南溪劇情。" +
                           "成為武林公敵且「讓葉雲裳陪伴」可解鎖風雲史《惡人自有惡人磨》；客棧「洗澡」再「下樓喝酒」觸發南溪劇情（圍攻戰／會戰時南溪會加入）。",
                    See = To("錦香宮", "武林大會"),
                    Next = To("雲_決策"),
                },
                new MapNode
                {
                    Id = "雲_決策", Title = "你的決策（雲裳結緣後）", Kind = NodeKind.Branch, Lane = 2, Row = 10,
                    Start = T(3, 12, 1), End = T(4, 1, 3), Where = "第三年十二月上旬",
                    Note = "集思廣益新增「訪問葉雲舟」：他代表唐門出戰過就給同舟劍譜（練成後學不到劍聖極招，但通常比較強）。",
                    See = To("眾人的決策"),
                    Options = new[]
                    {
                        決策退隱("告別唐門時女弟子房、男弟子房、煉丹房劇情差分；汗青書25《退隱江湖》為葉雲裳版本。", To("E25"), Flag("G11002", "與葉雲裳結緣")),
                        決策頑抗("葉雲舟與葉雲裳都參戰，終戰瑞笙無點蒼雙尊輔助；終戰獲勝 → 汗青書38《神州旅人》。", To("E38")),
                        決策西武林("大戰準備期有許多劇情變化。", To("雲_會戰")),
                    },
                },
                new MapNode
                {
                    Id = "雲_會戰", Title = "會戰・劍聖傳承・眉山決戰", Kind = NodeKind.Event, Lane = 4, Row = 11, Route = 西武林,
                    Start = T(4, 2, 2), End = T(4, 3, 1), Where = "第四年二月中旬會戰 → 二月下旬劍聖傳承 → 三月上旬眉山決戰",
                    Note = "「護衛三師兄」第二場對決後觸發對決無相祖師；戰後「雲裳找你」時好感≥50 且滿足劍聖前置，二月下旬大門下山可觸發劍聖傳承" +
                           "（擲到≥50「葉雲舟沒出現」，需《點破雲關》且未學《同舟劍法》）。眉山決戰戰勝可達成風雲史《魔王的爪牙》。",
                    See = To("東西武林盟會戰"),
                    Options = new[] { O("眉山決戰擊敗武林盟主", "汗青書48《武林傳奇》，結局畫面與葉雲裳相伴。", To("E48")) },
                },
            },
        };

        // ================= 夏侯蘭 =================
        private static MapSheet 夏侯蘭() => new MapSheet
        {
            Tab = "夏侯蘭", Title = "夏侯蘭結緣線", Lover = 5, Bond = Flag("G11005", "與夏侯蘭結緣"),
            Intro = "崆峒留學進奪魄門並拜夏侯蘭為師，第三年建造樹屋、雪山行，最後在雪山之約抵抗傳功並打贏她。",
            Sections = new[]
            {
                new MapSection { Row = 0, Title = "第二年　崆峒留學・拜師", Lanes = new[] { "", "", "結緣必要", "", "崆峒季試" } },
                new MapSection { Row = 4, Title = "第三年五月～七月　雪山行", Lanes = 必要 },
                new MapSection { Row = 7, Title = "第三年八月起　結緣之後", Lanes = new[] { "", "", "結緣之後", "", "邀請・西武林盟" } },
            },
            Nodes = new[]
            {
                new MapNode
                {
                    Id = "蘭_留學", Title = "留學討論・門派抽籤", Kind = NodeKind.Branch, Lane = 2, Row = 0,
                    Start = T(2, 2, 1), End = T(2, 3, 1), Where = "第二年二月上旬唐門月會 → 三月上旬崆峒門派",
                    Reqs = Rq(貢獻度(Op.Ge, 50)),
                    Note = "門派骰可用「骰子選項」頁指定。",
                    See = To("留學討論", "崆峒留學"),
                    Options = new[]
                    {
                        O("崆峒留學，抽到奪魄門（擲骰 50～74）", "初識魏菊後若選「抱著被別派敵視的覺悟唱反調」，下一個選項不可選「我要去玄功門」。",
                          To("蘭_遭竊"), null, FlagIs("M20011", 2, "崆峒門派＝奪魄門")),
                        O("其他門派或不留學", "無法拜師夏侯蘭，結緣線中斷。"),
                    },
                },
                new MapNode
                {
                    Id = "蘭_遭竊", Title = "衣服遭竊・拜師", Kind = NodeKind.Branch, Lane = 2, Row = 1, Route = 崆峒,
                    Start = T(2, 4, 1), End = T(2, 4, 1), Where = "第二年四月上旬",
                    Options = new[]
                    {
                        O("偷襲時擲到 >50 防禦成功；對決峨嵋弟子一戰勝、二戰敗或撐 5 回合、三戰後「師禮拜謝」",
                          "拜師夏侯蘭，解鎖奪魄森林修練。", To("蘭_森林")),
                        O("其他結果", "沒有拜師，結緣線中斷。"),
                    },
                },
                new MapNode
                {
                    Id = "蘭_森林", Title = "奪魄森林修練", Kind = NodeKind.Event, Lane = 2, Row = 2, Route = 崆峒,
                    Start = T(2, 4, 1), End = T(2, 7, 3), Where = "拜師後到崆峒留學結束",
                    Reqs = Rq(Rel(R.夏侯蘭, Op.Ge, 40), NoFlag("E21001", "樊嘯天未拜師夏侯蘭")),
                    Note = "增加好感的主要方式，盡量在留學結束前好感≥40。練功支線若讓樊嘯天拜師夏侯蘭，會影響後面劇情與結局演出，初次走這條線不建議。",
                    Next = To("蘭_季試", "蘭_樹屋"),
                },
                new MapNode
                {
                    Id = "蘭_季試", Title = "崆峒季試", Kind = NodeKind.Branch, Lane = 4, Row = 3, Route = 崆峒,
                    Start = T(2, 5, 3), End = T(2, 5, 3), Where = "第二年五月下旬",
                    Note = "不是結緣必要條件。",
                    Options = new[]
                    {
                        O("三戰全勝且樊嘯天未拜師", "獲得【崆峒磨喝樂】（紀念品）。"),
                        O("第三戰敗給瑞笙", "之後雪山行可得「雪山飛鴻爪」，影響終戰夏侯蘭的輔助技能（否則為奪魄幽靈爪）。"),
                    },
                },
                new MapNode
                {
                    Id = "蘭_樹屋", Title = "建造樹屋", Kind = NodeKind.Event, Lane = 2, Row = 4,
                    Start = T(3, 5, 3), End = T(3, 5, 3), Where = "第三年五月下旬",
                    Reqs = Rq(Rel(R.夏侯蘭, Op.Ge, 40)),
                    Note = "事件中的選擇與結緣無關。",
                    Next = To("蘭_雪山"),
                },
                new MapNode
                {
                    Id = "蘭_雪山", Title = "雪山行", Kind = NodeKind.Branch, Lane = 2, Row = 5,
                    Start = T(3, 6, 1), End = T(3, 6, 1), Where = "第三年六月上旬唐門月會",
                    Reqs = Rq(Lover(5)),
                    Note = "出發前身上要有酒（雪山之約要用）。優先度：菊花酒（擊敗第三香後擲≥70「放生」再≥60「都沒有」）＞猴兒酒（臥雲崗回程大師兄彌留時選「是你藏起來不讓喝的猴兒酒」，三月上旬後山覓得）＞其他酒。雪山行有很多生死簿事件。",
                    See = To("雪山行"),
                    Options = new[]
                    {
                        O("選「雪山行」", "與夏侯蘭前往雪山。", To("蘭_之約"), null, Flag("M10017", "已選雪山行")),
                        O("不選", "六月中旬夏侯蘭獨自前往雪山，結緣線中斷。"),
                    },
                },
                new MapNode
                {
                    Id = "蘭_之約", Title = "雪山之約", Kind = NodeKind.Branch, Lane = 2, Row = 6,
                    Start = T(3, 7, 2), End = T(3, 7, 2), Where = "第三年七月中旬",
                    Reqs = Rq(Lover(5), Rel(R.夏侯蘭, Op.Ge, 49), Note("身上有酒")),
                    Options = new[]
                    {
                        O("三次都選「抵抗」，第四次天命擲到 99",
                          "第二次需身上有酒、第三次需心上人為夏侯蘭；第四次上限為 50＋好感（≥49 才有機會、≥99 必成）。接著對決夏侯蘭獲勝 → 獲得「夏侯蘭的髮帶」完成結緣。",
                          To("蘭_決策"), null, Flag("G11005", "已與夏侯蘭結緣")),
                        O("接受傳功或抵抗失敗", "夏侯蘭逝世，獲得紀念技能玄黃納心綱。"),
                        O("對決夏侯蘭落敗", "生死簿74《與花合葬》。"),
                    },
                    Next = To("蘭_邀請"),
                },
                new MapNode
                {
                    Id = "蘭_邀請", Title = "邀請夏侯蘭", Kind = NodeKind.Event, Lane = 4, Row = 7,
                    Start = T(3, 8, 3), End = T(3, 8, 3), Where = "第三年八月下旬從大門下山",
                    Reqs = Rq(Flag("G11005", "與夏侯蘭結緣")),
                    Note = "無論如何她都不會同行武林大會。選「不是」再「襲擊夏侯蘭」並落敗，可解鎖防具密密縫（素質不如護心鏡）。",
                },
                new MapNode
                {
                    Id = "蘭_決策", Title = "你的決策（夏侯蘭結緣後）", Kind = NodeKind.Branch, Lane = 2, Row = 7,
                    Start = T(3, 12, 1), End = T(4, 1, 3), Where = "第三年十二月上旬",
                    Note = "集思廣益可以訪問夏侯蘭。",
                    See = To("眾人的決策"),
                    Options = new[]
                    {
                        決策退隱("練功場及男弟子房劇情差分；汗青書25《退隱江湖》改為與夏侯蘭上雪山版本，道德≥60 則為31《傳說之人》。", To("E25", "E31"), Flag("G11005", "與夏侯蘭結緣")),
                        決策頑抗("進入伴侶之約與唐門圍攻戰。", To("蘭_圍攻")),
                        決策西武林("進入東西武林盟會戰。", To("蘭_會戰")),
                    },
                },
                new MapNode
                {
                    Id = "蘭_圍攻", Title = "伴侶之約・唐門圍攻戰", Kind = NodeKind.Event, Lane = 2, Row = 8, Route = 頑抗,
                    Start = T(4, 1, 2), End = T(4, 2, 2), Where = "第四年一月中旬（西武林失敗則二月中旬）",
                    Note = "伴侶之約從大門下山陪伴夏侯蘭；另有結緣者時被修羅場取代，修羅場落敗且未與葉雲裳結緣 → 生死簿82。樊嘯天拜師夏侯蘭時她不參加終戰（不影響結局）。",
                    See = To("圍攻戰"),
                    Options = new[]
                    {
                        O("聽從夏侯蘭建議離開唐門", "汗青書34《行屍走肉》。", To("E34")),
                        O("不聽從，終戰獲勝", "汗青書43《失寵》。", To("E43")),
                    },
                },
                new MapNode
                {
                    Id = "蘭_會戰", Title = "東西武林盟會戰・眉山決戰", Kind = NodeKind.Event, Lane = 4, Row = 8, Route = 西武林,
                    Start = T(4, 2, 2), End = T(4, 3, 1), Where = "第四年二月中旬會戰 → 三月上旬眉山決戰",
                    Note = "建議先練完龍吟功；大院對決擲到≥65「來得及時」以外的結果。眉山決戰戰勝可達成風雲史《雪山派大弟子》。",
                    See = To("東西武林盟會戰"),
                    Options = new[]
                    {
                        O("唐門出擊、固守外堡，戰後「我...聽妳的」", "汗青書34《行屍走肉》。", To("E34")),
                        O("眉山決戰擊敗武林盟主", "汗青書48《武林傳奇》，結局畫面與夏侯蘭相伴。", To("E48")),
                    },
                },
            },
        };

        // ================= 龍湘 =================
        private static MapSheet 龍湘() => new MapSheet
        {
            Tab = "龍湘", Title = "龍湘結緣線", Lover = 8, Bond = Flag("G11003", "與龍湘結緣"),
            Intro = "好感集中在錦香宮與武林大會：收留被揭發的龍湘回唐門，之後化解她的心魔，決策不選退隱，就能在年底結緣。",
            Sections = new[]
            {
                new MapSection { Row = 0, Title = "第一年～第三年八月　累積好感", Lanes = new[] { "", "", "結緣必要", "", "客棧線・龍淵" } },
                new MapSection { Row = 1, Title = "第三年九月～十月　錦香宮・武林大會", Lanes = 必要 },
                new MapSection { Row = 3, Title = "第三年十一月起　結緣", Lanes = new[] { "", "", "結緣必要", "", "西武林盟" } },
            },
            Nodes = new[]
            {
                new MapNode
                {
                    Id = "湘_好感", Title = "初識龍湘・累積好感", Kind = NodeKind.Event, Lane = 2, Row = 0,
                    Start = T(1, 9, 1), End = T(3, 9, 2), Where = "第一年九月初識龍湘起",
                    Reqs = Rq(Rel(R.龍湘, Op.Ge, 40)),
                    Note = "唐門外堡可刷「龍湘聽講」（需先觸發初識福韞、龍湘遭竊）；錦香宮可刷華音閣。",
                    Next = To("湘_錦香", "湘_龍淵"),
                },
                new MapNode
                {
                    Id = "湘_龍淵", Title = "巧遇龍淵（客棧線）", Kind = NodeKind.Event, Lane = 4, Row = 1, Route = 客棧,
                    Start = T(2, 8, 3), End = T(2, 8, 3), Where = "第二年八月下旬，不留學・住客棧",
                    Note = "經歷過巧遇龍淵，龍湘結緣時龍淵有劇情差分並新增「守信用」，可得《酩酊玄劍掌》；龍淵好感8以上可得《龍淵七絕》。第二年六月前葉雲裳在唐門且好感≥30 會進不了客棧線。",
                    See = To("客棧線"),
                },
                new MapNode
                {
                    Id = "湘_錦香", Title = "錦香宮作客・龍湘夜遊", Kind = NodeKind.Branch, Lane = 2, Row = 1,
                    Start = T(3, 9, 3), End = T(3, 10, 1), Where = "第三年九月下旬～十月上旬",
                    See = To("錦香宮"),
                    Options = new[]
                    {
                        O("龍湘好感≥40 → 觸發龍湘夜遊", "過程不重要。", To("湘_大會"), Rq(Rel(R.龍湘, Op.Ge, 40)), FlagIs("M60016", 2, "已前往錦香宮作客")),
                        O("不去錦香宮或好感不足", "沒有龍湘夜遊，結緣線中斷。"),
                    },
                },
                new MapNode
                {
                    Id = "湘_大會", Title = "武林大會：收留龍湘", Kind = NodeKind.Branch, Lane = 2, Row = 2,
                    Start = T(3, 10, 2), End = T(3, 10, 2), Where = "第三年十月中旬，錦香宮被揭發後",
                    Note = "只要收留過龍湘，西武林線會戰她都會參戰，不一定要結緣。",
                    See = To("武林大會"),
                    Options = new[]
                    {
                        O("「不顧旁人冷眼收留龍湘」（好感≥30）", "龍湘加入唐門。", To("湘_心事"), Rq(Rel(R.龍湘, Op.Ge, 30)), Flag("M60036", "唐門收容龍湘")),
                        O("不收留", "結緣線中斷。"),
                    },
                },
                new MapNode
                {
                    Id = "湘_心事", Title = "龍湘練劍・龍湘心事", Kind = NodeKind.Branch, Lane = 2, Row = 3,
                    Start = T(3, 11, 2), End = T(4, 1, 3), Where = "第三年十一月中旬起，後山挑柴隨機觸發",
                    Reqs = Rq(Lover(8)),
                    Options = new[]
                    {
                        O("「我來告訴你，用劍」→ 閒聊 →「我有心上人，就是妳」",
                          "閒聊完「初識之時」「錦香宮」後問「龍湘的心事」：修養中庸以上擲到≥60「說」；暴躁以下選「不了我不問了」擲到≥80，或再次對決（落敗後別選「我輸了，聽不得妳的心事了」）。閒聊「心魔」被問心上人時回答「我有心上人，就是妳」。",
                          To("湘_決策"), null, Flag("M60035", "已觸發龍湘的心事")),
                        O("在結緣時間點之後才觸發", "第四年一月下旬前都能觸發，但過了結緣時間點就無法結緣。"),
                    },
                },
                new MapNode
                {
                    Id = "湘_決策", Title = "武林盟對策：你的決策", Kind = NodeKind.Branch, Lane = 2, Row = 4,
                    Start = T(3, 12, 1), End = T(3, 12, 1), Where = "第三年十二月上旬",
                    See = To("眾人的決策"),
                    Options = new[]
                    {
                        O("遣散唐門（退隱）", "有其他結緣者且心上人為龍湘時，後山選「湘姊，妳要不要跟我來呢?」並在對決後「我是不會放妳走的」→ 汗青書24《趕海人》；其他情況觸發龍湘三年之約。都無法結緣。",
                          To("E24"), null, FlagIs("M70015", 1, "決策＝退隱江湖")),
                        O("頑抗到底或成立西武林盟", "進入龍湘結緣。", To("湘_結緣")),
                    },
                },
                new MapNode
                {
                    Id = "湘_結緣", Title = "龍湘結緣", Kind = NodeKind.Event, Lane = 2, Row = 5, Route = 頑抗 | 西武林,
                    Start = T(3, 12, 3), End = T(4, 1, 3), Where = "頑抗到底：第三年十二月下旬；西武林盟：第四年一月下旬",
                    Reqs = Rq(Rel(R.龍湘, Op.Ge, 60), Flag("M60035", "已觸發龍湘的心事")),
                    Note = "獲得「龍湘的髮簪」即完成結緣；戰鬥輸贏都可以，但輸給龍湘（8 回合後自動落敗除外）會無法與龍淵對話。結緣後下山泡溫泉可觸發陪伴（心相+50）。",
                    Next = To("湘_圍攻", "湘_會戰"),
                },
                new MapNode
                {
                    Id = "湘_圍攻", Title = "伴侶之約・唐門圍攻戰", Kind = NodeKind.Event, Lane = 2, Row = 6, Route = 頑抗,
                    Start = T(4, 1, 2), End = T(4, 2, 2), Where = "第四年一月中旬（西武林失敗則二月中旬）",
                    Note = "伴侶之約到後山與龍湘約會；另有結緣者時會被修羅場取代。",
                    See = To("圍攻戰"),
                    Options = new[] { O("終戰獲勝", "汗青書44《雞腿大亨》。", To("E44")) },
                },
                new MapNode
                {
                    Id = "湘_會戰", Title = "東西武林盟會戰・眉山決戰", Kind = NodeKind.Event, Lane = 4, Row = 6, Route = 西武林,
                    Start = T(4, 2, 2), End = T(4, 3, 1), Where = "第四年二月中旬會戰 → 三月上旬眉山決戰",
                    Note = "大院對決擲到≥65「來得及時」以外的結果，龍湘對戰時再擲≥50 有覺醒事件。結緣後戰後尋找龍湘時聽不到〈長相思令．詠雞腿〉。眉山決戰戰勝可達成風雲史《忘憂俠侶》。",
                    See = To("東西武林盟會戰"),
                    Options = new[] { O("眉山決戰擊敗武林盟主", "汗青書48《武林傳奇》，結局畫面與龍湘相伴。", To("E48")) },
                },
            },
        };

        // ================= 上官螢（未實裝） =================
        private static MapSheet 上官螢() => new MapSheet
        {
            Tab = "上官螢", Title = "上官螢（目前無法結緣）", Lover = 4,
            Intro = "wiki 標示目前版本無法攻略：攻略旗標 G11001 沒有劇本會設為已結緣。這裡整理已實裝、會依上官螢好感或心上人改變的事件。",
            Sections = new[]
            {
                new MapSection { Row = 0, Title = "第一年", Lanes = new[] { "", "", "相關事件", "", "" } },
                new MapSection { Row = 2, Title = "第二年", Lanes = new[] { "", "", "相關事件", "", "" } },
                new MapSection { Row = 4, Title = "第三年", Lanes = new[] { "", "", "相關事件", "", "" } },
            },
            Nodes = new[]
            {
                new MapNode
                {
                    Id = "螢_財務", Title = "唐門財務改善：上官螢來訪", Kind = NodeKind.Branch, Lane = 2, Row = 0,
                    Start = T(1, 8, 1), End = T(1, 8, 1), Where = "第一年八月上旬",
                    Options = new[]
                    {
                        O("正常招待", "認識上官螢。", To("螢_郊遊"), null, Flag("G10001", "已認識上官螢")),
                        O("「妳若愛惜性命，嘿嘿……(威脅)」", "對決上官螢；落敗 → 生死簿49「上官螢的制裁」。", To("螢_郊遊")),
                    },
                },
                new MapNode
                {
                    Id = "螢_郊遊", Title = "下山郊遊", Kind = NodeKind.Branch, Lane = 2, Row = 1,
                    Start = T(1, 9, 3), End = T(1, 9, 3), Where = "第一年九月下旬",
                    Reqs = Rq(Flag("E12001", "上官螢在唐門")),
                    Options = new[]
                    {
                        O("一般同遊", "上官螢在唐門時好感+1。", To("螢_江陵")),
                        O("阻止葉雲裳吃甜食", "對決上官螢：上官螢好感-4、小師妹與葉雲裳好感-2，並記下「毆打上官螢」。", To("螢_江陵")),
                    },
                },
                new MapNode
                {
                    Id = "螢_江陵", Title = "江陵：上官螢參戰", Kind = NodeKind.Event, Lane = 2, Row = 2, Route = 崆峒,
                    Start = T(2, 9, 1), End = T(2, 10, 1), Where = "第二年九月～十月江陵圍城",
                    Reqs = Rq(Flag("M30013", "上官螢參戰江陵大戰")),
                    Note = "江陵之戰後她被南宮深退婚。",
                    See = To("江陵決戰"),
                    Next = To("螢_內戰"),
                },
                new MapNode
                {
                    Id = "螢_內戰", Title = "唐門內戰：上官螢的遊說", Kind = NodeKind.Branch, Lane = 2, Row = 3,
                    Start = T(2, 12, 3), End = T(2, 12, 3), Where = "第二年十二月下旬",
                    See = To("門派宣戰"),
                    Options = new[]
                    {
                        O("「反駁」（唐錚好感≥40 或心上人為上官螢）", "遊說成敗決定門人減少的數量；上官螢倒戈則接「恩怨兩清」。", To("螢_問候"),
                          Rq(Either(Note("唐錚好感≥40"), Lover(4))), Flag("M40047", "內戰時上官螢幫助唐門")),
                        O("不反駁", "門人減少較多。", To("螢_問候")),
                    },
                },
                new MapNode
                {
                    Id = "螢_問候", Title = "武林大會：上官螢問候", Kind = NodeKind.Branch, Lane = 2, Row = 4,
                    Start = T(3, 10, 2), End = T(3, 10, 2), Where = "第三年十月中旬",
                    Options = new[]
                    {
                        O("上官螢好感≥30", "得知她要遠嫁日本；心上人為上官螢時心相-50，否則-20。", To("螢_對策"),
                          Rq(Rel(R.上官螢, Op.Ge, 30)), Flag("M60026", "得知上官螢遠嫁日本")),
                        O("好感<30", "一般問候；小師妹或葉雲裳同行時上官螢好感-1。", To("螢_對策")),
                    },
                },
                new MapNode
                {
                    Id = "螢_對策", Title = "上官對策：挑戰上官隼", Kind = NodeKind.Branch, Lane = 2, Row = 5,
                    Start = T(3, 10, 2), End = T(3, 10, 2), Where = "第三年十月中旬",
                    See = To("武林大會"),
                    Options = new[]
                    {
                        O("心上人為上官螢且得知遠嫁日本 → 挑戰", "進入「私人情仇」並為上官螢出戰。", To("螢_未實裝"),
                          Rq(Lover(4), Flag("M60026", "得知上官螢遠嫁日本")), Flag("M60073", "為上官螢出戰")),
                        O("其他情況挑戰上官隼", "一般挑戰，不算為上官螢出戰。", To("螢_未實裝")),
                        O("不挑戰", "沒有後續。", To("螢_未實裝")),
                    },
                },
                new MapNode
                {
                    Id = "螢_未實裝", Title = "集思廣益・伴侶之約（未實裝）", Kind = NodeKind.Event, Lane = 2, Row = 6,
                    Start = T(3, 12, 1), End = T(4, 2, 2), Where = "第三年十二月起",
                    Reqs = Rq(Flag("E12001", "上官螢在唐門"), Flag("M70062", "可陪伴上官螢")),
                    Note = "集思廣益「訪問上官螢」需心相>20 且上官螢在唐門，目前版本未解鎖；伴侶之約、修羅場雖有上官螢的判定，但結緣旗標沒有來源。汗青書39《富甲一方》目前無法達成。",
                },
            },
        };

        // ================= 魏菊（未實裝） =================
        private static MapSheet 魏菊() => new MapSheet
        {
            Tab = "魏菊", Title = "魏菊（目前無法結緣）", Lover = 7,
            Intro = "wiki 標示目前版本無法攻略：攻略旗標 G11007 沒有劇本會設定。這裡整理已實裝、會依魏菊好感或心上人改變的事件。",
            Sections = new[]
            {
                new MapSection { Row = 0, Title = "第二年　崆峒留學・江陵", Lanes = new[] { "", "", "相關事件", "", "" } },
                new MapSection { Row = 2, Title = "第三年", Lanes = new[] { "", "", "相關事件", "", "" } },
            },
            Nodes = new[]
            {
                new MapNode
                {
                    Id = "菊_留學", Title = "魏菊招攬・鶴手出遊", Kind = NodeKind.Branch, Lane = 2, Row = 0, Route = 崆峒,
                    Start = T(2, 3, 1), End = T(2, 3, 2), Where = "第二年三月上旬崆峒門派 → 三月中旬鶴手出遊",
                    Note = "鶴手出遊「想當仙人」可偶遇魏菊；處世≥80 時魏菊好感-2，<80 時+2。",
                    See = To("崆峒留學"),
                    Options = new[]
                    {
                        O("「我要去玄功門」", "直接加入玄功門（魏菊好感+1），不必抽籤；記名玄功門才有「魏菊挽留」。", To("菊_江陵"), null, FlagIs("M20011", 3, "崆峒門派＝玄功門")),
                        O("抽籤或選其他門派", "之後仍能在鶴手出遊遇到魏菊。", To("菊_江陵")),
                    },
                },
                new MapNode
                {
                    Id = "菊_江陵", Title = "江陵：法王就義・玄功傳承", Kind = NodeKind.Branch, Lane = 2, Row = 1, Route = 崆峒,
                    Start = T(2, 10, 1), End = T(2, 10, 1), Where = "第二年十月上旬",
                    Note = "接受魏菊療傷死亡率高（內功最低 46，建議 50 以上）。戰後休憩：魏菊出戰王二壯且心上人不是魏菊時有「魏菊關心」，之後可能記下「魏菊的悲劇」。",
                    See = To("江陵決戰"),
                    Options = new[]
                    {
                        O("心上人為魏菊：替魏菊接掌", "魏菊出戰王二壯時：魏菊好感+3、性情+2。", To("菊_大會"), Rq(Lover(7))),
                        O("玄功傳承：「當了崆峒弟子，還能留在唐門嗎？」", "加入崆峒派、獲得《鐵琵琶功》（需魏菊出戰、心上人為魏菊、有鐵琵琶功）。", To("菊_大會"), Rq(Lover(7))),
                        O("玄功傳承：「我才不要當什麼崆峒弟子」", "戰役拒加崆峒：勝利則與崆峒結死仇；落敗生死簿21。", To("菊_大會")),
                    },
                },
                new MapNode
                {
                    Id = "菊_大會", Title = "遭遇崆峒派・魏菊侍酒", Kind = NodeKind.Event, Lane = 2, Row = 2,
                    Start = T(3, 9, 1), End = T(3, 10, 2), Where = "第三年九月上旬旅途 → 十月中旬武林大會",
                    Note = "旅途遭遇崆峒派時處世≥60 魏菊好感-1；魏菊侍酒：心上人為魏菊心相+50、變心-2，否則心相+20、變心+3。崆峒神威更新補充了她暗中布局顛覆崆峒的設定，劇情見小竹、小梅分頁的崆峒爭奪戰。",
                    See = To("武林大會", "竹_崆峒"),
                    Next = To("菊_未實裝"),
                },
                new MapNode
                {
                    Id = "菊_未實裝", Title = "集思廣益・出使崆峒", Kind = NodeKind.Branch, Lane = 2, Row = 3,
                    Start = T(3, 12, 1), End = T(4, 1, 3), Where = "第三年十二月起",
                    Note = "汗青書30《山林隱逸》、42《隱世大儒》需與魏菊結緣，目前無法達成。",
                    See = To("眾人的決策"),
                    Options = new[]
                    {
                        O("集思廣益「訪問小菊」", "聽取魏菊對武林盟的意見。", null, null, Flag("M70008", "已訪問小菊")),
                        O("西武林盟：魏菊出使崆峒", "由魏菊代表唐門出使崆峒派。", null, null, Flag("M70032", "魏菊出使崆峒派")),
                    },
                },
            },
        };

        // ================= 瑞杏（未實裝） =================
        private static MapSheet 瑞杏() => new MapSheet
        {
            Tab = "瑞杏", Title = "瑞杏（杏花仙，目前無法結緣）", Lover = 1,
            Intro = "瑞杏目前只能透過夢境與錦囊成為心上人，沒有結緣事件（官方規劃之後以免費 DLC 推出）。這裡整理和她有關、會影響命運點數的事件。",
            Sections = new[]
            {
                new MapSection { Row = 0, Title = "夢境與許願", Lanes = new[] { "", "", "相關事件", "", "" } },
            },
            Nodes = new[]
            {
                new MapNode
                {
                    Id = "杏_錦囊", Title = "變心夢：杏梢錦囊", Kind = NodeKind.Event, Lane = 2, Row = 0,
                    Start = T(1, 4, 1), End = Forever, Where = "任意時間的變心夢",
                    Reqs = Rq(Stat(G.命運, "命運", Op.Ge, 100)),
                    Note = "變心夢中命運≥100 才能打開杏梢錦囊（「不要忘記我。」），強制把心上人變成瑞杏。",
                    Next = To("杏_夢見"),
                },
                new MapNode
                {
                    Id = "杏_夢見", Title = "江陵決戰前夜：夢見瑞杏", Kind = NodeKind.Branch, Lane = 2, Row = 1, Route = 崆峒,
                    Start = T(2, 10, 1), End = T(2, 10, 1), Where = "第二年十月上旬，失眠時「堅持怒睡」",
                    Reqs = Rq(Stat(G.命運, "命運", Op.Ge, 50), Lover(1)),
                    See = To("江陵決戰"),
                    Options = new[]
                    {
                        O("「好」", "心相+100。", To("杏_許願")),
                        O("「來下棋吧」", "心相+100、學問+2、修養+1。", To("杏_許願")),
                        O("「妳侍寢吧」", "心相+40、道德-1、性情+1。", To("杏_許願")),
                    },
                },
                new MapNode
                {
                    Id = "杏_許願", Title = "向瑞杏許願", Kind = NodeKind.Branch, Lane = 2, Row = 2,
                    Start = T(2, 10, 1), End = T(3, 1, 3), Where = "夢鄉中向瑞杏許願",
                    Note = "崆峒神威更新後，許願救回大師兄需要更多命運點數；缺命運點時開局可以保留一些不點。",
                    Options = new[]
                    {
                        O("「希望人間溫柔點」", "得到 30 命運點數（小竹線解小梅屍心丹毒需要命運 50）。"),
                        O("「無論如何復活唐布衣」", "支付命運點數，特定時機復活唐布衣。", null, null, Flag("M30030", "已向瑞杏許願")),
                        O("「不想活了」", "趙活死亡。"),
                    },
                },
            },
        };
    }
}
