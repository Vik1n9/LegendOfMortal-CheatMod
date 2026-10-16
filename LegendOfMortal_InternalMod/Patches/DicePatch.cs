using HarmonyLib;
using LegendOfMortal_InternalMod.Core;
using Mortal.Story;
using UnityEngine;
using Cfg = LegendOfMortal_InternalMod.Config.ModConfig;

namespace LegendOfMortal_InternalMod.Patches
{
    /// <summary>
    /// 骰子控制唯一實作（修改器側不再重做骰子，直接共用此處）。
    /// 舊版失效主因：CheckPointManager.Dice 被當成 Dice(ref int random)，
    /// 新版是 Dice(string name, int random) : DiceCheckResult。
    /// </summary>
    internal static class DicePatch
    {
        public static int ForcedValue
        {
            get => Cfg.ForcedDiceValue.Value;
            set => Cfg.ForcedDiceValue.Value = value;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(DiceMenuDialog), "Awake")]
        public static void Awake_Postfix(DiceMenuDialog __instance)
        {
            // 新版 Awake 是 protected override + New Input System，
            // Postfix 不需重建 InputField（舊包 Assets.mortaldice 已遺失時會報錯），
            // 這裡只做相容：不清掉舊邏輯，但避免空引用。
            // 骰值輸入改由遊戲風格主視窗提供（見 UI/CheatWindow），不再依賴外掛 prefab。
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(DiceMenuDialog), "OnEnable")]
        public static void OnEnable_Postfix(DiceMenuDialog __instance)
        {
            // 同上：不再強制重置外部 InputField，避免空引用。
        }

        // 新版簽名：Dice(string name, int random)
        // Harmony 允許 Prefix 只取子集，這裡取 (string name, ref int random) 即可。
        [HarmonyPrefix]
        [HarmonyPatch(typeof(CheckPointManager), "Dice")]
        public static void Dice_Prefix(string name, ref int random)
        {
            int v = ForcedValue;
            if (v != 0)
            {
                random = v;
                var dlg = Object.FindObjectOfType<DiceMenuDialog>();
                if (dlg != null)
                {
                    // 同步顯示值，否則轉盤顯示與判定對不上
                    dlg.SetPrivateField("_currentRandomValue", v);
                }
            }
        }

        // Postfix 保險：若強制值非0，直接改 ResultCount 的 Random 源頭。
        // DiceCheckResult.ResultCount = Random + Additions，所以改 Random 最乾淨。
        [HarmonyPostfix]
        [HarmonyPatch(typeof(CheckPointManager), "Dice")]
        public static void Dice_Postfix(string name, int random, ref DiceCheckResult __result)
        {
            int v = ForcedValue;
            if (v != 0 && __result != null)
            {
                __result.Random = v;
            }
        }

        // 舊版直接 return false 強制 CheckRevolution()=true（永遠可重骰/命運點）。
        // 新版邏輯相同（命運>0 或 _useFatePoint 即 true），保留但改為可配置：
        // 若 ForcedValue==0 則不干預，避免新手誤觸無限重骰破壞平衡。
        [HarmonyPrefix]
        [HarmonyPatch(typeof(DiceMenuDialog), "CheckRevolution")]
        public static bool CheckRevolution_Prefix(ref bool __result)
        {
            // 只有開啟骰子控制且有指定值時，才強制允許重骰；否則走原邏輯。
            if (Cfg.EnableDiceController.Value && ForcedValue != 0)
            {
                __result = true;
                return false;
            }
            return true;
        }
    }
}
