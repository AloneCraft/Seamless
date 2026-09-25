# 不変条件・禁止事項・前提・作業境界

機能 ID と証拠 S-ID は [機能一覧](01_Functions.md) を参照する。既存の契約と今後の改善要求を区別する。ここに書かれた契約を現在の全コードが満たしているという意味ではなく、反する箇所はバックログと受け入れ項目へ結びつける。

## 不変条件

| ID | 変更後も成立すべき条件 | 根拠 / 検証 |
|---|---|---|
| INV-01 | 同じ入力・意味論・比較子の下で、結果、重複、順序、例外、副作用の評価回数、列挙子の解放を保つ。性能改善で観測可能な意味を変えない | S-06～S-08 / AC-LINQ |
| INV-02 | System.Linq 互換の等値性と、ScopedQuery の決定的バイト一致を分離する。明示比較子は対応する API の契約に従って優先する | S-03 / AC-CODEC |
| INV-03 | 大規模データ経路は共有予算で予約し、全件がメモリに収まることを前提にしない。退避不能・上限超過は理由付きで失敗する | S-01、S-03。Compact の既存違反は GAP-02 / AC-RESOURCE |
| INV-04 | スコープ所有の遅延列・格納列・グループは scope の寿命を越えて使用できない。同じ scope の同時列挙制限を維持する | S-03、S-06 / AC-LIFETIME |
| INV-05 | 一時資源は所有者・形式・lock を確認して回収する。他の実行、所有不明、壊れた識別情報の資源を削除しない | S-03 / AC-LIFETIME |
| INV-06 | Tsavorite のデータ本体とスレッドごとの操作オブジェクトを区別し、Count の正確さ・不明状態・再計数を混同しない。long 件数を正本とし、int への無言の桁あふれを禁止する | S-05、S-09 / AC-COLLECTION |
| INV-07 | 共通 Store → エンジン別作成の利用モデルを維持する。Tsavorite は作成したデータ本体からスレッドごとの操作オブジェクトを作り、Persisted 拡張は collection を直接返す。エンジン固有設定をすべての利用側へ露出し直さない | S-02、S-04、S-05 / AC-API |
| INV-08 | 永続 Store の保存境界と DB の transaction commit 境界を区別する。DB は commit 済み WAL のみ復旧し、checkpoint 未完了の対象があれば必要な WAL を保持する | S-02、S-03、S-09 / AC-RECOVERY |
| INV-09 | 読取セッション中の writer 待機、同一実行文脈の昇格拒否、writer 直列化、内側 rollback による外側 transaction 全体の中止を維持する | S-03、S-09 / AC-DB |
| INV-10 | SQL import は staging の検証が成功するまで宛先を公開せず、置換時は旧宛先を backup として残す。未対応 SQL は対象を示して拒否する | S-10 / AC-IMPORT |
| INV-11 | FLinq Registry を演算子登録・能力・coverage の正本とし、生成の入力単位、診断位置、trace 順序、incremental cache 分離を維持する | S-07、S-17 / AC-GENERATOR |
| INV-12 | 順序なし列に位置依存演算を適用するには既存の明示 opt-in 契約を要求する。未対応の強制 intercept を黙って成功扱いしない | S-07、S-08 / AC-GENERATOR |
| INV-13 | 測定結果には実際に使ったソース・Worker・依存 DLL・入力・条件を対応づける。条件不一致を比較可能としない | S-11～S-15 / AC-WORKER、AC-COMPARE |
| INV-14 | 固定 baseline は明示操作だけで更新する。CorrectnessFailure、Incompatible、Cancelled、NotRun、Interrupted、Skipped を Passed に読み替えない | S-14 / AC-VERIFY |
| INV-15 | Verification の CLI と UI は同じ計画・実行・比較モデルを使う。画面独自の合否や非公開の数値補正を作らない | S-14 / AC-UI |
| INV-16 | Persisted と Tsavorite の独立性、低位 Execution と UI/Verification の依存方向を守る。出荷 runtime に検証 DB や UI を混ぜない | S-02、S-15 / AC-PACKAGE |
| INV-17 | 共有ソースの `.projitems`、同階層 `.shproj`、GUID、Seamless.slnx 登録を一致させる。軽量 solution は生成 script を正本とする | S-01、S-15 / AC-PROJECT |
| INV-18 | 既存の dirty 変更・ユーザーデータ・他者プロセスを保全する。現在の観測が過去の記録と矛盾すれば現在の根拠で状態を訂正する | S-01、S-16 / AC-BASELINE |

## 禁止事項

| ID | やってはいけない変更・回避 |
|---|---|
| NG-01 | 例外を握り潰す、固定値を返す、待機や再試行だけを増やす、診断を隠す、失敗テストを除外して修正完了とする |
| NG-02 | 全件 `List` / `Dictionary` / `ToArray` 化で資源予算を迂回する。入力規模を無断で小さくし、元の規模での成功に見せる |
| NG-03 | 型・SQL・Codec・順序・意味論が未対応のときに別の意味へ暗黙変換する。強制 intercept の未対応を System.Linq への黙示転送で隠す |
| NG-04 | アプリ起動、schema version 増加、自動削除・自動 migration によって既存ローカル DB を変更する。必要な変更は明示 backup と対象 DB への直接適用で行う |
| NG-05 | 未保存 Store の状態、未 commit DB 操作、checkpoint 前 WAL を誤って確定・破棄する |
| NG-06 | 古い Worker / DLL / 生成コードの結果を現在の変更の証拠にする。UI Worker と別の Headless run を同一試行として混ぜる |
| NG-07 | baseline の自動更新、入力 hash や Semantics の差の無視、旧固定 run ID の新契約への持込みを行う |
| NG-08 | 所有不明の変更を reset / clean / checkout で戻す。無関係な dirty ファイルを一括 stage/commit する。既存プロセスを一括停止する |
| NG-09 | `.g.cs` 等の生成成果物のみを直して generator を放置する。共有ソースを solution の loose file だけで公開する |
| NG-10 | SQL 解析・import 成功を routine・trigger・全制約の実行互換と報告する。テストの存在・少数成功を全回帰の合格と報告する |
| NG-11 | 不具合修復と無関係な命名変更・構造再設計・大量整形・保存形式変更を混ぜる |
| NG-12 | 未回答の互換性・製品範囲を承認済みとして扱う。歴史的失敗を、再現なしに現行失敗や解決済みと断定する |

## 前提条件

- 対象はローカル `G:\GitHub\Seamless`。`Work/qa/wt-head` のような比較用 checkout を現行の正本にしない。
- SDK は `global.json` の `10.0.100`、`rollForward=latestFeature` に従う。実行時には実際の `dotnet --info` を保存する。すべての project が同じ TFM とは限らない。
- FLinq generator は現行 csproj では `netstandard2.0`。runtime の多くは `net10.0`。Windows UI の TFM は project ごとに確認する。古い仕様の TFM をコマンドへ転記しない。
- Windows UI の検証には MAUI workload と実際の desktop session が必要。Visual Studio の最新判定は CLI ビルドだけで受け入れ済みにしない。
- SQL Server integration は接続先、専用 fixture、読み取り/取込権限が準備されたときに行う。秘密情報は文書・ログ・manifest に保存しない。
- 機能テストは隔離した一時 DB/Store を使い、既存の `Work/UI`、`Work/Verification`、`Test/Work` を失敗時に自動削除しない。
- 性能評価には同一 CPU/OS/runtime/構成/入力/測定方法を用い、測定中の他プロセスの影響を記録する。数値が揃わなければ比較を延期する。
- 復旧・保存形式変更前には停止可能な writer、整合した backup、検証済みの戻し方を用意する。
- 共通理解は Q-01 以降の製品判断と根拠の記録で進める。コードで判定できる事項は質問せず調査する。

## 今回の変更範囲 / スコープ

今回の成果物は `AtrasReverseEnginnering/*.md`。対象システムの全体を計画の対象とするが、ソース・テスト・CI・既存仕様・QA state・DB を変更する作業はこの文書作成に含めない。既存 dirty 2 ファイルは保全する。文書内リンク・内容の整合と、作成ファイルの差分を確認する。

将来のコード変更範囲は [Phase 計画](03_Phases.md) の各スコープに限定し、依存先に修正が必要なら理由と範囲を先に記録する。Phase の一般的な rollback 手順は [復旧計画](04_Acceptance.md) に従う。

## 非対象 / Out of Scope

- 今回のコード実装、DB migration、データ削除、NuGet 公開、Git commit/push、外部サービス設定。
- 合意のない旧 API 全削除、保存形式の破壊的変更、既存履歴 DB の統合・全変換。
- 全 SQL Server 方言・全組込み関数・全 trigger・任意の動的 SQL の完全互換。
- 新しいストレージ engine、分散 DB、複数 node transaction、クラウド同期、Web/モバイル UI の新設。
- .NET/MAUI/Roslyn/Tsavorite の一斉 upgrade。現行依存の不具合修復に必要な更新は根拠を添え別 task とする。
- UI の全面 redesign、機能と無関係な rename、大規模なコード整形。
- OS 強制停止・電源断・ディスク故障への無条件保証。試験していない範囲は未検証として残す。

## 共通の完了条件

対象の外部挙動・失敗時挙動・重要境界が再現可能で、変更前後の証跡があり、必要な回帰が通り、未対応境界が明示され、コード・テスト・現行仕様が矛盾しないこと。新規の未説明な警告・エラー・回帰を残さない。既知の失敗を残せるのは当該 Phase の非対象として理由・影響・後続 task を明示し、その Phase の契約を阻害しない場合だけである。
