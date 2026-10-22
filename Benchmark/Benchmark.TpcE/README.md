# Benchmark.TpcE

TPC-E(証券取引の業務ベンチ、仕様 1.14.0)を模擬する、負荷試験と不具合探しのコンソールプログラム。
Tsavorite と Persisted の両方に同じデータを置き、標準 LINQ と FLinq の両方で同じ取引を流して、**4通りが食い違わないか**を見る。

| 略称 | 保存先 | LINQ |
|---|---|---|
| TL | Tsavorite | 標準 LINQ |
| TF | Tsavorite | FLinq |
| PL | Persisted | 標準 LINQ |
| PF | Persisted | FLinq |

主な目的は速さの比較ではなく、次の確認である。

- 同じ乱数の種で同じ取引を流したとき、4通りの**出力**・**書いた中身**・**読み戻し**・**全表と全索引のダイジェスト**が一致するか。
- 保存→閉じる→開き直しで、状態が変わらないか。
- 強制終了(子プロセスを保存の途中などで止める)から開き直した状態が、最後に確定した保存と一致するか。

食い違いや例外が出たら、その場で止めて再現情報(乱数の種・何件目のどの取引か・食い違った行・再現コマンド)を出し、終了コードを 0 以外にする。

## 使い方

```bash
dotnet run -c Release --project Benchmark/Benchmark.TpcE -- run
```

```bash
dotnet run -c Release --project Benchmark/Benchmark.TpcE -- crash --cycles 3
```

```bash
dotnet run -c Release --project Benchmark/Benchmark.TpcE -- describe
```

出力は `Work/TpcE/<日時>-s<種>-<コマンド>/`(`Work/` は git 管理外)に書く。
`summary.txt`(要約)、`summary.json`、`txn-stats.csv`(取引種別×組み合わせの時間)、`run.json`(引数)、失敗したときは `failure/`。

### コマンド

| コマンド | 内容 |
|---|---|
| `run`(既定) | 4通りを交互に流して突き合わせる |
| `crash` | 強制終了の検査。子プロセスを保存の決まった段階や時間で止め、開き直した状態を参照と比べる(既定は TF と PF) |
| `replay --from <実行フォルダ>` | 失敗した実行を、その直前まで流し直して詳しく記録する |
| `generate` | 初期データだけ作って、表ごとの件数とダイジェストを出す |
| `describe` | 規模ごとの件数、推測で決めた値、FLinq を止めた問い合わせを出す。流さない |

### 主な引数

| 引数 | 既定 | 備考 |
|---|---|---|
| `--seed` | 1 | 同じ種なら毎回同じ取引が同じ順に流れる |
| `--customers` | 1000 | 仕様は 1,000 顧客を 1 単位とする。それ未満も指定できる(仕様外の縮小) |
| `--initial-trade-days` | 1 | 初期取引の営業日数(仕様は 300) |
| `--market-days` | 260 | 日次相場の営業日数(仕様は 1,305) |
| `--news-item-bytes` | 1024 | ニュース本文の大きさ(仕様の上限は 100,000) |
| `--transactions` | 15000 | 流す CE 取引の件数 |
| `--warmup` | 1000 | 時間の集計から外す最初の CE 件数(正しさの検査には含める) |
| `--variants` | all | `TL,TF,PL,PF` の部分集合。1 つだけだと突き合わせはせず、読み戻しと開き直しだけ |
| `--save-every` | 5000 | 何件の CE ごとに保存するか |
| `--reopen-every` | 2 | 保存の何回に 1 回、閉じて開き直すか。最後には必ず開き直す |
| `--stop-at` / `--trace-from` | なし | 再現用。この通し番号の直前で止める / ここから詳しく記録する |
| `--inject-fault` | なし | `ENGINE:TXN:KIND`(KIND は `Output` / `DropWrite` / `ReopenTamper`)。検出できるかの確認用 |
| `--priority` | belownormal | 他の作業を邪魔しない。`normal` / `idle` も可 |
| `--cpu` | なし | 指定すると 1 つの論理 CPU に固定する |
| `--keep-stores` | なし | 成功したときもストアを残す(失敗したときは常に残る) |
| `crash` の `--cycles` | 12 | 組み合わせごとの周期の数 |

実行に影響する引数は、失敗時に出す再現コマンドへすべて入る。

### 終了コード

| コード | 意味 |
|---|---|
| 0 | 成功 |
| 1 | 不一致(出力・書き込み・読み戻し・ダイジェスト・開き直し・強制終了からの復旧) |
| 2 | 想定外の例外 |
| 3 | 引数の誤り |
| 4 | 資源の上限(メモリ・ディスク・ログ満杯) |
| 5 | 子プロセスの応答なし |
| 130 | Ctrl+C |

## 速さを測るとき

4通りを 1 取引ずつ交互に流すので、キャッシュや GC が混ざり、絶対値は悪く出る。
公平な速さが要るときは `--variants TF` のように 1 つだけを流す。
JIT の最適化が落ち着くまでの最初の `--warmup` 件は集計から外す。

## 構成

- `Benchmark.TpcE.Core`: 行型・キー型、33 表と 18 本の副索引、合成データ生成、運転側(CE・MEE・DM)、4 通りを交互に流す実行器、強制終了の検査、計測。
- `Shared/TpcE.Transactions`(共有プロジェクト): 12 取引の処理。LINQ は `*Queries` にだけ書き、`*Frames` から呼ぶ。
- `Benchmark.TpcE.{Tsavorite,Persisted}.{Linq,FLinq}`: 共有の取引コードを別名 2 つ(`TpcEDatabase` と `LinqInterceptAttribute`)だけ変えて 4 回コンパイルする。
- `Benchmark.TpcE.Tests`: ベンチ自体のテスト(CI には入れていない)。

```bash
dotnet test Benchmark/Benchmark.TpcE.Tests -c Release -m:1
```

## 仕様からの簡略化

- 初期データは EGen の入力ファイルを使わず、合成データ(件数比・表の関係・値の範囲は仕様 2.6 に合わせ、名前などの文字列は種から決定的に作る)。
- 推測で決めた値は `describe` に一覧を出す(口座の税区分、銘柄の価格表、上場取引所の割合など)。
- 1 スレッドで流し、取引の隔離はしない。巻き戻し(Trade-Order の約 1%)は取り消し記録で逆に書いて行う。
- 日次相場の日数・ニュース本文・初期取引の日数は既定で縮小(引数で仕様値にできる)。
- TPC-E の整合条件の全件検査・応答時間の制約・待ち時間・複数の CE の並列実行は扱わない。

## 見つけたライブラリの不具合

`Work/TpcE/found-bugs.md` に再現手順つきで記録している(Work/qa への登録前のメモ)。

- FLinq: ラムダの中の入れ子の LINQ が外側のメソッド引数を使うと、生成コードが CS0103 になる。ベンチ側は外側をループにして回避している。
- Tsavorite: セクター境界がログのレコードを分割すると、次の復元で NullReferenceException になる(`CollectionStore.FlushLogToSectorBoundary`)。修正済みで、再発を強制終了の検査が見張る。

## 既知の回避

- FLinq の問い合わせを止める `[LinqIntercept(false)]` は使っていない(`describe` で確認できる)。
- 順序の無い列(Set・辞書・GroupBy)の後で `Take`/`First` を使うときは、必ず一意な副キーまで含めて並べ替える(4 通りで列挙順が違うため)。
