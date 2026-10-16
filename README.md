# LegendOfMortal-CheatMod

《活俠傳》（Legend of Mortal）的 BepInEx 內置 MOD：骰子控制、劇情選項結果提示、養成事件與支線任務查詢，以及戰鬥、數值、養成相關的作弊功能。

- 適用：Steam 版 1.0.6000.2（崆峒神威）
- 框架：BepInEx 5.4.23.5（Unity Mono x64），Windows 與 Linux / Steam Deck（Proton）皆可
- 全部透過 Harmony 補丁與遊戲內 API 實作，不寫外部記憶體，也不需要另開修改器

進遊戲按 **F1** 打開「唐門密卷」。介面採用原版「狀態」卷軸的紙卷風格，字體借用遊戲內建的思源宋體。

## 下載與安裝

到 [Releases](../../releases) 下載：

| 檔案 | 說明 |
|---|---|
| `LegendOfMortal_InternalMod_v1.0_full.zip` | 含 BepInEx 5.4.23.5，解壓到遊戲資料夾即可 |
| `LegendOfMortal_InternalMod_v1.0_plugin_only.zip` | 只有 MOD，給已安裝 BepInEx 5 的玩家 |

1. Steam 收藏庫對《活俠傳》按右鍵 → 管理 → 瀏覽本機檔案。
2. 把壓縮檔內容全部解壓到該資料夾（`winhttp.dll` 與 `Mortal.exe` 同一層）。
3. **Linux / Steam Deck**：Steam → 內容 → 一般 → 啟動選項填入
   `WINEDLLOVERRIDES="winhttp=n,b" %command%`
4. 從 Steam 啟動，畫面上方出現「內置MOD已載入」即成功。

詳細說明（含解除安裝）見壓縮檔內的 `README.txt`，原檔在 [`LegendOfMortal_InternalMod/package/README.txt`](LegendOfMortal_InternalMod/package/README.txt)。

## 功能

| 分頁 | 功能 |
|---|---|
| 骰子選項 | 指定天命骰子點數；劇情選項後顯示結果（如 +銀兩） |
| 屬性銀兩 | 銀兩、命運、行動點；人物屬性；門派數值 |
| 戰鬥養成 | 單挑／群戰無敵、群戰一擊必殺、技能無冷卻、戰鬥不限時；練功不消耗、快速練功、好感只升不降 |
| 事件任務 | 目前地點可觸發的養成事件（可指定必出）；支線任務與條件（可設為必過） |
| 系統 | 遊戲速度 0.5～5 倍（不干擾暫停與劇情快轉） |

設定檔：`BepInEx/config/LegendOfMortal_InternalMod.cfg`（快捷鍵與各功能預設值）。

## 從原始碼建置

需要 .NET SDK 8（以 `Microsoft.NETFramework.ReferenceAssemblies` 編譯 net48）與一份已安裝的遊戲，編譯時會引用遊戲的 `Mortal_Data/Managed/*.dll`。

```bash
cd LegendOfMortal_InternalMod
dotnet build -c Release
# 遊戲不在 Linux Steam 預設位置時：
dotnet build -c Release -p:GameDir="/path/to/LegendOfMortal"
```

- 產物：`bin/Release/net48/LegendOfMortal_InternalMod.dll`
- 編譯後會自動複製到遊戲的 `BepInEx/plugins/LegendOfMortal_InternalMod/`；偵測到遊戲執行中則跳過並警告。
- `libs/` 內是編譯用的 BepInEx 5 / HarmonyX 組件。

### 打包發行版

```bash
LegendOfMortal_InternalMod/package/package.sh
```

輸出到 `dist/`（不進版控）：full 與 plugin_only 兩個 zip 及 SHA256SUMS。BepInEx 從官方 GitHub release 下載並驗證 sha256，版本號取自 `Plugin.cs` 的 `BepInPlugin`。

## 程式結構

```
LegendOfMortal_InternalMod/
├── Plugin.cs            BepInEx 進入點，套用各組 Harmony 補丁
├── ModRunner.cs         自建常駐物件跑快捷鍵/Tick/IMGUI（遊戲會銷毀 BepInEx 管理物件）
├── Config/              BepInEx 設定項
├── Patches/             DicePatch（骰子）、InfoPatch（選項/事件/任務）、CheatPatches（作弊）
├── Features/            CheatEngine：數值讀寫、行動點、遊戲速度
├── Core/                Lua 劇本解析、事件快取、資料輔助
├── UI/                  GameStyleUI（紙卷風格 GUISkin）、CheatWindow（主視窗）、ClickBlocker（擋點穿）
└── package/             發行包腳本與玩家說明
```

## 第三方元件

- [BepInEx](https://github.com/BepInEx/BepInEx)（LGPL-2.1）
- [HarmonyX](https://github.com/BepInEx/HarmonyX)（MIT）

本專案與遊戲開發商無關。修改數值會寫入存檔，使用前請先備份。
