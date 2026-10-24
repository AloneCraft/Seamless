#!/usr/bin/env bash
# C3 の実験を 1 コア(既定 CPU 14)で順に流す。結果は C:\tpccspike\results\*.txt。
# 使い方(Git Bash): bash Benchmark/Benchmark.TpcC.Spike/run-all.sh [現在の版の exe] [修正前の版の exe] [Debug ビルドの exe]
# 修正前の版(0679a83e = fd5504f9 の直前)の組み立て方は report.md の「再現手順」を参照。
set -u
EXE=${1:-/c/tpccspike/bin/current/Benchmark.TpcC.Spike.exe}
OLD=${2:-/c/tpccspike/old-artifacts/bin/Benchmark.TpcC.Spike/release/Benchmark.TpcC.Spike.exe}
DBG=${3:-/c/tpccspike/debug-artifacts/bin/Benchmark.TpcC.Spike/debug/Benchmark.TpcC.Spike.exe}
OUT=/c/tpccspike/results
mkdir -p "$OUT"
run(){
    local name=$1
    shift
    echo "== $name $(date +%H:%M:%S)"
    (time "$@") > "$OUT/$name.txt" 2>&1
    grep -E "^(PASS|FAIL|DONE)" "$OUT/$name.txt" | tail -1
}
# 6: 保存の大きさと時間(時間を測るので最初に、他の実験と重ねない)
run exp6-million-release "$EXE" exp6 --scale million --saves 6 --txns 3000 --release true
run exp6-million-open "$EXE" exp6 --scale million --saves 6 --txns 3000 --release false
# 2: 所有スレッドの受け渡し 100 万回
run exp2-thread "$EXE" exp2 --mode thread --handoffs 1000000
run exp2-pool "$EXE" exp2 --mode pool --handoffs 1000000
run exp2-async "$EXE" exp2 --mode async --handoffs 1000000
# Debug ビルド(シングル用の同時走査の検査が入る)でも受け渡しを短く
run exp2-thread-debug "$DBG" exp2 --mode thread --handoffs 100000 --save-every 20000 --verify-every 20000 --reacquire-every 15000
run exp2-pool-debug "$DBG" exp2 --mode pool --handoffs 100000 --save-every 20000 --verify-every 20000 --reacquire-every 15000
# 1: 同時の書き込みと保存
for by in owner other all; do
    for release in true false; do
        run "exp1-p4-$by-release$release" "$EXE" exp1 --partitions 4 --seconds 30 --save-by $by --release $release
    done
done
run exp1-p8-all "$EXE" exp1 --partitions 8 --seconds 30 --save-by all --release true
run exp1-p16-owner "$EXE" exp1 --partitions 16 --seconds 30 --save-by owner --release true --save-interval-ms 2000
run exp1-p4-owner-smallmem "$EXE" exp1 --partitions 4 --seconds 30 --save-by owner --small-memory true
# 3: LightEpoch
run exp3 "$EXE" exp3
# 4: 並列の開き直しと強制終了からの復旧
for p in 4 8 16; do
    run "exp4-open-p$p" "$EXE" exp4 --mode open --partitions $p --scale small
done
run exp4-crash-p4 "$EXE" exp4 --mode crash --partitions 4 --cycles 20
run exp4-crash-p8 "$EXE" exp4 --mode crash --partitions 8 --cycles 10
# 5: Remove のあとの保存→開き直し(修正前の版と比べる)
run exp5-sector-current "$EXE" exp5 --mode sector
run exp5-sweep-old "$OLD" exp5 --mode sweep --minutes 8 --seed 1
run exp5-sweep-current "$EXE" exp5 --mode sweep --minutes 25 --seed 1
echo "== all done $(date +%H:%M:%S)"
