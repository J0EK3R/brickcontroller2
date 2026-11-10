#!/usr/bin/env bash
# build-linux.sh - builds and tests everything in BrickController2 that is buildable on Linux.
#
# Usage: scripts/build-linux.sh [--skip-android]
# Env:   CONFIG (default Release)
# Skipped on purpose: BrickController2.iOS (needs macOS/Xcode), BrickController2.WinUI
# (Windows only). The .slnx is never built as a whole because it contains those projects.
# Exits non-zero on the first failure.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CONFIG="${CONFIG:-Release}"
SKIP_ANDROID=0
[ "${1:-}" = "--skip-android" ] && SKIP_ANDROID=1

# shellcheck source=env-linux.sh
source "$REPO_ROOT/scripts/env-linux.sh"
cd "$REPO_ROOT"
P="BrickController2"

echo "=== Core library";  dotnet build "$P/BrickController2/BrickController2.csproj" -c "$CONFIG"
echo "=== Tools";         dotnet build "$P/BrickController2.Tools/BrickController2.Tools.csproj" -c "$CONFIG"
echo "=== Tests (build)"; dotnet build "$P/BrickController2.Tests/BrickController2.Tests.csproj" -c "$CONFIG"
echo "=== Tests (run)"
dotnet test "$P/BrickController2.Tests/BrickController2.Tests.csproj" -c "$CONFIG" --no-build \
  --logger "trx;LogFileName=test-results.trx" --results-directory TestResults

if [ "$SKIP_ANDROID" -eq 0 ]; then
  # Same switches as CI: unsigned-keystore-free APK, so no keystore.jks is needed in Release.
  echo "=== Android"
  dotnet publish "$P/BrickController2.Android/BrickController2.Android.csproj" \
    -c "$CONFIG" -f net10.0-android -p:AndroidKeyStore=false -p:AndroidPackageFormat=apk \
    -p:AndroidSdkDirectory="$ANDROID_HOME" -p:JavaSdkDirectory="$JAVA_HOME"
  echo "APK(s):"; find "$P/BrickController2.Android/bin/$CONFIG" -name '*.apk'
fi
echo "=== Build finished successfully"
