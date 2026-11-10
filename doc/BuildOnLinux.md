# Building BrickController2 on Linux (no root)

Verified on Linux x86_64, 16 GB RAM, 4 CPUs, 2026-11-10. Everything is installed in `$HOME`.

## Quick start
```bash
scripts/setup-linux.sh          # one-time, idempotent (about 5 min first run, 3 s afterwards)
scripts/build-linux.sh          # build + unit tests + Android APK (about 2.5 min)
scripts/build-linux.sh --skip-android
source scripts/env-linux.sh     # use the toolchain in your own shell
```
Prerequisites (must exist already): `curl`, `unzip`, `tar`, 10 GB free disk, internet access to
`dot.net`, `api.adoptium.net`, `dl.google.com`, `nuget.org`. Env vars `DOTNET_ROOT`, `JDK_DIR`,
`ANDROID_HOME`, `CMDLINE_TOOLS_REV`, `BUILD_TOOLS`, `ANDROID_PLATFORM`, `CONFIG` override the defaults.

## Installed tools
| Tool | Version | Location |
|---|---|---|
| .NET SDK | 10.0.115 (`global.json`: 10.0.100, rollForward latestFeature) | `~/.dotnet` |
| Workload `maui-android` | manifest 10.0.110/10.0.100 | `~/.dotnet` |
| JDK (Temurin) | 21.0.13 | `~/jdk/jdk-21` |
| Android cmdline-tools | revision 11076708 | `~/android-sdk/cmdline-tools/latest` |
| Android platform / build-tools / platform-tools | android-36 / 36.0.0 / 37.0.1 | `~/android-sdk` |

## Projects
| Project | Status |
|---|---|
| BrickController2 (core) | built, 0 warnings |
| BrickController2.Tools | built, 0 warnings |
| BrickController2.Tests | built, 0 warnings; 734 passed, 0 failed, 0 skipped |
| BrickController2.Android | built via `dotnet publish` (CI switches), no warnings reported |
| BrickController2.iOS | skipped: needs macOS + Xcode |
| BrickController2.WinUI | skipped: Windows-only target framework |

Never build `BrickController2.slnx` as a whole on Linux (it contains iOS and WinUI).
The APK is created at
`BrickController2/BrickController2.Android/bin/Release/net10.0-android/com.scn.BrickController2-Signed.apk`
(about 45 MB, contains `AndroidManifest.xml` and `classes.dex`). `-p:AndroidKeyStore=false` is required in
Release, otherwise a `keystore.jks` is expected. This file (`doc/*.md`) is excluded from the embedded help resources.

## Troubleshooting
- Out of memory during the Android build: use `-m:1`, run `dotnet build-server shutdown`, or build Debug.
- Missing ICU: `export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` (not needed on the verified machine).
- SDK/JDK not found by MSBuild: pass `-p:AndroidSdkDirectory=$ANDROID_HOME -p:JavaSdkDirectory=$JAVA_HOME`.
- Licenses: rerun `setup-linux.sh`, or `yes | sdkmanager --sdk_root=$ANDROID_HOME --licenses`.
- Outdated cmdline-tools URL: set `CMDLINE_TOOLS_REV` to a current revision.
- NuGet restore failures: check access to `api.nuget.org`.
