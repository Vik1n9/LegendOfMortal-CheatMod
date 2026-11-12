# text-search

用一句話描述劇情，在《活俠傳》劇本對白裡找出是哪一段、屬於哪個任務檢查點。整理攻略、結緣線條件時，用來代替手動翻劇本。

| 步驟 | 做什麼 | 在哪裡跑 |
|---|---|---|
| `build_story.py` | 從遊戲檔抽出每個 Lua 劇本的繁中文本（見下表） | 本機 |
| `search.py` 第 1 段 | 對白切成 24 行一段，用中文兩字詞（BM25）挑出前 N 段候選 | 本機 |
| `search.py` 第 2 段 | 每段候選各問 TypeSafe 一題 Score：「這段對白是不是描述中的那一幕」，再依結果排序 | TypeSafe API |
| `search.py` 第二輪 | 第一輪判為相關的劇本（「部分吻合＋就是這一幕」≥ 0.3，最多 `--expand` 個），其餘段落全部再評一次 | TypeSafe API |

第二輪是用來補本機候選的盲點。本機只比對字面，同一個劇本裡換了說法的段落會漏掉。
例如查「師父說要幫趙活找媳婦」時，本機只選到 `talk_girl5_02` 後段的回想台詞，真正那一幕在開頭，原文用的是「捉個未出閣的年輕小娘子回來，給你作新娘子」。
第二輪展開這個劇本後，開頭那段就以「就是這一幕」機率 0.97 排到第一名。

## 收錄的文本

每個劇本依出場順序收錄以下幾類，每行開頭的標籤就是下表的「標示」：

| 標示 | 來源 | 說明 |
|---|---|---|
| 說話者名稱／旁白／字幕／某某（心想） | `say(…GetStoryText("D_…"/"N_…"/"T_…"))` | 對白 |
| 選項 | `option…[n] = "O_…"` | 有條件的選項（`X and "O_…" or "~O_…"`）標「需條件」；條件名稱不存在（`XXX`、`…_TEMP`）的標「停用」 |
| 擲骰 | `dice…[n] = "O_…|>=66"`＋`checkpointmanager.Dice(…)` | 前面加上擲骰標題（`DiceHeader/`），每個選項附點數條件，例：`閣下便是：南宮深（<33） ／ 唐布衣（<66） ／ 青樓閃電俠（>=66）` |
| 風雲史 | `statmodifymanager.AddStory(…)`→`LegendInfo/` | 遊戲內「風雲史」的劇情摘要。少數沒有劇本寫入的，依鍵名對回劇本；對不到的放在「風雲史（無對應劇本）」 |

約 200 個選項沒有字串鍵，原文直接以簡中寫在劇本裡，抽取時用系統的 `opencc -c s2tw` 轉成繁體。

## 用法

```bash
PY=tools/game-extract/.venv/bin/python
GAME="$HOME/.local/share/Steam/steamapps/common/LegendOfMortal"

$PY -m pip install typesafe-sdk           # 第一次
$PY tools/text-search/build_story.py "$GAME"

$PY tools/text-search/search.py "師父說要幫趙活找媳婦" --dry-run   # 只看本機候選與預估用量
$PY tools/text-search/search.py "師父說要幫趙活找媳婦" -n 12 --show 2
```

如果要顯示任務檢查點和時間，需要先跑過 `tools/game-extract`，產生 `tools/game-extract/out/missions.json`。

不加 `--dry-run` 時需要 `TYPESAFE_API_KEY`，可以放在 `tools/text-search/.env`：

```
TYPESAFE_API_KEY=...
```

## 判讀

每段候選會得到四個層級的機率：0 無關、1 只是提到、2 部分吻合、3 就是這一幕。

- `P(吻合)`：層級 3 的機率，結果依這一欄排序。
- 判定：`P(吻合) ≥ 0.6` 標成「吻合」；層級 2 加 3 的機率 ≥ 0.6 標成「相關段落」；信心 < 0.3 標成「不確定」。
  這些門檻只是起點，要拿實際查詢的結果來調整。
- 問題的措辭與層級定義集中在 `search.py` 開頭的 `INSTRUCTIONS`、`CRITERIA`。
- 本機候選如果沒有包含正確的那一段，TypeSafe 也挑不出來。
  遇到這種情況，可以用 `-n` 加大候選數，或改用對白裡實際會出現的字詞來描述。

## 注意

- `out/story.json` 是遊戲原文，`.cache/` 存的是 API 回應，`.env` 是金鑰，三者都在 `.gitignore` 裡，不可以提交。
- 查詢時會把候選對白送到 TypeSafe 的伺服器（官方表示不會拿來訓練）。
  實測一次查詢（第一輪 12 段＋第二輪 16 段）約 2.8 萬輸入 token，以 `$0.042`／百萬 token 計算約 0.0012 美元。
  第二輪最多送 40 段（`EXPAND_MAX`）；加上 `--expand 0` 就不做第二輪。
- 相同的查詢與候選會直接讀 `.cache/`，不會重複計費。
