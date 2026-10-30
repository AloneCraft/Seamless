#!/usr/bin/env bash
# run-all.sh の後に、検証で足りないと分かった点を補う再実行。最終版のバイナリで取り直す(1 コア、CPU 14)。
# 準備(Git Bash。ビルドは -m:1):
#   現在の版   dotnet build Benchmark/Benchmark.TpcC.Spike/Benchmark.TpcC.Spike.csproj -c Release -m:1 --artifacts-path C:\tpccspike\final-current
#   修正前の版 git archive 0679a83e Seamless.Collections.Tsavorite Seamless.Collections Seamless.Execution Directory.Build.props Directory.Build.targets global.json README.md | tar -x -C /c/tpccspike/old-src
#              dotnet build ... -c Release -m:1 "-p:SeamlessRoot=C:\tpccspike\old-src\" --artifacts-path C:\tpccspike\final-old
set -u
NEW=/c/tpccspike/final-current/bin/Benchmark.TpcC.Spike/release/Benchmark.TpcC.Spike.exe
OLD=/c/tpccspike/final-old/bin/Benchmark.TpcC.Spike/release/Benchmark.TpcC.Spike.exe
OUT=/c/tpccspike/results
mkdir -p "$OUT"
run(){
    local name=$1
    shift
    echo "== $name $(date +%H:%M:%S)"
    (time "$@") > "$OUT/$name.txt" 2>&1
    grep -E "^(PASS|FAIL|DONE)" "$OUT/$name.txt" | tail -1
}
# 5: Set の行の大きさ別(Pad16=R88、Pad64=R136 は修正前で落ちると予測)
run exp5-sectorscan2-old "$OLD" exp5 --mode sectorscan --kinds dict,set0,set3,set4 --max 300
run exp5-sectorscan2-current "$NEW" exp5 --mode sectorscan --kinds dict,set0,set3,set4 --max 300
# 1: 保存の重なりを数える(最大同時 Store.Save、他の取引の最中に始まった保存の回数)
run exp1b-p4-all "$NEW" exp1 --partitions 4 --seconds 60 --save-by all --release true --save-interval-ms 300
run exp1b-p8-all "$NEW" exp1 --partitions 8 --seconds 60 --save-by all --release true --save-interval-ms 300
run exp1b-p8-owner "$NEW" exp1 --partitions 8 --seconds 60 --save-by owner --release true --save-interval-ms 300
run exp1b-p4-other "$NEW" exp1 --partitions 4 --seconds 60 --save-by other --release true --save-interval-ms 300
# 3: 上限ちょうどのメモリ(private / gc-committed)と保存単独の時間
run exp3b "$NEW" exp3
# 6: 順序を入れ替えて 3 回ずつ(release を交互に)。ゲートを握る全体の時間も出す
for round in 1 2 3; do
    run exp6b-r$round-release "$NEW" exp6 --scale million --saves 6 --txns 3000 --release true
    run exp6b-r$round-open "$NEW" exp6 --scale million --saves 6 --txns 3000 --release false
done
# 6: 小規模(約 5.7 万行)のログを残す
run exp6b-small "$NEW" exp6 --scale small --saves 4 --txns 500 --release true
# 追加(限界を埋める): 保存の積み重ね・データ本体の数・Set の固定メモリ・メモリ内の 100 万回受け渡し
run exp7-checkpoints "$NEW" exp7 --mode checkpoints --scale small --steps 1,5,10,20,40,80 --txns 50
run exp7-storecount "$NEW" exp7 --mode storecount --rows 20000 --counts 1,2,4,9,18,36
run exp7-setoverhead "$NEW" exp7 --mode setoverhead
run exp2-thread-bigmem-1m "$NEW" exp2 --mode thread --handoffs 1000000 --small-memory false --save-every 100000 --verify-every 100000 --reacquire-every 50000
run exp2-pool-bigmem-1m "$NEW" exp2 --mode pool --handoffs 1000000 --small-memory false --save-every 100000 --verify-every 100000 --reacquire-every 50000
# 複数コア(他プロセスの負荷が低いと確認してから短時間だけ。論理 CPU 1,3,5,7)
# run exp2mc-thread-bigmem-1m "$NEW" exp2 --mode thread --handoffs 1000000 --small-memory false --save-every 100000 --verify-every 100000 --reacquire-every 50000 --cpu 1,3,5,7
# run exp1mc-p4-all "$NEW" exp1 --partitions 4 --seconds 30 --save-by all --release true --save-interval-ms 300 --cpu 1,3,5,7
# run exp4mc-crash-p4-saves "$NEW" exp4 --mode crash --partitions 4 --cycles 12 --kill-mode saves --save-interval-ms 600 --seed 5 --cpu 1,3,5,7
echo "== final done $(date +%H:%M:%S)"
