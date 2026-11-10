using System.Collections.Generic;
using Mortal.Core;

namespace LegendOfMortal_InternalMod.Features
{
    /// <summary>
    /// 雜物（藥品、食物等可使用道具）數量修改：直接呼叫 ItemData.SetCount／AddCount，
    /// 背包、商店等介面下次刷新時就會顯示新數量。
    /// 書籍（秘笈）與貴重品不在此列，避免影響武學升級與劇情判定。
    /// </summary>
    public static class ItemCheat
    {
        public static IEnumerable<ItemData> MiscItems()
        {
            var db = Instance;
            var collection = db?.Miscs;
            if (collection?.List == null) yield break;

            foreach (var item in collection.List)
                if (item != null && !IsDevItem(item)) yield return item;
        }

        /// <summary>開發用的「測試雜物」等道具，正常遊玩拿不到，不列出以免誤改。</summary>
        private static bool IsDevItem(ItemData item)
            => Name(item).Contains("測試")
               || (item.Id ?? string.Empty).IndexOf("test", System.StringComparison.OrdinalIgnoreCase) >= 0;

        public static ItemDatabase Instance
        {
            get
            {
                try { return ItemDatabase.Instance; }
                catch { return null; }
            }
        }

        /// <summary>道具顯示名稱；語系查不到時退回編號。</summary>
        public static string Name(ItemData item)
        {
            try
            {
                string key = item.GetNameKey();
                string text = LocalizationManager.Instance?.LocaleResolver.GetString(key);
                if (!string.IsNullOrEmpty(text) && text != key) return text;
            }
            catch { }
            return item.Id;
        }

        public static void Set(ItemData item, int count)
        {
            try { item?.SetCount(UnityEngine.Mathf.Max(0, count)); }
            catch (System.Exception ex) { Plugin.LogError("道具設定失敗: " + ex.Message); }
        }

        public static void Add(ItemData item, int delta)
        {
            try { item?.AddCount(delta); }
            catch (System.Exception ex) { Plugin.LogError("道具增加失敗: " + ex.Message); }
        }
    }
}
