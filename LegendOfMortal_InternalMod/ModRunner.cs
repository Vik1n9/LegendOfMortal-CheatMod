using System;
using LegendOfMortal_InternalMod.Config;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LegendOfMortal_InternalMod
{
    /// <summary>
    /// 每幀邏輯（快捷鍵／作弊 Tick／IMGUI）獨立放在自建的隱藏常駐物件上，
    /// 不依賴 BepInEx_Manager：實機發現外掛載入成功但 Update/OnGUI 完全沒跑。
    /// 每次切場景都會檢查，被銷毀就重建。
    /// </summary>
    public class ModRunner : MonoBehaviour
    {
        private static ModRunner _instance;
        private static bool _hooked;
        private bool _loggedUpdate, _loggedGui, _loggedInputErr;
        private static bool _useNewInput;

        public static void Ensure(string reason)
        {
            if (!_hooked)
            {
                _hooked = true;
                SceneManager.sceneLoaded += (s, m) => Ensure("場景載入 " + s.name);
            }
            if (_instance != null) return;
            var go = new GameObject("LoM_InternalMod_Runner") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ModRunner>();
            Plugin.LogInfo($"Runner 已建立（{reason}）");
        }

        private void Update()
        {
            if (!_loggedUpdate) { _loggedUpdate = true; Plugin.LogInfo("Runner Update 開始運作"); }
            if (KeyDown(ModConfig.MainMenuKey.Value))
            {
                UI.CheatWindow.Visible = !UI.CheatWindow.Visible;
                Plugin.LogInfo($"主視窗 {(UI.CheatWindow.Visible ? "開啟" : "關閉")}");
            }

            UI.ClickBlocker.Sync(UI.CheatWindow.Visible, UI.CheatWindow.WindowRect);
            Features.CheatEngine.Tick();
        }

        private void OnGUI()
        {
            if (!_loggedGui) { _loggedGui = true; Plugin.LogInfo("Runner OnGUI 開始運作"); }
            UI.GameStyleUI.Apply();
            UI.CheatWindow.Draw(Plugin.Instance);
            // 啟動後 60 秒內提示 MOD 已載入（確認注入成功），之後不再常駐擋畫面；
            // 放上方正中，避開左上角的貢獻度/行動 HUD 與右上角選單
            if (!UI.CheatWindow.Visible && Time.realtimeSinceStartup < 60f)
            {
                var text = new GUIContent($"內置MOD已載入　按 <b>{ModConfig.MainMenuKey.Value}</b> 打開「唐門密卷」");
                var size = UI.GameStyleUI.Notice.CalcSize(text);
                GUI.Label(new Rect((Screen.width - size.x) / 2f, UI.GameStyleUI.Px(16), size.x, size.y), text, UI.GameStyleUI.Notice);
            }
        }

        private void OnDestroy()
        {
            Plugin.LogInfo("Runner 被銷毀，下次切場景重建");
            if (_instance == this) _instance = null;
        }

        // 遊戲用 New Input System；若舊版 Input 被停用就改走 Keyboard.current
        private bool KeyDown(KeyCode key)
        {
            if (!_useNewInput)
            {
                try { return Input.GetKeyDown(key); }
                catch (InvalidOperationException)
                {
                    _useNewInput = true;
                    if (!_loggedInputErr) { _loggedInputErr = true; Plugin.LogInfo("舊版 Input 已停用，改用 New Input System"); }
                }
            }
            return NewInputKeyDown(key);
        }

        private static bool NewInputKeyDown(KeyCode key)
        {
            try
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb == null) return false;
                string name = key.ToString()
                    .Replace("Control", "Ctrl").Replace("Keypad", "Numpad").Replace("Return", "Enter");
                if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9) name = "Digit" + (key - KeyCode.Alpha0);
                if (!Enum.TryParse(name, out UnityEngine.InputSystem.Key k)) return false;
                return kb[k].wasPressedThisFrame;
            }
            catch { return false; }
        }
    }
}
