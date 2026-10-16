using System;
using Mortal.Core;
using UnityEngine;
using Cfg = LegendOfMortal_InternalMod.Config.ModConfig;

namespace LegendOfMortal_InternalMod.Features
{
    /// <summary>
    /// 內置安全作弊（不含骰子；骰子由 DicePatch 提供）。
    /// 全部走遊戲內 API，不做外部記憶體寫入。
    /// 戰鬥/數值/行動/速度等。
    /// </summary>
    public static class CheatEngine
    {
        public static bool GodCombat => Cfg.GodCombat.Value;
        public static bool GodBattle => Cfg.GodBattle.Value;
        public static bool InfAction => Cfg.InfAction.Value;

        public static void SetStat(GameStatType type, int value)
        {
            try
            {
                var stats = PlayerStatManagerData.Instance?.Stats;
                var g = stats?.Get(type);
                if (g == null) { Plugin.LogError($"找不到數值 {type}"); return; }
                if (value > g.Max) g.SetMax(value);
                g.SetValue(value);
            }
            catch (Exception ex) { Plugin.LogError("SetStat: " + ex.Message); }
        }

        public static int GetStat(GameStatType type)
        {
            try { return PlayerStatManagerData.Instance.Stats.Get(type).FinalValue; }
            catch { return 0; }
        }

        public static void AddActionPoints()
        {
            try
            {
                var mgr = PlayerStatManagerData.Instance;
                if (mgr == null) return;
                mgr.Stats.Set(GameStatType.行動次數, mgr.GetMaxActionCount());
            }
            catch { }
        }

        // 遊戲自己會設 timeScale：暫停=0、劇情快轉=10、單挑倍速=_currentSpeedTime。
        // 只在遊戲處於正常速度（1）時套用倍率，其餘狀態一律不碰，否則暫停會失效。
        private static float _appliedSpeed = 1f;

        public static void ApplyGameSpeed()
        {
            try
            {
                float s = Mathf.Clamp(Cfg.GameSpeed.Value, 0.5f, 5f);
                float cur = Time.timeScale;
                if (Mathf.Abs(s - 1f) < 0.01f)
                {
                    // 關閉變速：把自己設過的值還原成 1
                    if (Mathf.Abs(_appliedSpeed - 1f) > 0.01f && Mathf.Abs(cur - _appliedSpeed) < 0.01f) Time.timeScale = 1f;
                    _appliedSpeed = 1f;
                    return;
                }
                bool gameNormal = Mathf.Abs(cur - 1f) < 0.01f;
                bool oursStale = Mathf.Abs(cur - _appliedSpeed) < 0.01f && Mathf.Abs(cur - s) > 0.01f;
                if (gameNormal || oursStale)
                {
                    Time.timeScale = s;
                    _appliedSpeed = s;
                }
            }
            catch { }
        }

        public static void ResetGameSpeed() { try { Time.timeScale = 1f; _appliedSpeed = 1f; } catch { } }

        public static void Tick()
        {
            if (!Cfg.EnableCheats.Value) return;
            if (InfAction) AddActionPoints();
            ApplyGameSpeed();
        }
    }
}
