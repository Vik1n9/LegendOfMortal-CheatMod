# game-extract

從本機安裝的《活俠傳》抽出遊戲內「事件判定」，供 `tools/guide-verify` 比對、以及整理攻略時對照。

遊戲把判定資料放在 Unity 資產裡，這個工具用 [UnityPy](https://github.com/K0lb3/UnityPy) 讀出來：

| 來源 | 內容 |
|---|---|
| `Mortal_Data/globalgamemanagers.assets` | `MissionData`（任務）、`MissionCheckData`（時間／條件／旗標）、`FlagData`（旗標值域）、`LibraryItemData`（汗青書編號） |
| `Mortal_Data/level0` | 語系字串表（`Library/Title/200xx` 等中文結局名稱） |
| `Mortal_Data/sharedassets*.assets` | `SwitchResultData`（劇情檢查點 `checkpointmanager.Switch` 的分支條件） |
| `Mortal_Data/resources.assets` | Lua 劇本（`endgamepanel.Open`、`SetFlag`／`AddFlag`、劇本互指） |
| `Mortal_Data/Managed/*.dll` | 只掃字串常數，找出由遊戲程式直接寫入的旗標（例：戰鬥結果 `C00000`） |

## 用法

```bash
python3 -m venv .venv
.venv/bin/pip install UnityPy TypeTreeGeneratorAPI

.venv/bin/python tools/game-extract/extract.py \
  "/home/<user>/.local/share/Steam/steamapps/common/LegendOfMortal" \
  -o tools/game-extract/out
```

預設輸出到本目錄的 `out/`。`out/` 只留驗證需要的最低資料（事件／結局名稱、旗標編號與值、劇本名稱），不含劇本內文、旗標備註文字、結局說明；目前仍列入 .gitignore，不提交。沒有遊戲的人仍可跑 `tools/guide-verify`，只是會略過「攻略 vs 遊戲本體」那一層。

## 產出

| 檔案 | 內容 |
|---|---|
| `out/missions.json` | 211 個任務、300 個檢查點：檢查點編號、時間（特定／區間）、起始劇本名稱 |
| `out/flags.json` | 1068 個旗標的預設值，與開發備註列出的值（只留數字，例：備註 `留學 1:青城 2:崆峒` → `[1, 2]`） |
| `out/endings.json` | 66 個汗青書的編號與中文名稱 |
| `out/scripts.json` | 約 1900 個劇本名稱：開啟的結局、寫入的旗標值、用 `AddFlag` 累加的旗標、互相引用的劇本、結局開啟點的旗標條件（其餘 Lua 條件只記「劇本條件」） |
| `out/switches.json` | 約 3500 個劇情檢查點：每個分支的比較式，只保留旗標名稱與常數，其他數值（屬性、好感、心上人等）只記名稱 |

## 判定規則（從 DLL decompile 確認）

- 時間：`_timeCheckType` = 特定（比對 `_specificTime`）／區間（`_minTime` ≦ 現在 ≦ `_maxTime`）／週期（不檢查時間）。
  `GameTime.ConvertToRounds() = (年-1)*36 + (月-1)*3 + 旬(1~3)`，與 MOD 的 `EventGuide.Round` 一致。
- 條件：`MissionCheckData.Check()` 對所有 `ConditionResultItem` 取 AND；每個 `ConditionResultItem` 內依 `LogicOperatorType`（AND／OR）合併 `StatCompareItem`。
- 比較：`_compareOp` 0=＝、1=≠、2=<、3=≦、4=>、5=≧。
- 劇情檢查點：`SwitchResultData.Execute()` 依序檢查分支，回傳第一個成立的編號（從 1 起）；全部不成立回傳「分支數＋1」。
- 變數：`StatGroupVariable` 由多個 `StatValueReference` 依 `_groupType`（合計／最大／最小／相乘／全部／任一）組合；
  來源類型 `_checkType`：0 常數、1 角色數值、2 好感度、3 旗標、4 書籍、5 雜物、6 貴重品、7 隨機值、8 數值群組、9 遊戲時間、10 技能、11 秘笈等級、12 已開發項目、13 開發項目等級、14 書籍精通、15 風雲史、16 條件設定。

## 注意

- 這是唯讀工具，不會修改遊戲檔案。
- 遊戲改版後在本機重新跑一次，`tools/guide-verify` 會用新資料重新檢查攻略（`out/` 不提交）。
