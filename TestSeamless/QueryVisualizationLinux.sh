#!/usr/bin/env bash
set -euo pipefail
run=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd -P)
readarray -t settings < <(python3 - "$run/prepared.json" <<'PY'
import json,sys
data=json.load(open(sys.argv[1],encoding='utf-8-sig'))
print(data['Nonce']);print(data['Configuration']);print(data['LinuxRun']);print(data['LinuxSourceSnapshot'])
PY
)
nonce=${settings[0]}
configuration=${settings[1]}
source=${settings[3]}
test "$run" = "${settings[2]}"
test "$configuration" = Debug || test "$configuration" = Release
export DOTNET_ROOT=/opt/seamless/dotnet PATH=/opt/seamless/dotnet:$PATH
export DOTNET_CLI_HOME=/home/kazuh/.cache/Seamless/prepared-wal-tests/dotnet-home
export NUGET_PACKAGES=/home/kazuh/.cache/Seamless/prepared-wal-tests/nuget
export TMPDIR=/home/kazuh/.cache/Seamless/tmp
export DOTNET_PROCESSOR_COUNT=8 MSBUILDDISABLENODEREUSE=1 DOTNET_HOST_PATH=/opt/seamless/dotnet/dotnet
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false DOTNET_NOLOGO=1 DOTNET_GENERATE_ASPNET_CERTIFICATE=false
export XDG_RUNTIME_DIR=/run/user/1000 DBUS_SESSION_BUS_ADDRESS=unix:path=/run/user/1000/bus
private=/home/kazuh/.cache/Seamless/visual-plan-$nonce
dbroot=/home/kazuh/.local/share/Seamless/visual-plan-$nonce
build_unit=seamless-vis-build-$nonce.scope
umask 077
claim() { (set -o noclobber; : > "$run/$1.claim") || { echo 'This phase was already claimed.' >&2; exit 1; }; }
verify_main() { python3 "$run/QueryVisualizationLinux.Verify.py" main "$run" "$1"; }
case "${1:-}" in
  prepare)
    claim prepare
    test -x /opt/seamless/dotnet/dotnet
    test -d "$NUGET_PACKAGES"
    test -d /var/tmp/seamless/nuget
    mkdir -p "$TMPDIR"
    test ! -e "$private" && test ! -e "$dbroot"
    mkdir -m 700 "$private"
    verify_main prepared
    python3 "$run/QueryVisualizationLinux.Verify.py" source "$run" prepared
    taskset -c 0-7 dotnet --version > "$run/linux-sdk-version.txt"
    ;;
  build)
    claim build
    test -f "$run/main-prepared.json"
    test ! -e "$run/artifacts" && test ! -e "$run/runtime"
    exec systemd-run --user --scope --unit="$build_unit" -p MemoryHigh=1400M -p MemoryMax=1700M -p CPUQuota=800% taskset -c 0-7 bash "$0" build-inner
    ;;
  build-inner)
    grep -Fq "$build_unit" /proc/self/cgroup
    verify_main before-build
    python3 "$run/QueryVisualizationLinux.Verify.py" source "$run" before-build
    test "$(taskset -pc $$ | sed 's/.*: //')" = 0-7
    for project in "$run/probe/QueryVisualizationAcceptance.csproj" "$source/Seamless/Database/Server/Server.csproj"; do
      name=$(basename "$project" .csproj)
      dotnet msbuild "$project" -getProperty:ArtifactsPath,BaseOutputPath,BaseIntermediateOutputPath,RestorePackagesPath -p:UseArtifactsOutput=true -p:ArtifactsPath="$run/artifacts" -p:RestorePackagesPath="$NUGET_PACKAGES" -p:Configuration="$configuration" -nologo > "$run/paths-$name.json"
    done
    python3 "$run/QueryVisualizationLinux.Verify.py" paths "$run"
    dotnet publish "$run/probe/QueryVisualizationAcceptance.csproj" -c "$configuration" --artifacts-path "$run/artifacts" -o "$run/runtime" -m:1 -p:UseSharedCompilation=false -p:NuGetAudit=false -p:RestorePackagesPath="$NUGET_PACKAGES" -p:RestoreFallbackFolders=/var/tmp/seamless/nuget
    python3 "$run/QueryVisualizationLinux.Verify.py" source "$run" after-build
    python3 "$run/QueryVisualizationLinux.Verify.py" runtime "$run"
    verify_main after-build
    ;;
  create|open)
    phase=$1
    host_unit=seamless-vis-host-$nonce-$phase.scope
    claim "$phase"
    test -f "$run/linux-runtime-proof.json"
    if test "$phase" = create; then test ! -e "$dbroot"; else test -d "$dbroot"; fi
    mkdir -m 700 "$private/$phase"
    chmod 700 "$private" "$private/$phase"
    verify_main "before-$phase"
    # private-launch.jsonを含むログ・証拠はLinux所有者の0700領域へ出力する。
    exec systemd-run --user --scope --unit="$host_unit" -p MemoryHigh=850M -p MemoryMax=1200M -p CPUQuota=800% taskset -c 0-7 dotnet "$run/runtime/QueryVisualizationAcceptance.dll" "$phase" "$dbroot" "$private/$phase" "$run/stop-$phase"
    ;;
  stop)
    phase=${2:-}
    test "$phase" = create || test "$phase" = open
    host_unit=seamless-vis-host-$nonce-$phase.scope
    test -f "$private/$phase/ready.json"
    python3 "$run/QueryVisualizationLinux.Verify.py" host "$run" "$private/$phase/ready.json" "$host_unit"
    : > "$run/stop-$phase"
    python3 - "$host_unit" <<'PY'
import subprocess,sys,time
deadline=time.monotonic()+90
while subprocess.run(['systemctl','--user','is-active','--quiet',sys.argv[1]]).returncode==0:
    if time.monotonic()>=deadline: raise SystemExit('Host scope did not stop. Leave ownership reserved.')
    time.sleep(.2)
PY
    verify_main "after-$phase"
    ;;
  *) echo 'Usage: QueryVisualizationLinux.sh prepare|build|create|open|stop create|stop open' >&2; exit 2 ;;
esac
