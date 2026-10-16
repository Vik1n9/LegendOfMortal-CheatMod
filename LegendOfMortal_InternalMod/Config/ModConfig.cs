using BepInEx.Configuration;
using UnityEngine;

namespace LegendOfMortal_InternalMod.Config
{
    public static class ModConfig
    {
        public static ConfigEntry<bool> EnableDiceController;
        public static ConfigEntry<bool> EnableOptionInfo;
        public static ConfigEntry<bool> EnableCheats;
        public static ConfigEntry<KeyCode> MainMenuKey;
        public static ConfigEntry<KeyCode> SpeedUpKey;

        // 骰子
        public static ConfigEntry<int> ForcedDiceValue;

        // 作弊細項
        public static ConfigEntry<bool> GodCombat;
        public static ConfigEntry<bool> GodBattle;
        public static ConfigEntry<bool> InfAction;
        public static ConfigEntry<bool> InfPractice;
        public static ConfigEntry<bool> FastPractice;
        public static ConfigEntry<bool> InfForgeAlchemy;
        public static ConfigEntry<bool> FastForgeAlchemy;
        public static ConfigEntry<bool> MaxFriendship;
        public static ConfigEntry<float> GameSpeed;

        public static void Init(ConfigFile cfg)
        {
            EnableDiceController = cfg.Bind("總開關", "啟用骰子控制", true, "控制天命骰子隨機值（修復新版 API）");
            EnableOptionInfo = cfg.Bind("總開關", "啟用選項結果顯示", true, "在選項文字後顯示收益、在養成點顯示事件/任務提示");
            EnableCheats = cfg.Bind("總開關", "啟用作弊功能", true, "戰鬥、養成、數值等作弊功能，全部走遊戲內 API，不寫外部記憶體");

            MainMenuKey = cfg.Bind("快捷鍵", "主選單快捷鍵", KeyCode.F1, "遊戲風格作弊/查詢視窗的開關（看起來像遊戲介面的一部分）");
            SpeedUpKey = cfg.Bind("快捷鍵", "加速鍵", KeyCode.LeftControl, "按住加速（劇情跳過加速用）");

            ForcedDiceValue = cfg.Bind("骰子", "指定骰值", 0, "0=不指定；非0則強制 CheckPointManager.Dice 的 random（最終結果仍會吃屬性/好感加成）");

            GodCombat = cfg.Bind("戰鬥", "單挑無敵秒殺", false, "單挑時我方不受傷害");
            GodBattle = cfg.Bind("戰鬥", "群戰無敵秒殺", false, "群戰時我方不受傷害");
            InfAction = cfg.Bind("養成", "無限行動點", false, "行動點用完自動回滿");
            InfPractice = cfg.Bind("養成", "練功免消耗", false, "練功時不扣除消耗");
            FastPractice = cfg.Bind("養成", "快速練功", false, "每次練功直接升一級");
            InfForgeAlchemy = cfg.Bind("養成", "鍛造煉丹免消耗", false, "預留，尚未實作");
            FastForgeAlchemy = cfg.Bind("養成", "快速鍛造煉丹", false, "預留，尚未實作");
            MaxFriendship = cfg.Bind("養成", "好感只升不降", false, "好感只升不降（含虞小梅、郁竹結緣線）");
            GameSpeed = cfg.Bind("系統", "遊戲速度", 1f, "1=正常，範圍 0.5-5");
        }
    }
}
