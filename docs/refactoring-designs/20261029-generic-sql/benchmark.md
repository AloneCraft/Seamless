# 公平な共通比較・計測仕様

## 前提
同じSQL文字列より処理・結果・取引の同等性を優先する。SQL Server/MySQL/PostgreSQLのサーバー版、storage engine、collation/timezone、索引、分離・durabilityを固定し記録する。版は各受け入れrunのmanifestに固定し、latest tagだけを結果識別子にしない。

## 共通fixtureと12workload
Customers(Id PK,Name,Age)、Orders(Id PK,CustomerId FK,Amount decimal(18,2) NULL,CreatedAt timestamp相当)を基本とし、件数/seed/NULL比率/重複文字列/偏り/索引は[固定workload仕様](workloads.json)に定義する。これは将来driver向けの仕様で、実行済みfixtureではない。意味が異なる型はDB別DDLと理由を併記する。

元の比較DBをfixtureとして再利用せず、管理所有markerを検査した専用DBだけを準備・更新する。名前の一致だけを所有証明にしない。

W01 PK lookup、W02 数値range、W03 projection/filter、W04 COUNT/空集合、W05 SUM/AVG/MIN/MAX、W06 GROUP/HAVING、W07 INNER JOIN、W08 LEFT JOIN/NULL、W09 DISTINCT、W10 安定key付き並べ替えとページング、W11 fixture CRUD commit、W12 fixture rollback。DDLはfixture準備で行い測定区間に含めない。更新workloadは各試行の初期状態を復元し復元時間を区間外へ置く。

各caseにID、dialect別SQL、引数型/値、期待列名/順/型、行の多重集合、順序key、native ID、意味論、materialization条件、budget、timeoutを定義する。TOP/LIMITは一意順序を指定し、同順位内の順序を要求しない。portable SQLで意味が揃わないcaseはNotEquivalentとして共通速度比から除外する。

COUNTの幅やSUMのprecision等、DB間で正当に異なるmetadataは方言別期待契約を持つ。SQL Serverとその互換profileの比較ではmetadataも一致させる。3DB共通比較は値が失われない論理カテゴリで対応させ、型差を記録し、無条件のCLR型変換で隠さない。COUNTの64bit比較にはSQL ServerだけCOUNT_BIGを使う。

## A〜G
| 区分 | 区間 |
|---|---|
| A SQL E2E | 事前接続済みのcommand送信開始から全結果・終端受信。接続/auth時間は別値 |
| B parser | SQL入力からAST全消費。cache無効、DB実行なし |
| C binding/optimization | 構文解析済み入力からplan完成。catalog版を固定 |
| D cached SQL | Prepare/cache hitを明示し実行・全件消費。missを混ぜない |
| E engine | parser/通信を除いた同じplanの全件実行。Seamless内比較とし他DBのSQL内部時間と同一視しない |
| F native | LINQ/FLinq生成経路の直接実行とnative remoteを別記。同じprojection/全件消費・意味論が前提 |
| G transaction/recovery | durability設定とcommit境界、log flush、障害点、再起動、再実行の重複/消失を分ける |

Release、2warmup、7測定round、同時1/4/16が既定。各cellは10秒/roundを初期設定とし要求数も記録。p99は1000要求未満なら不足表示し正式比較に使わない。標本quantileであることを表示し真のtail保証としない。SQL経路の順を交互/seed付き順序で変え、warm/coldを別にする。性能比の合格閾値は設定しない。

## 正しさと指標
整数/decimal/文字列/日時/取り込み値は厳密。浮動計算由来と確定した列だけSingle abs1e-6/rel1e-5、Double abs1e-12/rel1e-9を既定に使用。設定値・対象ordinalを保存。SQL/native両方の順序を検証。実装同士の一致だけでなく独立期待値と元RDBMSを用いる。

throughput、標本p50/p95/p99、CPU、Working Set/Private Bytes観測、GC回数/alloc、I/Oを記録。PID+creation timeを照合し、要求elapsedとbatch counter区間の差を説明する。in-processではharness分も同一processへ入る限界を記録する。asyncの割当を1thread counterの差だけで測らず、他仕事を隔離したprocess/EventPipe等を使用。OS process transferは物理disk I/Oとしない。取得不能はUnavailable、native未定義はNotImplemented、意味不一致はFailed/NotEquivalent。0に置換しない。

正しさ照合とreport生成は測定外。結果を全件List化せず有界chunk/外部sortを使う。資源超過は失敗。JSON/CSV/HTMLにデータ世代、SQL差分、版/hash、hardware/OS/runtime、cache状態、全条件、失敗理由・標本数を保存する。サーバー背景処理と競合I/Oの混入も記録する。
