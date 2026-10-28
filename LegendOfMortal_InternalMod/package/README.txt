活俠傳 內置MOD v{VERSION}
========================================

適用版本：Steam 版《活俠傳》1.0.6001.0（崆峒神威）以上
內建 BepInEx 5.4.23.5（Windows x64）

進遊戲按 F1 打開「唐門密卷」。介面採用遊戲原版的紙卷風格與字體，
所有功能都透過遊戲本身的 API 執行，不寫外部記憶體，也不需要另開修改器。


■ 功能
----------------------------------------
骰子選項
  ・指定天命骰子點數（最終點數仍會加上武學、好感等加成）
  ・在劇情選項後顯示結果（例如 +銀兩）

屬性銀兩
  ・設定銀兩、命運；回滿行動點／無限行動點
  ・設定人物屬性（體力、內力、輕功、魅力、學問、嘴力、道德、修養）
  ・設定門派數值（資產、名聲、人數、向心力、個人貢獻度）

戰鬥養成
  ・單挑／群戰我方無敵、群戰一擊必殺、技能無冷卻、戰鬥不限時
  ・練功不消耗、快速練功、好感只升不降

事件任務
  ・列出目前地點可觸發的養成事件，可指定下次必出
  ・列出支線任務與條件，可設為必定通過

系統
  ・調整遊戲速度（0.5～5 倍，不影響暫停與劇情快轉）


■ 安裝（Windows）
----------------------------------------
1. 在 Steam 收藏庫對《活俠傳》按右鍵 → 管理 → 瀏覽本機檔案，
   會打開含有 Mortal.exe 的遊戲資料夾。
2. 把壓縮檔裡的「所有內容」直接解壓到這個資料夾
   （winhttp.dll 要和 Mortal.exe 放在同一層）。
3. 正常從 Steam 啟動遊戲。畫面上方出現
   「內置MOD已載入　按 F1 打開「唐門密卷」」就代表安裝成功。

如果你已經裝過 BepInEx 5，可以改用 plugin_only 版，
只要把 BepInEx\plugins\LegendOfMortal_InternalMod 資料夾放進去即可。


■ 安裝（Linux / Steam Deck，Proton）
----------------------------------------
照上面的步驟解壓後，還要在 Steam 對《活俠傳》按右鍵 → 內容 → 一般 →
「啟動選項」填入：

    WINEDLLOVERRIDES="winhttp=n,b" %command%

沒有這一行，Proton 不會載入 BepInEx，MOD 也就不會生效。


■ 已安裝其他 BepInEx 版本的玩家
----------------------------------------
本 MOD 需要 BepInEx 5。若遊戲資料夾裡已經有 BepInEx 6，請先刪除
BepInEx 資料夾、winhttp.dll、doorstop_config.ini、.doorstop_version、changelog.txt，
再安裝本 MOD。（若 BepInEx\plugins 裡還有其他 MOD，請先自行備份。）
另外請勿和其他控制骰子的 MOD 同時使用，以免互相衝突。


■ 設定
----------------------------------------
第一次啟動後會產生設定檔：
  BepInEx\config\LegendOfMortal_InternalMod.cfg
可在裡面更改開視窗的快捷鍵（預設 F1）及各功能的預設值。


■ 解除安裝
----------------------------------------
刪除遊戲資料夾裡的 winhttp.dll 即可停用；
要完全移除，再刪除 BepInEx 資料夾、doorstop_config.ini、.doorstop_version、changelog.txt。
Linux / Steam Deck 請一併清空啟動選項。


■ 注意事項
----------------------------------------
・修改數值會直接寫進存檔，使用前建議先備份存檔。
・遊戲更新後，MOD 可能需要跟著更新才能正常運作。
  如果進遊戲後沒有看到載入提示，請查看 BepInEx\LogOutput.log。


■ 第三方元件
----------------------------------------
・BepInEx（LGPL-2.1）：https://github.com/BepInEx/BepInEx
・HarmonyX（MIT）：https://github.com/BepInEx/HarmonyX
