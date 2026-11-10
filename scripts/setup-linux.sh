#!/usr/bin/env bash
# setup-linux.sh - idempotent, root-less installer of the BrickController2 build toolchain.
#
# Installs into $HOME: .NET SDK (version from global.json), JDK 21 (Temurin),
# Android command line tools + platform/build-tools, and the maui-android workload.
# Every step is skipped when its result already exists, so re-running is cheap.
#
# Overridable environment variables: DOTNET_ROOT, JDK_DIR, ANDROID_HOME, JDK_MAJOR,
# DOTNET_CHANNEL, CMDLINE_TOOLS_REV, ANDROID_PLATFORM, BUILD_TOOLS.
set -euo pipefail

# ---------------------------------------------------------------- configuration
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
JDK_MAJOR="${JDK_MAJOR:-21}"
JDK_DIR="${JDK_DIR:-$HOME/jdk/jdk-$JDK_MAJOR}"
ANDROID_HOME="${ANDROID_HOME:-$HOME/android-sdk}"
DOTNET_CHANNEL="${DOTNET_CHANNEL:-10.0.1xx}"          # feature band allowed by global.json rollForward
CMDLINE_TOOLS_REV="${CMDLINE_TOOLS_REV:-11076708}"    # pinned; bump if the URL rots
ANDROID_PLATFORM="${ANDROID_PLATFORM:-36}"
BUILD_TOOLS="${BUILD_TOOLS:-36.0.0}"
MIN_FREE_GB=10

log() { printf '\n=== %s\n' "$*"; }

# Fresh temp dir for all downloads; removed on exit. Nothing is executed from it
# except the (official) dotnet-install.sh, which is run with the repo as cwd.
TMP_DIR="$(mktemp -d)"
trap 'rm -rf "$TMP_DIR"' EXIT

# ---------------------------------------------------------------- preflight
preflight() {
  log "Preflight"
  local tool
  for tool in curl unzip tar; do
    command -v "$tool" >/dev/null || { echo "Missing required tool: $tool" >&2; exit 1; }
  done
  case "$(uname -m)" in
    x86_64)  JDK_ARCH=x64;     ;;
    aarch64) JDK_ARCH=aarch64; ;;
    *) echo "Unsupported architecture: $(uname -m)" >&2; exit 1 ;;
  esac
  local free_gb
  free_gb=$(df -Pk "$HOME" | awk 'NR==2 {print int($4/1024/1024)}')
  echo "Free disk in \$HOME: ${free_gb} GB"
  if [ "$free_gb" -lt "$MIN_FREE_GB" ]; then
    echo "Need at least ${MIN_FREE_GB} GB free." >&2; exit 1
  fi
}

# ---------------------------------------------------------------- .NET SDK
install_dotnet() {
  log ".NET SDK"
  # Required version from global.json, e.g. 10.0.100 -> feature band prefix "10.0.1".
  local want band
  want=$(grep -oE '"version"[[:space:]]*:[[:space:]]*"[^"]+"' "$REPO_ROOT/global.json" | sed -E 's/.*"([^"]+)"$/\1/')
  band="$(echo "$want" | cut -d. -f1,2).1"
  if [ -x "$DOTNET_ROOT/dotnet" ] && "$DOTNET_ROOT/dotnet" --list-sdks | grep -q "^${band}"; then
    echo "SDK ${band}xx already installed."
  else
    curl -fsSL https://dot.net/v1/dotnet-install.sh -o "$TMP_DIR/dotnet-install.sh"
    bash "$TMP_DIR/dotnet-install.sh" --channel "$DOTNET_CHANNEL" --install-dir "$DOTNET_ROOT" \
      || bash "$TMP_DIR/dotnet-install.sh" --version "$want" --install-dir "$DOTNET_ROOT"
  fi
}

# ---------------------------------------------------------------- JDK
install_jdk() {
  log "JDK $JDK_MAJOR (Temurin)"
  if [ -x "$JDK_DIR/bin/java" ]; then
    echo "JDK already present in $JDK_DIR."
    return
  fi
  curl -fsSL -o "$TMP_DIR/jdk.tar.gz" \
    "https://api.adoptium.net/v3/binary/latest/${JDK_MAJOR}/ga/linux/${JDK_ARCH}/jdk/hotspot/normal/eclipse"
  mkdir -p "$JDK_DIR"
  tar -xzf "$TMP_DIR/jdk.tar.gz" -C "$JDK_DIR" --strip-components=1
}

# ---------------------------------------------------------------- Android SDK
install_android_sdk() {
  log "Android SDK"
  local sdkmanager="$ANDROID_HOME/cmdline-tools/latest/bin/sdkmanager"
  if [ ! -x "$sdkmanager" ]; then
    curl -fsSL -o "$TMP_DIR/cmdline-tools.zip" \
      "https://dl.google.com/android/repository/commandlinetools-linux-${CMDLINE_TOOLS_REV}_latest.zip"
    unzip -q "$TMP_DIR/cmdline-tools.zip" -d "$TMP_DIR/ct"
    mkdir -p "$ANDROID_HOME/cmdline-tools"
    mv "$TMP_DIR/ct/cmdline-tools" "$ANDROID_HOME/cmdline-tools/latest"
  fi
  export JAVA_HOME="$JDK_DIR"
  # 'yes' gets SIGPIPE when sdkmanager exits; tolerate it despite pipefail.
  yes | "$sdkmanager" --sdk_root="$ANDROID_HOME" --licenses >/dev/null || true
  "$sdkmanager" --sdk_root="$ANDROID_HOME" --install \
    "platform-tools" "platforms;android-${ANDROID_PLATFORM}" "build-tools;${BUILD_TOOLS}"
}

# ---------------------------------------------------------------- workload
install_workload() {
  log "maui-android workload"
  export DOTNET_ROOT PATH="$DOTNET_ROOT:$PATH" DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
  # Run from the repo root so global.json selects the right SDK.
  cd "$REPO_ROOT"
  if dotnet workload list 2>/dev/null | grep -q '^maui-android'; then
    echo "Workload already installed."
  else
    dotnet workload install maui-android
  fi
}

# ---------------------------------------------------------------- summary
summary() {
  log "Installed versions"
  cd "$REPO_ROOT"
  dotnet --version
  dotnet workload list || true
  "$JDK_DIR/bin/java" -version 2>&1 | head -1
  "$ANDROID_HOME/cmdline-tools/latest/bin/sdkmanager" --sdk_root="$ANDROID_HOME" --list_installed 2>/dev/null || true
  echo; echo "Done. Next: source scripts/env-linux.sh && scripts/build-linux.sh"
}

preflight
install_dotnet
install_jdk
install_android_sdk
install_workload
summary
