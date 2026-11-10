# guide-verify

不需要遊戲 DLL 的驗證專案：用 `Stubs.cs` 內的型別替身編譯 `Core` 與 `UI/GuideMapView.cs`，
檢查指南地圖資料（Id 不重複、去向存在、連線向下且不穿過其他節點、節點皆可達、結局 66 個）。

本機抽出 `tools/game-extract/out/` 的遊戲資料後（該目錄不提交），會多做一層「攻略 vs 遊戲本體」比對：

- 攻略用到的旗標是否都存在於遊戲旗標表；`FlagIs(id, 值)` 的值是否在遊戲列出的值域內。
- 66 個結局的編號、名稱是否與遊戲語系字串表相同。
- 標為「目前無法達成」的結局，是否其實有劇本會開啟；標為可達成的，是否沒有任何劇本開啟。
- 地圖節點時間是否對得上任務檢查點；遊戲任務有多少比例已收進「本旬事件」。
- 煙霧測試：直接餵旗標與日期給 `PlayerRoutes` / `FindCurrent`，確認目前節點正確；
  並確認早期結局過了期限會變成「已錯過」。

```bash
dotnet run --project tools/guide-verify
```

資料目錄預設找 repo 內的 `tools/game-extract/out`，也可用環境變數指定：

```bash
LOM_GAMEDATA=/path/to/out dotnet run --project tools/guide-verify
```

輸出 `ALL OK` 即通過；`WARN` 是不影響建置、但需要人工確認的項目（例如 wiki 與遊戲腳本不一致）。
找不到遊戲資料時只跑第一層檢查。修改 `GuideMapData.cs` / `GuideEndings.cs` / `EventGuideData.cs` 後請先跑一次。
