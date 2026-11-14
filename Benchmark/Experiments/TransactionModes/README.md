# Tsavorite トランザクション方式の採用判断試験

製品に方式を採用する前に、Tsavorite の履歴と既存 FLinq を組み合わせるコストを比較する独立した実験。対象の仕様は [中間仕様書](../../../Document/20261114Argon.md)。通常の製品 API や既存データは変更しない。

## 比較する方式

| CLI / JSON の値 | 内容 |
|---|---|
| `Single` / `0` | 取引全体を直列実行。通常の `Set<int,PlainRow>` を直接変更し、中止時は旧値を戻す |
| `Deferred` / `1` | 変更集合に保持し、楽観的検証と WAL 永続化の後で共有ログへ追記する |
| `Immediate` / `2` | 書込み時に未確定版を共有ログへ追記し、楽観的検証と WAL 永続化の後で可視化する |

三方式で同じ `WalWriter`、`WalFormat`、ファイルの `Flush(true)` を使用する。読み取り専用取引は WAL を書かない。並行方式では公開済み確定番号を取得して読み、キーの読み書き依存と全走査の表依存を確定時に検証する。競合した業務処理の自動再実行は行わない。

`Row` は40バイト、通常の `PlainRow` は16バイト。差分24バイトの内訳は取引所有者8、走査用の旧版アドレス8、論理削除フラグ8。Tsavorite 自身のヘッダーとキーは別に存在する。

## 前レコードの利用

`Storage.cs` は `InPlaceUpdater` を拒否し、`CopyUpdater` で `PreserveCopyUpdaterSourceRecord` を設定する。点検索の読み取りでは値と同時に実際の `Info.PreviousAddress` を取得し、`ReadAtAddress` で過去へ進む。ハッシュ連鎖には異なるキーが入るので、キーが一致した版だけを可視性の候補にする。

現在のコレクションのネイティブ走査は値だけを渡す。この実験では、最初の遡及に使う同一キーの `RMWInfo.SourceAddress` を値に保持する。コピー更新のコールバック中に新レコードのヘッダーを読んでも、保持処理後の最終前リンクとは限らないため、このアドレスを使う。二段目以降はネイティブの実際の連鎖を読む。この追加8バイトの費用も測定対象である。

通常の最新値走査から古い版を解決するだけでは、並行追記中にキーが欠落する場合がある。実験では**走査中の構造安定化スコープ**を置き、その間は同じ表への物理追記を待たせる。独立した書込みの物理追記も表内で短く直列化する。取引の計算・読み取り・WAL 待ちは並行できるが、更新を一切待たせない走査を実現した試験ではない。

`Queries.cs` のスコープ管理は通常の C# メソッドに置き、内部の `*Core` メソッドを FLinq が生成する。生成されたコードに `.Iterate(` があることを実行前に必ず検査する。`using var` を最適化対象の本体に入れると、現在の生成器で解放が保持されないケースを観測したため、この接続形を使用する。

## 規模と測定

- 主表1万 / 10万行、副表64行。1テーブルのログメモリ32 MiB、並行方式のログ使用量予算24 MiB。プロセス全体のメモリ上限を意味しない。
- メモリ / ファイル、同時実行1 / 2 / 4 / 8、読取100 / 95 / 50%、一様 / 1%集中、5回。方式の順序を交代する。
- 混合負荷は1取引で1キーを読み、必要なら値を1増やす。各セル0.5秒。成功取引数と成功時レイテンシを計測し、最後に成功した更新の総数と保存値を照合する。競合は成功に数えない。
- 別途、FLinq / LINQ の全走査を各8回、5反復で比較する。履歴8版の走査は1万行のみ。反復更新と中止によるログ増加も比較する。
- ファイル構成は測定前に `FlushAndEvict`。OS のファイルキャッシュは消去しない。メモリ構成でも耐久化用 WAL はファイルに書く。
- `Allocated` はセル全体の管理ヒープ割当量、`PeakWorkingSet` は20 ms間隔のプロセス計測。ネイティブログ使用量、WAL ファイル容量を別に保存する。短い標本なのでピークの取り逃し、JIT、スケジューラ、同居処理の影響がある。

## 実行

.NET SDK 10.0.401 とローカル NuGet キャッシュの Microsoft.Garnet 2.2.1 を使用する。このマシンの比較規約に従い、共有 BGO は使わず、共通8論理CPUに制限する。新規の作業ディレクトリを指定する。

```powershell
$taskRoot='D:\CodexTemp\Seamless\TransactionModes\my-run'
$logRoot='F:\CodexTemp\Seamless\TransactionModes\my-run'
New-Item -ItemType Directory -Path $taskRoot -Force | Out-Null
& .\TestSeamless\Invoke-LocalProcess.ps1 `
    -Executable 'C:\Users\kazuh\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\powershell\pwsh.exe' `
    -Arguments @('-NoProfile','-File','G:\GitHub\Seamless\Benchmark\Experiments\TransactionModes\Run.ps1',
                 '-TaskRoot',$taskRoot,'-LogRoot',$logRoot,'-Configuration','Debug','-Stage','accept') `
    -WorkingDirectory $taskRoot -ArtifactDirectory ($logRoot+'\launch-debug') `
    -TimeoutSeconds 240 -MaxLogicalProcessors 8
```

次に `Configuration=Release`、`Stage=accept`、`-Reuse` で同じソースを検証し、成功後に `Stage=bench`、`-Reuse` で性能を測る。性能測定のラッパーのタイムアウトは3600秒程度を確保する。各実行の `ArtifactDirectory` は別名にする。

`Run.ps1` は依存プロジェクトごとソースを D に複製し、ハッシュの台帳を F に残す。子プロセスの PID・生成時刻・CPU affinity と生成コードの場所も記録する。メモリ監視の登録・更新・終了を行い、85%以上で新しい重い処理を開始しない。実行中に85%以上になった場合、自分が起動した試験だけを中止する。

正確性結果は `<Configuration>-accept.json`。性能結果は `<Configuration>-bench.json`（JSON Lines）と `.summary.json`。成功したセルの一時データだけを、自分の試験ルート内と確認して削除する。失敗したセルは保持する。同じ性能レポートで再開したときは完了済みセルを省略する。失敗セルは性能上の合格に含めない。

性能計測の各起動で新しい fixture ルートを作る。中断時のレポートを再利用しても、完了した走査用ストアの WAL と新規 fixture の作成が衝突しない。プロセスの記録には fixture のルートと実行スクリプトのハッシュも含める。

## 保証の範囲

これは採用判断用プロトタイプであり、製品トランザクションの差替えではない。二つの表の原子的公開を検査するが、製品の二次索引維持、制約、Gateway、TPC の業務全体を実装したものではない。

履歴回収は活動中取引がない静止時だけに限定する。さらに、中止版が物理的な最新値として残る場合は回収を拒否する。通常の Compact が旧確定値を捨てることを防ぐためであり、即時方式で中止が起きた後の回収には MVCC 専用設計が必要である。保持予算超過をエラーにする。通常の engine tombstone、read cache、運転中の回収、チェックポイントからの再開、可変参照値、ネスト・保存点、取引IDの外部照会は受入範囲外。復旧試験は WAL フレームを読み、新規ストアへ再生する。製品のファイルデバイスの世代管理・ローテーション・部分破損からの全復旧を保証する試験ではない。

最小の履歴実験だけで即時方式を決めず、今回の正確性、履歴・中止の容量、点検索の並行性、FLinq の費用を合わせて判断する。

管理メタデータの到達性に基づく回収と、その総予算は未実装。ネイティブログを回収できる場合でも、長期運転のメモリ使用量をこの短時間試験から保証しない。

全セルの検査と中央値の集計は、Python 標準ライブラリだけで実行できる。

```powershell
& C:\Python314\python.exe -B .\Benchmark\Experiments\TransactionModes\Summarize.py `
    ($logRoot+'\Release-bench.json') ($logRoot+'\comparison-summary.json')
```

このコマンドも、このリポジトリの比較規約を適用したプロセスから実行する。1,582セル・各条件の5反復・セル名の重複・失敗の有無を確認してから集計する。
