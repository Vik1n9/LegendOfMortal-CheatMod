using BepInEx.Configuration;
using UnityEngine;

namespace LegendOfMortal_InternalMod.Config
{
    public static class ModConfig
    {
        public static ConfigEntry<bool> EnableOptionInfo;
        public static ConfigEntry<KeyCode> MainMenuKey;

        public static void Init(ConfigFile cfg)
        {
            EnableOptionInfo = cfg.Bind("總開關", "啟用選項結果顯示", true, "在選項文字後顯示收益、在養成點顯示事件/任務提示");
            MainMenuKey = cfg.Bind("快捷鍵", "主選單快捷鍵", KeyCode.F1, "唐門密卷 Lite 視窗的開關");
        }
    }
}
