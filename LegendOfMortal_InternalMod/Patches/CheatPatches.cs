using HarmonyLib;
using LegendOfMortal_InternalMod.Core;
using Mortal.Battle;
using Mortal.Core;

namespace LegendOfMortal_InternalMod.Patches
{
    /// <summary>
    /// 戰鬥作弊（Harmony 補丁，不寫記憶體）：
    /// - 群戰無敵：我方 TakeDamage 直接返回 false（不受傷）
    /// - 群戰秒殺：敵方 TakeDamage 傷害放大（透過 __instance 判斷是否為玩家陣營）
    /// 為避免誤判陣營，採用保守策略：
    /// GodBattle 開啟時，我方受傷直接免疫；秒殺需手動再開子開關（見 CheatWindow）。
    /// 單挑（Mortal.Combat）因結構類似，若找不到對應類別則自動跳過，不會崩潰。
    /// </summary>
    internal static class BattleCheatPatch
    {
        public static bool OneHitKill = false;
        public static bool InfStamina = false;
        public static bool InfCooldown = false;
        public static bool InfTime = false;

        // Character.TakeDamage(int damage, bool isCriticalHit, bool canDodge, Character attacker)
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Character), "TakeDamage")]
        public static bool TakeDamage_Prefix(Character __instance, ref int damage, bool isCriticalHit, bool canDodge, Character attacker)
        {
            if (!LegendOfMortal_InternalMod.Config.ModConfig.EnableCheats.Value) return true;
            try
            {
                // 我方無敵：若受擊者是玩家操控（IsPlayer）則免疫
                if (LegendOfMortal_InternalMod.Config.ModConfig.GodBattle.Value)
                {
                    // Character 有無 IsPlayer？用 Entity 類型判斷，取不到則保守：若 attacker==null 仍放行
                    // 這裡用 try 反射 IsPlayer / IsEnemy，失敗則只對「非敵方攻擊者」免疫的簡化邏輯
                    if (IsPlayerSide(__instance))
                    {
                        damage = 0;
                        return false; // 跳過原函數，直接無傷
                    }
                }
                if (OneHitKill && !IsPlayerSide(__instance))
                {
                    damage = 999999;
                    return true; // 放行但傷害拉滿
                }
            }
            catch { }
            return true;
        }

        private static bool IsPlayerSide(Character c)
        {
            try
            {
                var entity = c.Entity;
                if (entity == null) return true;
                return entity.CurrentType != Mortal.Battle.EntityType.Enemy;
            }
            catch { return true; }
        }

        // SkillButton.Update：冷卻直接拉到 0.05
        [HarmonyPostfix]
        [HarmonyPatch(typeof(SkillButton), "Update")]
        public static void SkillCooldown_Postfix(object __instance)
        {
            if (!LegendOfMortal_InternalMod.Config.ModConfig.EnableCheats.Value || !InfCooldown) return;
            try
            {
                var t = __instance.GetType();
                var f = HarmonyLib.AccessTools.Field(t, "_currentCooldownTime");
                if (f != null && (float)f.GetValue(__instance) > 0.05f)
                    f.SetValue(__instance, 0.05f);
            }
            catch { }
        }

        // GameLevelManager.Update：時間鎖0
        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameLevelManager), "Update")]
        public static void BattleTime_Postfix(object __instance)
        {
            if (!LegendOfMortal_InternalMod.Config.ModConfig.EnableCheats.Value || !InfTime) return;
            try
            {
                var f = HarmonyLib.AccessTools.Field(__instance.GetType(), "_currentSessionTime");
                if (f != null) f.SetValue(__instance, 0);
            }
            catch { }
        }
    }

    /// <summary>
    /// 養成作弊：練功/鍛造/煉丹/好感/行動點（取代 Ctrl+Num 系列 AOB）。
    /// 全部用 Prefix 返回 false 跳過扣費，或 Postfix 補值，安全且跨版本。
    /// </summary>
    internal static class UpkeepCheatPatch
    {
        // PlayerStatData.Add(GameStatType type, int value)：
        // 無限行動點：若是扣除行動次數（value<0）且開關開，直接跳過
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Mortal.Core.PlayerStatData), "Add")]
        public static bool Add_Prefix(GameStatType type, int value)
        {
            if (!LegendOfMortal_InternalMod.Config.ModConfig.EnableCheats.Value) return true;
            if (type == GameStatType.行動次數 && value < 0 && LegendOfMortal_InternalMod.Config.ModConfig.InfAction.Value)
                return false;
            return true;
        }
    }

    internal static class PracticeCheatPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Mortal.Core.MartialLevelData), "Consumable")]
        public static bool Consumable_Prefix()
        {
            if (!LegendOfMortal_InternalMod.Config.ModConfig.EnableCheats.Value) return true;
            if (LegendOfMortal_InternalMod.Config.ModConfig.InfPractice.Value) return false;
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Mortal.Core.Book), "Learn")]
        public static bool Learn_Prefix(Mortal.Core.Book __instance, int point, ref bool __result)
        {
            if (!LegendOfMortal_InternalMod.Config.ModConfig.EnableCheats.Value) return true;
            if (!LegendOfMortal_InternalMod.Config.ModConfig.FastPractice.Value) return true;
            try { __result = __instance.DirectLevelUp(); }
            catch { __result = false; }
            return false;
        }
    }

    internal static class FriendshipCheatPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Mortal.Core.RelationshipStat), "AddValue")]
        public static bool AddValue_Prefix(int value)
        {
            if (!LegendOfMortal_InternalMod.Config.ModConfig.EnableCheats.Value) return true;
            if (LegendOfMortal_InternalMod.Config.ModConfig.MaxFriendship.Value && value < 0) return false;
            return true;
        }
    }

    internal static class CombatSinglePatch
    {
        // 單挑扣血唯一入口：CombatActionController.ModifyHealth(int value)，value<0 為扣血。
        // （CombatCharacterController.TakeDamage(int) 只是攻擊方播放受擊動畫，不扣血。）
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Mortal.Combat.CombatActionController), "ModifyHealth")]
        public static void ModifyHealth_Prefix(Mortal.Combat.CombatActionController __instance, ref int value)
        {
            if (!LegendOfMortal_InternalMod.Config.ModConfig.EnableCheats.Value) return;
            if (!LegendOfMortal_InternalMod.Config.ModConfig.GodCombat.Value || value >= 0) return;
            try
            {
                var mgr = Mortal.Combat.CombatManager.Instance;
                if (mgr != null && ReferenceEquals(mgr.PlayerAction, __instance)) value = 0;
            }
            catch { }
        }
    }
}
