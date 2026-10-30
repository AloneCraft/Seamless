# 実クライアントの通信検証

## 現在の判定
世代 `20261029T085200772Z-e76f5309`（package `0.0.0-ssms.20261029084842137`）はValidatedとなり稼働中である。AdventureWorks2016とfixtureの計94対象で取り込み全値とTDS全値・metadataが一致し、基本SQL・生成関数・SQL/native比較も成功した。実SSMSの接続初期化はこの版で失敗しており、GUI受け入れは未完了である。値を空文字・切り捨てに置き換えない。

## 修正・確認済み
| 項目 | 観測した原因と変更 | 検証 |
|---|---|---|
| sys.columns.collation_name | 数値列のNULLを返す一方metadataが非NULLだった。NULL属性を修正 | 実SqlClientのNULL/非NULL行・metadata・終端がDebug/Release成功、SystemCatalog回帰各6件成功 |
| 空のPLP値 | total=0の後にゼロchunkとterminatorを重複出力していた。正長chunkだけ出力し終端を1つにした | 修正前nvarchar/varchar/varbinaryの3件失敗。修正後varchar/varbinaryの空/NULL/大値/後続列と行はDebug/Release成功。Unicode PLPは別のchunk配置修正で初回読取も成功 |
| packet-size交渉 | 4096-byte送信を行う一方でENVCHANGE type4が欠落。選択値4096を明示 | 要求512/4096/8000/8192に対する実PacketSizeとraw通知5件がDebug/Release成功。Unicodeのchunk配置は別の契約で検証 |
| IPv6 loopback | IPv4だけの待受から、IPv4とIPv6専用loopbackの同一portへ変更。起動片側失敗は両側解放 | 実SqlClient直接IP・port0・停止/再起動/接続中停止と競合cleanupをDebug/Releaseで確認 |
| PRINT | 既存SQL型評価を使い文字/整数/decimal/NULLを通知として送信 | INFO内容・番号/class/state・SELECTとの順序・ExecuteNonQueryをSQL Server実機とDebug/Releaseで比較 |

## Unicode PLPの相互運用条件
実SqlClient 7.0.0で長いnvarchar(max)を読む際、所有clientのpacket残量が負になり文字コピーで例外になることを観測した。独立decoderの値復元は成功しており、MS-TDSがUnicode packet跨ぎを禁止するとは解釈しない。

送信側は[Unicode PLP送信設計](plp-streaming.md)に従い、Unicode PLPの各正長chunkを最大4084bytesとし、chunk headerとdataを一つのpacketへ収める。これにより文字読取要求をpacket内の値データ範囲に限定する。行を先にserializeする既存の16MiB上限、NULL/空、total/terminator、後続列を保持し、paddingや待ち時間を加えない。chunk範囲のリストは結果内で再利用・行ごとClearし、全応答は蓄積しない。

Debug/Releaseの関連試験は各30/30成功、skip 0。大値、ANSI/binary、NULL/空、2 Unicode列、境界前後、surrogate pair、後続列/行、packet交渉、PRINT、catalog、loopback、SharedV2基礎契約を含む。独立新process初回は同期/非同期×SequentialAccess有無×Debug/Releaseの全8条件で成功した。値は独立decoderとSQL Serverの期待結果で確認している。

証跡はF:/CodexTemp/SeamlessLargeValueAcceptance/Selected-{Debug,Release}.trxおよびPLP-first-*.jsonl。旧失敗・切り分け記録も同成果物フォルダーに保持する。実データ全体は上記Validated世代で別途照合した。この30件を全製品/全GUI合格へ拡大解釈しない。

## 環境依存: localhost
同じprivate Gatewayに127.0.0.1は接続可能、既定native SNIのlocalhostはIPv4/IPv6 listenerへ到達しない。独立した診断で[公式managed networking switch](https://learn.microsoft.com/ja-jp/sql/connect/ado-net/appcontext-switches?view=sql-server-ver17)を接続前に設定した場合は到達した。製品の既定switchは変えていない。

比較環境の既定は利用者が許容した127.0.0.1,14333に明示し、EXEのConnectionStrings__Seamlessによる上書きを維持する。元SQL Serverのlocalhost/Windows認証は元設定を維持する。localhostの制約をIPv6追加で解消したとは報告しない。診断は`Work/TestSeamless/Implementation/tds-localhost-diagnostics/`に保存した。

## 実SSMS
.ssmsslnをSSMS22が拒否したことと、同じ内容の.sln診断コピーで両projectが読めたことを実UIで確認した。正式入口はTestSeamless.Compare.sln。元SQL ServerへのGUI接続（localhost、Windows認証14700F\kazuh、AdventureWorks2016）は利用者が明示承認済みである。未接続を実操作成功に数えない。XML/台帳の回帰25件成功は実SQL実行とは別に扱う。正式.slnの2プロジェクト読み込みは実UIで確認した。

稼働版SSMS初期化の最小再現はSERVERPROPERTY列のInt32→String変換エラーである。SQL別名によって値・型が変化しない修正、SSMS22の実初期化バッチ、sys.databasesの不足列をソース側で検証した。小fixtureのSMO APIはVersion、Edition、DB、Tables、Views、StoredProcedures、UserDefinedFunctionsの7操作が成功し、関連回帰14件はDebug/Releaseで成功した。最新ソースからの新パッケージ・実データ世代・実SSMSへの適用は未実施である。SERVERPROPERTYの生の戻り型は現在基底型であり、SQL Serverのsql_variant完全互換ではない。
