#!/bin/bash
# iOS Xcode 프로젝트를 명령줄에서 만든다(설계서 5장 M2, 10장).
# 사용법: Tools/build-ios.sh [--open]
#   SLOTBATTLE_BUNDLE_ID, SLOTBATTLE_TEAM_ID 환경 변수로 번들 ID와 Apple 팀 ID를 바꿀 수 있다.
#   UNITY_PATH로 Unity 실행 파일 위치를 바꿀 수 있다.
# 주의: Unity 에디터에서 이 프로젝트가 열려 있으면 실패한다(프로젝트 잠금).
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
UNITY="${UNITY_PATH:-/Applications/Unity/Hub/Editor/6000.0.1f1/Unity.app/Contents/MacOS/Unity}"
mkdir -p "$ROOT/Builds"
LOG="$ROOT/Builds/ios-build.log"

echo "Unity로 Xcode 프로젝트를 만드는 중... (로그: $LOG)"
if ! "$UNITY" -batchmode -quit -projectPath "$ROOT" -buildTarget iOS \
    -executeMethod SlotBattle.EditorTools.IOSBuild.Build -logFile "$LOG"; then
    echo "실패했습니다. 로그의 오류를 확인하세요:"
    grep -E "error|Error|실패" "$LOG" | tail -20 || true
    exit 1
fi

PROJECT="$ROOT/Builds/iOS/Unity-iPhone.xcodeproj"
echo "완료: $PROJECT"
if [ "${1:-}" = "--open" ]; then
    open "$PROJECT"
fi
