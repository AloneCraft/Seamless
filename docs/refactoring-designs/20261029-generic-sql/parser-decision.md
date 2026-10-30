# パーサー比較・採用判断

## 決定と状態
標準OSS、有償任意は合意済み。ScriptDomは移行中の比較基準に限定し、最終製品・比較ツールからの依存除去を目指す。SqlParserCS 0.6.5を複数方言の第一評価候補とする。**SqlParserCS 0.6.5で基本経路の実装・評価を進める。3方言66件の構文/AST/診断検査は成功した。製品での意味解析・実行・パッケージ利用・GUI検証は未完であり、完全移行や全方言互換の合格を意味しない。** GSPはライセンス条件確認前に導入・同梱・購入・問い合わせをしない。

| 評価項目 | ScriptDom 180.102.0 | SqlParserCS 0.6.5 | GSP任意候補 |
|---|---|---|---|
| 方言・範囲 | T-SQL専用、既存資産あり | MySQL/PG/SQLite等を公称。網羅性保証なし | .NET版に複数grammar、公称と実コーパスを区別 |
| AST情報量 | TSqlFragment、token/offset/length | record AST、基本投影/JOIN/CAST等を検査済み。各nodeのsource spanは別途必要 | node/token境界、詳細は版ごと確認 |
| 正確性 | 今回の成功率未測定 | 選択した66件の期待受理/拒否・AST検査が成功。網羅性は未評価 | 未導入・未測定 |
| 方言固有構文 | T-SQLを既存171viewで再評価 | XML/APPLY等の対応は未検証 | .NETとJavaの対応一覧を混同しない |
| 診断 | ParseError一覧、位置 | ParserException行/列を確認。BMP/絵文字/CRLFの固定例を検査済み | 公式error handlingあり、実験なし |
| 速度 | 未測定 | 未測定 | 未測定 |
| メモリ | 未測定 | 未測定 | 未測定 |
| C#利用 | managed .NET NuGet、既存参照 | 固定NuGetのnet8 DLLをPowerShellから実解析。製品net10利用は未検証 | .NET SDK、任意依存 |
| 商用・再配布 | MIT、noticeを保持 | MIT、依存noticeも収集 | 商用、配布条件要契約確認 |
| runtime依存 | 公式NuGet対象frameworkを固定 | net7/net8とObjectPool 8.0.1依存を確認。PowerShellで実解析、consumer復元は未検証 | 評価版/製品版を固定するまで未確定 |
| 保守 | Microsoft OSS | OSS、更新版を固定して確認 | 商用サポート、条件未確定 |
| IR変換 | 全T-SQL層の分離が必要 | 意味解析は自前、source spanを検証 | ASTから独立IRへの変換と権利確認が必要 |

## 公式資料（確認日2026-10-29）
- [ScriptDomソース・MIT](https://github.com/microsoft/SqlScriptDOM)、[固定版NuGet](https://www.nuget.org/packages/Microsoft.SqlServer.TransactSql.ScriptDom/180.102.0)
- [SqlParser-cs公式](https://github.com/TylerBrinks/SqlParser-cs)、[固定安定版0.6.5](https://www.nuget.org/packages/SqlParserCS/0.6.5)。候補を無条件latestへ更新しない。
- [GSP価格](https://www.sqlparser.com/pricing/)、[現行FAQ](https://docs.sqlparser.com/faq/licensing/)、[旧.NET FAQ](https://docs-dotnet.sqlparser.com/faq/licensing/)、[.NET版仕様](https://docs.sqlparser.com/dotnet/)。価格ページと旧FAQのライセンス単位の説明に差があるため、記事だけで契約権利を確定しない。少なくとも再配布は別契約確認が必要。

## 目的補正後の評価範囲
[導入体験仕様](onboarding.md)の基本SQLとGUI初期化を採用ゲートとする。171viewは既存資産の対応状況調査として解析できる範囲だけ記録し、未対応を補完する必須ゲートにしない。以下の171view比較・広範構文は任意の調査手順として残す。

## 実施済みの構文評価
[再実行スクリプト](../../../tools/sql-migration/parser-evaluation.ps1)と[手順・限界](../../../tools/sql-migration/parser-evaluation.md)を保存した。固定DLLのSHAを照合し、SQL Server/MySQL/PostgreSQLの基本構文・CAST・無効入力・診断位置の計66件が成功。結果はF:/CodexTemp/SeamlessSqlOssTrial/repo-results.json、BGO task 2ac607f018fa4762b0e417d657a3cd8e。型と主要プロパティを検査し、AST文字列化だけで合格にしない。

MsSqlDialectがLIMIT、MySQL/PostgreSQL dialectがTOPを受理することを観測した。Seamless側の方言検査が必要であり、parserによる受理をDBの構文・意味論の保証に使わない。CASTの不正値や重複列もbinder/実行時検証と区別する。速度・割当は未測定であり高速性は主張しない。

## 発展的な比較・測定手順
1. D:/CodexTemp/SeamlessSqlP0に一時net10評価projectを作り、上記2 OSS版を固定する。永続製品csprojを追加しない。cache/logはF:/CodexTemp/SeamlessSqlP0、TEMP/TMPとscratchはD。製品参照は変更しない。
2. Tests/Database.Query.Tests/Fixtures/catalog.jsonのtype.Trim() == "V"の全171件を抽出（実値は末尾空白付き"V "）。抽出件数171と全definition非空を検査し、全CREATE VIEW原文を保存、SQL Server方言を指定する。fixture SHAはevidence.jsonに固定。
3. 共通12queryと方言別LIMIT/TOP/OFFSET、CTE、UNION、相関、quoted identifier、コメント、無効SQL、複数文、長文/深いネストをcase ID・期待ラベル付きで追加する。構文として合法な意味不正SQLはparserに拒否を要求せずbinderの試験に分ける。
4. 正しい解析と依存名/投影/スコープ・source位置が期待値を保つかを別々に照合する。既知に失敗するsyntaxは失敗理由を保存する。
5. Releaseでparse結果を全て消費して測る。各caseを2回warmup、7batch×100回を既定にしてelapsed ticks、per-thread allocated bytes、入力byte数、AST消費方法、GC/ランタイム/版/hashを保存。DB実行時間とは比較しない。
6. 現在の比較作業は[直接実行と8論理CPU上限](../20261030-local-cpu-execution.md)に従う。同じmaskを子孫へ継承し、共有調停器を使わない。実行できなければUnavailableとし、測定値0や合格にしない。部分的結果から全方言を保証しない。

## 採用ゲート
MIT/noticeと必要依存が確認できること、必須caseの構造と診断を保持すること、外部ASTを製品共通APIに漏らさないことが必須。T-SQLの基本subsetもOSS側へ移してからScriptDomを除去する。後続の目的補正によりXML等のvendor拡張の維持は必須ではない。SqlParserCSの必要構文/位置情報が不足する場合は任意のAST側表で補完可能かを検証し、原文から情報が復元できない場合は採用を保留する。未検証の速度で採用を正当化しない。

既存cacheのScriptDom nupkgとfixtureのSHAは記録済み。固定SqlParserCSパッケージを取得し、既存DLLによる構文評価を実行済み。評価projectのbuildと製品consumer復元・実行、性能測定は未実施。完全移行の合格には構文評価に加えて製品の実行結果・metadata・依存除去・GUI操作の証跡が必要。

## 製品基本経路の進捗（2026-10-29）

Query製品に固定版SqlParserCS 0.6.5とMIT noticeを追加し、外部ASTから所有構文を経て既存binder/実行演算子へ接続した。`SqlQueryEngine.PrepareBasic` はSQL Server意味論の明示選択入口であり、基本SELECTと基本ビュー定義の評価、GOバッチ分割、未対応option拒否をDebug/Release各4件で確認した（BGO `2c7d3933cd064e67a9c4903655aa1f01`）。その後追加した依存DB世代snapshotの回帰はCPU保留解除待ちである。

通常のPrepare、Gatewayの文dispatch、SQL Server移行処理にはScriptDom依存が残る。OSS基本入口の成功を完全移行やGUI経路のOSS化と説明しない。MySQL/PostgreSQLの解析と実行意味論も区別し、現基本入口は両方言の実行を明示拒否する。consumerパッケージの実生成profile試験は別途Debug/Release各5件で成功したが、最新DDL/metadata修正を含む最終パッケージの試験は未実施である。
