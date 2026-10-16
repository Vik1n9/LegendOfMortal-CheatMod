#!/usr/bin/env bash
# 打包給玩家的發行版：
#   dist/LegendOfMortal_InternalMod_v<版本>_full.zip         BepInEx 5.4.23.5 + MOD，解壓到遊戲資料夾即可
#   dist/LegendOfMortal_InternalMod_v<版本>_plugin_only.zip  只有 MOD，給已裝 BepInEx 5 的玩家
# BepInEx 從官方 GitHub 下載並驗 sha256，不從本機遊戲資料夾複製（避免混入 config/cache/日誌）。
set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
PROJ="$(dirname "$HERE")"
REPO="$(dirname "$PROJ")"
DIST="${DIST:-$REPO/dist}"   # 可用環境變數覆寫輸出位置
CACHE="$DIST/.cache"
DOTNET="${DOTNET:-$HOME/.dotnet/dotnet}"

BEPINEX_ZIP="BepInEx_win_x64_5.4.23.5.zip"
BEPINEX_URL="https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/$BEPINEX_ZIP"
BEPINEX_SHA256="82f9878551030f54657792c0740d9d51a09500eeae1fba21106b0c441e6732c4"

VERSION="$(grep -oP 'BepInPlugin\("[^"]+", "[^"]+", "\K[0-9.]+' "$PROJ/Plugin.cs")"
NAME="LegendOfMortal_InternalMod_v$VERSION"
echo "版本 $VERSION"

# 1. 編譯（遊戲沒開時 post-build 也會順便部署到本機遊戲）
"$DOTNET" build "$PROJ" -c Release -nologo -v q
DLL="$PROJ/bin/Release/net48/LegendOfMortal_InternalMod.dll"

# 2. 取得並驗證 BepInEx
mkdir -p "$CACHE"
if [ ! -f "$CACHE/$BEPINEX_ZIP" ]; then
    curl -fsSL -o "$CACHE/$BEPINEX_ZIP.part" "$BEPINEX_URL"
    mv "$CACHE/$BEPINEX_ZIP.part" "$CACHE/$BEPINEX_ZIP"
fi
echo "$BEPINEX_SHA256  $CACHE/$BEPINEX_ZIP" | sha256sum -c --quiet

# 3. 組目錄：README/LICENSE 轉成 Windows 記事本友善的 CRLF（README 另加 UTF-8 BOM）
STAGE="$(mktemp -d)"
trap 'rm -rf "$STAGE"' EXIT
readme() { printf '\xEF\xBB\xBF'; sed "s/{VERSION}/$VERSION/" "$HERE/README.txt" | sed 's/$/\r/'; }

mkdir -p "$STAGE/full" "$STAGE/plugin"
unzip -q "$CACHE/$BEPINEX_ZIP" -d "$STAGE/full"
for root in "$STAGE/full" "$STAGE/plugin"; do
    mkdir -p "$root/BepInEx/plugins/LegendOfMortal_InternalMod"
    cp "$DLL" "$root/BepInEx/plugins/LegendOfMortal_InternalMod/"
    readme > "$root/README.txt"
    sed 's/$/\r/' "$REPO/LICENSE" > "$root/LICENSE.txt"
done

# 4. 壓縮（檔名全 ASCII，避免 Windows 解壓亂碼）
mkdir -p "$DIST"
rm -f "$DIST/${NAME}_full.zip" "$DIST/${NAME}_plugin_only.zip"
(cd "$STAGE/full" && zip -qrX "$DIST/${NAME}_full.zip" .)
(cd "$STAGE/plugin" && zip -qrX "$DIST/${NAME}_plugin_only.zip" .)

cd "$DIST"
sha256sum "${NAME}_full.zip" "${NAME}_plugin_only.zip" > "${NAME}_SHA256SUMS.txt"
ls -l "${NAME}"_*
cat "${NAME}_SHA256SUMS.txt"
