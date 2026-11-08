# guide-verify

不需要遊戲 DLL 的驗證專案：用 `Stubs.cs` 內的型別替身編譯 `Core` 與 `UI/GuideMapView.cs`，
並檢查指南地圖資料（Id 不重複、去向存在、連線向下且不穿過其他節點、節點皆可達、結局 66 個）。

```bash
dotnet run --project tools/guide-verify
```
輸出 `ALL OK` 即通過。修改 `GuideMapData.cs` / `GuideEndings.cs` 後請先跑一次。
