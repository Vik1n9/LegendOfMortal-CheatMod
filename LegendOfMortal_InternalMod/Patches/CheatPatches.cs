using System;
using HarmonyLib;
using LegendOfMortal_InternalMod.Core;
using Mortal.Battle;
using Mortal.Core;
using UnityEngine;
using UnityEngine.UI;
using Cfg = LegendOfMortal_InternalMod.Config.ModConfig;

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

    /// <summary>
    /// 煉丹／鍛造升級作弊（兩者分開設定）：
    /// - 免消耗：升級時不扣毒藥／鍛造點數、銀兩與本次可升級次數。
    /// - 快速：每次點擊直接升一級（同樣不扣點數），對應「快速練功」。
    /// 只作用在 PoisonUpgradePanel／WeaponUpgradePanel，其他開發面板不受影響。
    /// </summary>
    internal static class ForgeAlchemyCheatPatch
    {
        private enum Kind { None, Alchemy, Forge }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Mortal.Core.UpgradePanel), "UpgradeButtonClick")]
        public static bool UpgradeButtonClick_Prefix(Mortal.Core.UpgradePanel __instance)
            => !TryUpgrade(__instance, false);

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Mortal.Core.UpgradePanel), "NextLevelButtonClick")]
        public static bool NextLevelButtonClick_Prefix(Mortal.Core.UpgradePanel __instance)
            => !TryUpgrade(__instance, true);

        // 遊戲原本會在點數／次數不足時把按鈕設為不可點；作弊開啟時強制保持可按。
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Mortal.Core.UpgradePanel), "UpdateButton")]
        public static void UpdateButton_Postfix(Mortal.Core.UpgradePanel __instance)
        {
            if (!IsEnabled(KindOf(__instance))) return;
            try
            {
                var data = GetField<Mortal.Core.UpgradeItemButton>(__instance, "_currentSelectedItem")?.Data;
                bool enable = data != null && !data.IsMaxLevel;
                ForceInteractable(__instance, "_upgradeButton", "_upgradeMenuToggleButton", enable);
                ForceInteractable(__instance, "_nextLevelButton", "_nextLevelMenuToggleButton", enable);
            }
            catch { }
        }

        private static bool TryUpgrade(Mortal.Core.UpgradePanel panel, bool fillLevel)
        {
            var kind = KindOf(panel);
            if (!IsEnabled(kind)) return false;
            try
            {
                var button = GetField<Mortal.Core.UpgradeItemButton>(panel, "_currentSelectedItem");
                var data = button?.Data;
                if (data == null || data.IsMaxLevel) return true;

                // 快速＝直接補滿這一級；免消耗但未開快速＝每次加一點。
                bool fast = kind == Kind.Alchemy ? Cfg.FastAlchemy.Value : Cfg.FastForge.Value;
                int points = (fast || fillLevel)
                    ? Math.Max(1, data.GetNextLevelRequirePoints())
                    : 1;

                bool levelup = data.Upgrade(points);
                if (levelup)
                {
                    if (!data.AlreadyDevelop) data.SetAlreadyDevelop(true);
                    if (data.IsMaxLevel) ApplyMaxRewards(panel, data);
                }

                data.UpdateStatus();
                button.UpdateStatus();
                Invoke(panel, "UpdateStatus");
                Invoke(panel, "UpdateButton");
                HideWarnings(panel);
                return true;
            }
            catch (Exception ex)
            {
                Plugin.LogError((kind == Kind.Alchemy ? "煉丹" : "鍛造") + "作弊: " + ex.Message);
                return true;
            }
        }

        private static bool IsEnabled(Kind kind)
        {
            if (!Cfg.EnableCheats.Value) return false;
            if (kind == Kind.Alchemy) return Cfg.InfAlchemy.Value || Cfg.FastAlchemy.Value;
            if (kind == Kind.Forge) return Cfg.InfForge.Value || Cfg.FastForge.Value;
            return false;
        }

        /// <summary>依面板名稱（優先）或點數來源判斷是煉丹還是鍛造。</summary>
        private static Kind KindOf(Mortal.Core.UpgradePanel panel)
        {
            try
            {
                string name = panel.gameObject.name;
                if (name.Contains("PoisonUpgrade")) return Kind.Alchemy;
                if (name.Contains("WeaponUpgrade")) return Kind.Forge;

                var pointData = GetField<Mortal.Core.GameStat>(panel, "_pointData");
                if (pointData != null)
                {
                    if (pointData.StatType == GameStatType.毒藥) return Kind.Alchemy;
                    if (pointData.StatType == GameStatType.鍛造) return Kind.Forge;
                }
            }
            catch { }
            return Kind.None;
        }

        // 滿級獎勵原本寫在 UpgradePanel.StartHandleUpgarde 裡，這裡補上同樣的處理。
        private static void ApplyMaxRewards(Mortal.Core.UpgradePanel panel, Mortal.Core.UpgradeItemData data)
        {
            var mods = data.ItemModify;
            if (mods == null) return;
            var message = GetField<Mortal.Core.GameMessagePanel>(panel, "_messagePanel");
            foreach (var mod in mods)
            {
                if (mod == null) continue;
                string key = "Story/M_Add_Misc_" + mod.Id;
                string text = LocalizationManager.Instance != null
                    ? LocalizationManager.Instance.LocaleResolver.GetString(key)
                    : key;
                message?.DisplayMessage(text);
                mod.ModifyCount();
            }
        }

        private static void ForceInteractable(Mortal.Core.UpgradePanel panel, string buttonField, string menuField, bool enable)
        {
            var button = GetField<Button>(panel, buttonField);
            if (button != null) button.interactable = enable;

            var menu = GetField<MonoBehaviour>(panel, menuField);
            if (menu == null) return;
            var type = menu.GetType();
            type.GetMethod("SetActive")?.Invoke(menu, new object[] { enable });
            type.GetMethod("Init")?.Invoke(menu, null);
        }

        private static void HideWarnings(Mortal.Core.UpgradePanel panel)
        {
            var text = GetField<Text>(panel, "_nextLevelText");
            if (text != null) text.gameObject.SetActive(false);
        }

        private static T GetField<T>(object obj, string name) where T : class
            => AccessTools.Field(obj.GetType(), name)?.GetValue(obj) as T;

        private static void Invoke(object obj, string name)
            => AccessTools.Method(obj.GetType(), name)?.Invoke(obj, null);
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
