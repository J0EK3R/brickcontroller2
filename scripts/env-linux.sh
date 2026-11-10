#!/usr/bin/env bash
# env-linux.sh - environment for building BrickController2 on Linux without root.
# Usage: source scripts/env-linux.sh   (it only exports variables, it never installs anything)
# All locations can be overridden by exporting the variable before sourcing.

export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export JAVA_HOME="${JAVA_HOME:-$HOME/jdk/jdk-21}"
export ANDROID_HOME="${ANDROID_HOME:-$HOME/android-sdk}"
export ANDROID_SDK_ROOT="$ANDROID_HOME"   # legacy name, still read by some tools

# Opt out of telemetry and the first-run banner (keeps CI-like output clean).
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

# Put dotnet, global dotnet tools, the JDK and the Android SDK tools on PATH.
export PATH="$DOTNET_ROOT:$DOTNET_ROOT/tools:$JAVA_HOME/bin:$ANDROID_HOME/cmdline-tools/latest/bin:$ANDROID_HOME/platform-tools:$PATH"
