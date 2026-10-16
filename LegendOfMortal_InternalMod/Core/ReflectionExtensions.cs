using System;
using System.Reflection;
using HarmonyLib;

namespace LegendOfMortal_InternalMod.Core
{
    public static class ReflectionExtensions
    {
        public static T GetFieldValue<T>(this object instance, string fieldname)
        {
            try
            {
                var f = AccessTools.Field(instance.GetType(), fieldname);
                if (f == null)
                {
                    LegendOfMortal_InternalMod.Plugin.LogError($"找不到欄位 {fieldname} @ {instance.GetType().Name}");
                    return default;
                }
                return (T)f.GetValue(instance);
            }
            catch (Exception ex)
            {
                LegendOfMortal_InternalMod.Plugin.LogError($"GetField {fieldname} 失敗: {ex.Message}");
                return default;
            }
        }

        public static void SetPrivateField(this object instance, string fieldname, object value)
        {
            try
            {
                var f = AccessTools.Field(instance.GetType(), fieldname);
                f?.SetValue(instance, value);
            }
            catch (Exception ex)
            {
                LegendOfMortal_InternalMod.Plugin.LogError($"SetField {fieldname} 失敗: {ex.Message}");
            }
        }

        public static T GetPrivateProperty<T>(this object instance, string propertyname)
        {
            try
            {
                return (T)AccessTools.Property(instance.GetType(), propertyname)?.GetValue(instance);
            }
            catch
            {
                return default;
            }
        }
    }
}
