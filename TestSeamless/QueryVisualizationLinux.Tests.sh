#!/usr/bin/env bash
set -euo pipefail
runner=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd -P)
run=${1:-}
phase=${2:-}
test -n "$run" && test -d "$run" || { echo 'Usage: QueryVisualizationLinux.Tests.sh <prepared LinuxRun> build|ui|preview|generated|trace-core|trace-recorder|trace-native|history|typed|typed-crash|heat|object-ui' >&2; exit 2; }
run=$(CDPATH= cd -- "$run" && pwd -P)
readarray -t settings < <(python3 - "$run/prepared.json" <<'PY'
import json,sys
p=json.load(open(sys.argv[1],encoding='utf-8-sig'))
print(p['Nonce']);print(p['LinuxRun']);print(p['LinuxSourceSnapshot']);print(p['Configuration'])
PY
)
nonce=${settings[0]}
source=${settings[2]}
test "$run" = "${settings[1]}" && test "${settings[3]}" = Release
case "$phase" in build|ui|preview|generated|trace-core|trace-recorder|trace-native|history|typed|typed-crash|heat|object-ui) ;; *) echo 'An explicit supported phase is required.' >&2; exit 2;; esac
export DOTNET_ROOT=/opt/seamless/dotnet PATH=/opt/seamless/dotnet:$PATH
export DOTNET_CLI_HOME=/home/kazuh/.cache/Seamless/prepared-wal-tests/dotnet-home
export NUGET_PACKAGES=/home/kazuh/.cache/Seamless/prepared-wal-tests/nuget
export TMPDIR=/home/kazuh/.cache/Seamless/tmp
export DOTNET_PROCESSOR_COUNT=8 MSBUILDDISABLENODEREUSE=1 DOTNET_HOST_PATH=/opt/seamless/dotnet/dotnet
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 DOTNET_GENERATE_ASPNET_CERTIFICATE=false
export XDG_RUNTIME_DIR=/run/user/1000 DBUS_SESSION_BUS_ADDRESS=unix:path=/run/user/1000/bus
export SEAMLESS_SOURCE_REPOSITORY="$source" SEAMLESS_TEST_REPOSITORY="$source"
export SEAMLESS_MANAGEMENT_TEST_ROOT="/home/kazuh/.local/share/Seamless/nt-$nonce/db"
export SEAMLESS_TEST_DATA_ROOT="/home/kazuh/.local/share/Seamless/nt-$nonce/data"
export SEAMLESS_TEST_CACHE_ROOT="/home/kazuh/.cache/Seamless/nt-$nonce"
project="$source/Tests/Management.Tests/Management.Tests.csproj"
work="$run/near-tests"
unit="seamless-vis-tests-$nonce-$phase.scope"
umask 077
if test "${3:-}" != inner; then
    test -f "$run/linux-runtime-proof.json"
    test -d "$run/artifacts" && test -x "$DOTNET_HOST_PATH"
    test -f "$runner/QueryVisualizationLinux.Tests.Verify.py"
    mkdir -p "$work"
    (set -o noclobber; : > "$work/$phase.claim") || { echo 'This test phase was already claimed. Retain its evidence and prepare an explicit new attempt.' >&2; exit 1; }
    exec systemd-run --user --scope --unit="$unit" -p MemoryHigh=1400M -p MemoryMax=1700M -p CPUQuota=800% taskset -c 0-7 bash "$0" "$run" "$phase" inner
fi
grep -Fq "$unit" /proc/self/cgroup
test "$(taskset -pc $$ | sed 's/.*: //')" = 0-7
verify() { python3 "$run/QueryVisualizationLinux.Verify.py" "$1" "$run" "$2"; }
verify main "tests-before-$phase"
verify source "tests-before-$phase"
python3 "$runner/QueryVisualizationLinux.Tests.Verify.py" input "$run" "$phase"
finish() {
    status=$?
    trap - EXIT
    set +e
    verify source "tests-after-$phase"; source_status=$?
    verify main "tests-after-$phase"; main_status=$?
    python3 "$runner/QueryVisualizationLinux.Tests.Verify.py" result "$run" "$phase" "$status" "$source_status" "$main_status"
    proof_status=$?
    if test "$status" -ne 0; then exit "$status"; fi
    if test "$source_status" -ne 0 || test "$main_status" -ne 0 || test "$proof_status" -ne 0; then exit 1; fi
}
trap finish EXIT
if test "$phase" = build; then
    test ! -e "$SEAMLESS_MANAGEMENT_TEST_ROOT" && test ! -e "$SEAMLESS_TEST_DATA_ROOT" && test ! -e "$SEAMLESS_TEST_CACHE_ROOT"
fi
mkdir -p "$SEAMLESS_MANAGEMENT_TEST_ROOT" "$SEAMLESS_TEST_DATA_ROOT" "$SEAMLESS_TEST_CACHE_ROOT"
test -f "$work/serial.runsettings" || printf '%s\n' '<RunSettings><xUnit><ParallelizeTestCollections>false</ParallelizeTestCollections><MaxParallelThreads>1</MaxParallelThreads></xUnit></RunSettings>' > "$work/serial.runsettings"
cd "$source"
if test "$phase" = build; then
    dotnet msbuild "$project" -getProperty:ArtifactsPath,BaseOutputPath,BaseIntermediateOutputPath,RestorePackagesPath -p:UseArtifactsOutput=true -p:ArtifactsPath="$run/artifacts" -p:RestorePackagesPath="$NUGET_PACKAGES" -p:Configuration=Release -nologo > "$work/paths.json"
    python3 "$runner/QueryVisualizationLinux.Tests.Verify.py" paths "$run" "$phase"
    timeout --signal=TERM --kill-after=30s 1200s dotnet build "$project" -c Release --artifacts-path "$run/artifacts" -m:1 -p:UseSharedCompilation=false -p:NuGetAudit=false -p:RestorePackagesPath="$NUGET_PACKAGES" -p:RestoreFallbackFolders=/var/tmp/seamless/nuget 2>&1 | tee "$work/build.log"
    python3 "$runner/QueryVisualizationLinux.Tests.Verify.py" native "$run" "$phase"
    exit 0
fi
test -f "$work/build-result.json"
case "$phase" in
    object-ui) filter='FullyQualifiedName~Management.Tests.QueryProfiler.|FullyQualifiedName~Management.Tests.ObjectExecutionProfiling.|FullyQualifiedName~Management.Tests.ExecutionGateMeasurement.|FullyQualifiedName~Management.Tests.QueryProfileProjection.|FullyQualifiedName~Management.Tests.ResourceHeatHistory.|FullyQualifiedName~Management.Tests.ResourceHeatHistoryView.|FullyQualifiedName~Management.Tests.CommonFunctions.|FullyQualifiedName~Management.Tests.ObjectDefinitionEditing.|FullyQualifiedName~Management.Tests.ObjectDocuments.|FullyQualifiedName~Management.Tests.ObjectDocumentPlanVersions.|FullyQualifiedName~Management.Tests.QueryVisualizationFlow.|FullyQualifiedName~Management.Tests.ExecutionGantt.|FullyQualifiedName~Management.Tests.QueryVisualizationIntegration.|FullyQualifiedName~Management.Tests.TreeExpansion.|FullyQualifiedName~Management.Tests.ManagerDatabaseLoading.|FullyQualifiedName~Management.Tests.ContextualAdditions.|FullyQualifiedName~Management.Tests.ResultsGridOperations.|FullyQualifiedName~Management.Tests.ResultsGridCells.|FullyQualifiedName~Management.Tests.QueryPlanPresentation.|FullyQualifiedName~Management.Tests.ResourceHeatGantt.|FullyQualifiedName~Management.Tests.PlanSnapshots.|FullyQualifiedName~Management.Tests.PlanSnapshotVisualization.|FullyQualifiedName~Management.Tests.ResourceHeatPlayback.|FullyQualifiedName~Management.Tests.ResourceHeatCollection.|FullyQualifiedName~Management.Tests.CheckpointPathCompatibility.|FullyQualifiedName~Management.Tests.CreationDrafts.' ;;
    heat) filter='FullyQualifiedName~Management.Tests.ResourceHeatCollection.|FullyQualifiedName~Management.Tests.ResourceHeatGantt.|FullyQualifiedName~Management.Tests.ResourceHeatPlayback.|FullyQualifiedName~Management.Tests.PlanSnapshotVisualization.' ;;
    ui) filter='FullyQualifiedName~QueryPlanPresentation|FullyQualifiedName~PlanGraph|FullyQualifiedName~QueryTracePresentation|FullyQualifiedName~QueryTraceLayout|FullyQualifiedName~QueryExecutionHistory.HistoryView|FullyQualifiedName~ManagementTypedStore.OfflineCopiesAndRepeatedUpgrade|FullyQualifiedName~ManagementTypedStore.UnknownFormatIsRejected|FullyQualifiedName~ManagementTypedStore.MissingCatalogRow' ;;
    generated) filter='FullyQualifiedName~Management.Tests.GeneratedControlFlow.|FullyQualifiedName~Management.Tests.GeneratedCodeContracts.|FullyQualifiedName~Management.Tests.QueryPlanBinding.|FullyQualifiedName~Management.Tests.PlanSnapshots.' ;;
    preview) filter='FullyQualifiedName~Management.Tests.GeneratedControlFlowPresentation.|FullyQualifiedName~Management.Tests.PlanElementMeaning.|FullyQualifiedName~Management.Tests.TreeExpansion.|FullyQualifiedName~Management.Tests.ManagerDatabaseLoading.|FullyQualifiedName~Management.Tests.ContextualAdditions.|FullyQualifiedName~Management.Tests.ResultsGridOperations.|FullyQualifiedName~Management.Tests.ResultsGridCells.' ;;
    trace-core) filter='FullyQualifiedName~Management.Tests.QueryTraceCore.' ;;
    trace-recorder) filter='FullyQualifiedName~Management.Tests.QueryTraceRecorderTests.' ;;
    trace-native) filter='FullyQualifiedName~Management.Tests.QueryTraceIntegration.|FullyQualifiedName~Management.Tests.QueryTraceSamplingIntegration.' ;;
    history) filter='FullyQualifiedName~Management.Tests.QueryExecutionHistory.|FullyQualifiedName~Management.Tests.ExecutionHistoryIntegrity.|FullyQualifiedName~Management.Tests.QueryExecutionHistoryAuthorization.' ;;
    typed) filter='FullyQualifiedName~Management.Tests.ManagementTypedStore.' ;;
    typed-crash) filter='FullyQualifiedName~Management.Tests.ManagementTypedUpgradeCrash.' ;;
esac
printf '%s\n' "$filter" > "$work/$phase.filter.txt"
timeout --signal=TERM --kill-after=30s 1200s dotnet test "$project" -c Release --artifacts-path "$run/artifacts" --no-build --no-restore --settings "$work/serial.runsettings" --filter "$filter" --logger 'console;verbosity=normal' --logger 'trx;LogFileName=checks.trx' --results-directory "$work/$phase" 2>&1 | tee "$work/$phase.log"
