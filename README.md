# LegendOfMortal-CheatMod

《活俠傳》（Legend of Mortal）內置 MOD，適用 Steam 版 **1.0.6001.0（崆峒神威）** 以上（1.0.6000.x 請使用 MOD v1.0），支援 Windows 與 Linux / Steam Deck。

## 安裝

1. 到 [Releases](../../releases/latest) 下載 `LegendOfMortal_InternalMod_v1.1_full.zip`。
   （已安裝 BepInEx 5 的玩家可改下載 `plugin_only` 版。）
2. Steam 收藏庫對《活俠傳》按右鍵 → 管理 → 瀏覽本機檔案。
3. 把壓縮檔內容全部解壓到該資料夾（`winhttp.dll` 要和 `Mortal.exe` 在同一層）。
4. **Linux / Steam Deck** 另需在 Steam → 內容 → 一般 → 啟動選項填入：
   ```
   WINEDLLOVERRIDES="winhttp=n,b" %command%
   ```
5. 從 Steam 啟動遊戲，畫面上方出現「內置MOD已載入」即安裝成功。

若遊戲資料夾已有 BepInEx 6，請先刪除 `BepInEx` 資料夾、`winhttp.dll`、`doorstop_config.ini`、`.doorstop_version`、`changelog.txt` 再安裝。

**解除安裝**：刪除 `winhttp.dll` 即停用；完全移除再刪除上述 BepInEx 相關檔案。

## 使用

- 遊戲中按 **F1** 開關「唐門密卷」視窗，拖曳標題列可移動。
- 數值類功能請先打開對應的遊戲介面（人物選單、門派介面）再設定。
- 快捷鍵與各功能預設值可在 `BepInEx/config/LegendOfMortal_InternalMod.cfg` 修改。
- 修改的數值會寫入存檔，使用前建議先備份存檔。

## 功能

| 分頁 | 功能 |
|---|---|
| 骰子選項 | 指定天命骰子點數；在劇情選項後顯示結果（如 +銀兩） |
| 屬性銀兩 | 設定銀兩、命運；回滿／無限行動點；人物屬性；門派數值 |
| 戰鬥養成 | 單挑／群戰我方無敵、群戰一擊必殺、技能無冷卻、戰鬥不限時；練功不消耗、快速練功、好感只升不降 |
| 事件任務 | 列出目前地點可觸發的養成事件並可指定必出；列出支線任務與條件並可設為必定通過 |
| 系統 | 遊戲速度 0.5～5 倍 |
