# names：場所事件與支線任務的中文名稱

產生 `LegendOfMortal_InternalMod/Core/TrainingNames.cs` 與 `Core/MissionNames.cs`，讓「事件任務」頁不再顯示內部代碼。
名稱以 [活俠傳 wiki](https://github.com/Mr-Smilin/LoM-wiki) 為準，wiki 沒收錄的才用遊戲開發備註；輸出一律經 `opencc -c s2tw` 轉成繁體。

所有中間檔放在同一個工作目錄 `$W`（不提交）：

```bash
W=/tmp/lom-names; mkdir -p $W
git clone --depth 1 https://github.com/Mr-Smilin/LoM-wiki.git $W/wiki/repo
PY=tools/game-extract/.venv/bin/python; GAME="$HOME/.local/share/Steam/steamapps/common/LegendOfMortal"

$PY tools/game-extract/names/positions.py "$GAME" $W/pos.json              # 場所與事件（sharedassets2）
$PY tools/game-extract/names/lua_strings.py "$GAME" $W/lua.json $W/loc.json  # Lua 劇本、繁中字串
python3 tools/game-extract/names/match_training.py $W $W/wiki/repo/docs/system/training/index.md
python3 tools/game-extract/names/gen_training.py $W LegendOfMortal_InternalMod/Core/TrainingNames.cs

# 支線任務另需完整任務說明與旗標備註（舊版 extract.py 的 missions.json、flags.json）
python3 tools/game-extract/names/match_missions.py $W   # 讀 $W/missions_full.json、$W/flags_full.json
```

比對規則：

- 場所事件：wiki〈養成指令〉表的「指令」＋「機率權重」＋「心相」（地點劇本與結果劇本的心相相加）都吻合才採用；
  事件有開發備註時，wiki 名稱需與備註有共同兩字詞，避免數值巧合配錯。
- 支線任務：只處理遊戲沒有任務說明者；以 wiki 各事件「事件時間」與劇本寫入的旗標比對，分數領先 ≥3 或該旬只有一個事件才採用。
