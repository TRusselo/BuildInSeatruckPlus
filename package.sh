#!/usr/bin/env bash
# Builds SeatruckJukebox and assembles release archives into ./dist
#   - SeatruckJukebox-<ver>.zip              (manual install: drop into BepInEx/plugins)
#   - SeatruckJukebox-<ver>-thunderstore.zip (Thunderstore / r2modman upload)
set -euo pipefail

cd "$(dirname "$0")"

VERSION="$(grep -oE '"version_number"[^"]*"[^"]*"' manifest.json | grep -oE '[0-9]+\.[0-9]+\.[0-9]+')"
NAME="SeatruckJukebox"
DLL="bin/Release/${NAME}.dll"

echo ">> Building ${NAME} ${VERSION} (Release)"
dotnet build "${NAME}.csproj" -c Release

if [[ ! -f "$DLL" ]]; then
  echo "ERROR: $DLL not found after build" >&2
  exit 1
fi

rm -rf dist build-tmp
mkdir -p dist

# --- Manual-install zip:  SeatruckJukebox/SeatruckJukebox.dll ---
mkdir -p "build-tmp/manual/${NAME}"
cp "$DLL" "build-tmp/manual/${NAME}/"
cp README.md LICENSE CHANGELOG.md "build-tmp/manual/${NAME}/"
( cd build-tmp/manual && zip -r -q "../../dist/${NAME}-${VERSION}.zip" "${NAME}" )

# --- Thunderstore zip:  manifest.json + icon.png + README.md at root, dll under plugins/ ---
mkdir -p "build-tmp/ts/plugins/${NAME}"
cp "$DLL" "build-tmp/ts/plugins/${NAME}/"
cp manifest.json README.md LICENSE icon.png "build-tmp/ts/"
( cd build-tmp/ts && zip -r -q "../../dist/${NAME}-${VERSION}-thunderstore.zip" . )

rm -rf build-tmp
echo ">> Done:"
ls -la dist
