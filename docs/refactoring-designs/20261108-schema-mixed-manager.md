# スキーマ混在とGUI作成・実行検証

## 合意と目的

2026年11月8日の承認済み計画に基づく。DB直下と名前付きスキーマ配下にテーブル・ビュー・関数を共存させる。新規DBは直下から開始し、既存オブジェクトを保持してスキーマを追加できる。従来のDB単位の混在禁止を置き換える。

同じ所属先の名前重複と、DB直下のオブジェクト名・スキーマ名の衝突を拒否する。異なる所属先の同名を許可する。C#は `container.Items` と `container.dbo0.Items`、SQLは `Items` と `dbo0.Items` を区別する。

SQLのスキーマ省略名の探し方は、2026年11月13日の決定で方言の設定(`SqlSchemaResolution`)に移した。既定は T-SQL(SQL Server と同じ)で、読むときは dbo を先に探し、無ければ直下を探す。作るときは dbo に作る。従来の「直下だけを探す」振る舞いは `RootOnly` として残し、カタログ(`DatabaseRegistry.SchemaResolution` など)で選ぶ。C# のネイティブ宣言は置き場を直接書く。

## 現在の実装と変更順序

生成器の混在禁止を撤廃し、所属先ごとの重複と生成・継承メンバーとの衝突を診断する。管理カタログはスキーマと完全パスを保持する。DatabaseDesignerへ空スキーマとビュー・関数の編集を追加し、Browser Managerの作成先をツリー上の操作対象から確定する。

1. 混在生成・名前衝突・空スキーマとSQL名前解決を近接試験で確認する。
2. 共通定義編集へスキーマ、ビュー、関数を接続する。関数のメニューとフォームは一種類とし、引数とC#戻り型を指定する。列挙を返す場合は結果列も設定できる。既存ランタイムのScalarFunction・TableFunctionへの接続は戻り型から選ぶ。処理本体はC#編集とし、再編集で末尾のコメントまで保持する。
3. 共通コマンドからDB・スキーマ・種類別の作成先を確定する。フォームからスキーマ入力を外し、パンくずへ表示する。空スキーマをソース定義として保持する。
4. 公開オブジェクトの結果表示・引数付き実行を、既存の認可・取引・資源制限へ接続する。
5. 隔離DBをGUI作成し、開発者SDKの成果物を取り込み、検証・明示適用・データ確認・再適用・再起動まで確認する。

### 名前入力の追加合意

スキーマの作成メニューを選ぶと、対象DBの左ツリー内に空の名前入力を表示してフォーカスする。Enterまたはフォーカス移動で名前を確定し、Escapeまたは空欄・空白のみで取り消す。確定は対象DBの下書きへ反映し、右のスキーマ作成カードは共通のツリー内入力へ統合する。DB新規作成も同じ入力部品と確定・取消規則を使う。Enterによる処理中のblurと、Escape後のblurで重複実行しない。

DB名の編集場所は、SSMSを参考に左ツリーのDB行一か所へ統一する。DB行のコンテキストメニューの「名前の変更」は、その行を名前入力へ切り替える。右タイトルと文書タブは変更後の名前を表示する。以前の右タイトルから直接編集する要求は、この追加合意で置き換える。Enterまたはフォーカス移動で確定し、Escapeまたは空欄・空白のみで取り消す。

変更対象は接続に使うDB名とする。現在名をDefinitionVersion.Databaseへ分離し、世代ごとのDefinitionと成果物の整合を検証する。専用APIと左ツリー編集を実装し、名前変更後の既存行・ビュー・関数をWindowsブラウザーで確認した。最終候補のDebug／Release各52件とWindowsブラウザー16項目が成功した。SchemaUiVerifiedへの変更後、同じ候補の正常再起動で新名と公開v4の既存行を確認した。設計・結果・制約は [DB名変更](20261108-manager-database-rename.md) に集約する。

名前入力の近接確認では、共通部品のDebugビルドが警告0・エラー0、入力・スキーマ作成・起動・破棄の19件が成功した。Enter・blur、Escape後のblur、空欄・空白、処理中の重複、エラー後の再入力、作成先DBの保持、同名スキーマ拒否を確認した。試験コードのDatabaseDesignerが製品型と衝突した初回コンパイル失敗を、製品型への別名参照で修正して再実行した。証跡は `F:/Seamless/manager-names-20261108/components-build`、`names-tests2`、`test-results2/names.trx`。この初回19件にはDB名変更を含まない。その後、DB名変更を含む最終52件をDebug／Releaseで確認し、共通入力の実ブラウザー操作を追加した。詳細はDB名変更の確認結果に従う。

## 検証対象

Windowsブラウザーで `SchemaUiDemo` を作成する。直下・dbo0・dbo1に各2テーブル（Items、Orders）、2ビュー（ItemList、OrderTotals）、2関数（OrderAmount、FindItems）を配置する。各場所で異なるデータを使い、誤参照を結果比較で検出する。スカラーとテーブル値、射影と結合・計算を含める。

生成プロジェクトをDebug・Releaseで実ビルドする。共有DLLと取り込み成果物の対応、実際の適用経路を照合する。GUIはDOMの文字列・ラベル・値で操作し、画像を表示証拠として保存する。既存DBと未コミット変更を保護し、隔離領域を使用する。

## 受入条件と残件

- 混在宣言の生成・コンパイル、同名の所属先別分離、同一所属先の衝突拒否、空スキーマの保存・復元。
- GUIで全18オブジェクトを作成し、保存・再読込後に所属と本体が一致する。
- Debug・ReleaseのSDKビルドとGUIからの成果物取り込み・検証・明示適用。
- 行投入・更新、全ビュー・関数の実行結果とC#・SQLアクセスの照合。
- 再ビルド・再適用で既存行を保持し、サーバー再起動後も結果が一致する。

既知の復旧時ロックとRoslynメモリ不足を、独立した全体受入のゲートとして引き継ぐ。aih acceptは現行CLI未実装であり、既存のリポジトリ試験入口を使用する。

## 現在の確認結果

今回のGUI受入はWindows上のBrowser Managerで実施した。隔離管理領域は `F:/Seamless/mui0`、SDKプロジェクトと証跡のルートは `F:/Seamless/schema-20261108`。既存運用DBと無関係の未コミット変更を保持した。SDKはテスト用0.0.0パッケージを `sdk-feed` のローカルNuGetフィードから参照し、Seamless本体へのProjectReferenceを使用していない。

| 工程 | 確認結果 | 証跡 |
|---|---|---|
| 生成器 | 近接41件成功。直下・dbo・dbo0・dbo1の同名分離、所属内衝突、空スキーマと宣言ビューの共存 | `green-views` |
| SQLの共通バインダー | ScriptDomと基本パーサーの省略名・明示名・暗黙dbo探索拒否が成功 | `sql-green` より前の共通バインダー記録 |
| Gateway SQL | 直下とdboの同名テーブルのSELECT・更新・メタデータ分離、ランタイムDDL・保存・再オープンの2件成功 | `sql-green-results/sql.trx` |
| GUI作成 | スキーマ2個、テーブル6個、ビュー6個、関数6個を通常フォームで作成。下書き保存・再読込・末尾コメント保持・専用プロジェクト保存を確認 | `schema-manager-18-objects-before-reload-20261108.txt`、`schema-manager-project-saved-20261108.png` |
| 共通操作 | DB・スキーマの＋、一項目の関数メニュー、列の上下挿入、検索と上下キー・Enterによる削除を確認。引数・結果列も共通部品を使用 | `schema-manager-column-menu-20261108.png` |
| SDKビルド | 実NuGetパッケージ参照のDebug・Releaseが警告0・エラー0。関数本体を変更した最終ソースも両構成で成功 | `sdk-v4-debug`、`sdk-v4-release` |
| 初回適用 | DLL検査・取り込み・プレビュー・明示適用でv2を公開。6表へ計12行を追加し、各表の1行を更新・再読込。全6ビュー・6関数の結果一致 | `schema-manager-sdk-applied-20261108.png`、`schema-manager-query-results-20261108.json` |
| 再適用 | 同名のSDK DLLで直下OrderAmountの本体を変更し、GUIでv3・v4へ公開。v4は画面再読込なしで全12問い合わせが一致 | `schema-manager-v4-results-20261108.json` |
| 再起動 | v4をstop-fileで正常終了して再開し、定義と全12問い合わせの一致を確認。以前のv3取消後復旧の成功と区別する | `gui-server9/receipt.json`、`schema-manager-v4-restart-results-20261108.json` |
| 実SDKのC#とSQL | 正常停止後のv4保存コピーを同じSDK DLLで開き、12行・6ビュー・3関数、所属先別SQL結合を照合。直下関数46、dbo0関数84、dbo1関数124 | `sdk-v4-access/stdout.log` |
| 共有依存と成果物 | パッケージとホストの共有11 DLLのSHA256を照合。公開v4のmodule.dllとSDK Debug成果物のSHA256が一致 | `sdk-package-runtime-hashes.json`、`v4-artifact-identity.json` |
| 最終回帰・版の照合 | 同じ対象でDebug・Release各38件成功。変更7製品プロジェクトのReleaseビルドは警告・エラー0。7変更アセンブリのPDB内通常ソース294文書と現在の作業ツリーが一致 | `final-debug-results/manager.trx`、`final-release-results/manager.trx`、`changed-source-provenance.json` |

初回v2のSDK DLLは `658DFD49D69A52FAA92B0CC1F421AAE486A0D753031F048131D21308636E40AA`。変更後v3のSDK DLLと公開module.dllはともに `CDEC3336E921E7B43E0A4B1405BF21E91D1AF3408C1F4ED23F8429E9CECB7D95`。v3のbundle hashは `13E9D7DA0053DF8EB85745AF4DEDFB5B96B33D8DA5FC9FC8DF270C9B1CB09F84`。DLL本体とソース・依存を含むbundleの識別を分けて記録する。

最終v4のSDK DLLと公開module.dllはともに `3771ACDE057CC4C297EAF76D3BB504A3F0958577D2C3F6791FDFAC2752C13569`、bundle hashは `F2318ECD8513941C62F3B424644E1FB45EC01C46CBA6DF35B0930B4E026B32C5`。

## 接続時に修正した問題

- 処理本体の末尾に改行なしの行コメントを置くと、閉じ波括弧がコメントへ取り込まれた。本体を改行で囲んで構文検査し、本文のコメントを保持するよう修正した。変更前後の同じ近接試験で確認した。
- ISet戻り型のビューをアナライザーが情報消失と誤判定した。型が順序なし契約を保持する戻り値・引数・フィールド・プロパティを認識する。既存試験ソースを使う隔離ハーネスで旧DLLは追加1項目失敗、修正DLLは5項目成功。Benchmark.Dynamic.Tests全体の合格とは分ける。
- プロジェクトの自動同期中に利用者の操作を捨て、診断も自動同期で上書きしていた。利用者操作は順に実行し、自動同期の正常終了では手動操作の案内を保持する。画面破棄で待機操作を取り消す。実画面のr5・r6への下書き同期で確認した。
- SDKライブラリの出力に依存DLLが揃わなかった。生成csprojへCopyLocalLockFileAssembliesを加え、実プロジェクトも同じ設定で依存19 DLLを出力した。生成テンプレートの近接試験も接続した。
- 同名SDK DLLの再適用で、旧型と新型を同じ移行ヘルパーへ束縛してInvalidCastExceptionが発生した。[同名SDK DLLの世代間移行](20261108-same-name-sdk-migration.md) の読み出し・書き込み分離で修正した。移行8条件と既存8条件のDebug16件成功、実GUIのv3公開も確認した。
- 再適用後の初回読み取りで、Tsavoriteのチェックポイント復旧がBlazor同期コンテキストを捕捉したまま同期待機し、画面を止めた。dotnet-stackでCollectionStore.LoadからRendererSynchronizationContextへの待機スタックを取得した。ConcurrentManagementOperationsの非同期入口もTask.Runで配送し、同期的なDB処理開始を画面コンテキストから離す。近接試験は変更前に非nullコンテキストを検出し、修正後はnull。実復旧後の12問い合わせも完了した。組み込み同期の専用DB実行スレッドには変更を加えていない。

公開済みプロジェクトを再び開いた後、保存基準v2・r6を公開v3へ整列し、GUIのr7の変更を3方向統合で保持した。同期前の全ソースと作業記録を `before-v4-source` へバックアップし、最終のファイル・表示・DLL検査がすべてSum()+2mで一致した。サーバーなしの保存コピー検証は、所有サーバーの終了確認後に単独実行した。

最終のWindows画面でTab・Enterから列メニューを開き、Escapeで閉じ、Titleの＋から下へ挿入して列順Id・Title・Column・UnitPriceを確認した。試し編集は取消し、公開v4のまま保持した。画像は `schema-manager-final-column-menu-20261108.png`。

今回の範囲別受入結果は `acceptance-summary.json` に集約した。公開5製品DLLと成功したDebug試験の実行DLLのハッシュが一致する。ソース照合は変更7アセンブリに限定し、他のアセンブリ・仮想生成ソース・NuGet由来ソースの照合へ広げない。

検証用の起動入口は `dotnet F:\Seamless\schema-20261108\gui-runtime7\Seamless.Management.Server.dll manager --root F:\Seamless\mui0`。稼働中サーバーの所有者IPCから起動セッションを発行する。サーバー本体は同じruntimeの `serve --config F:\Seamless\schema-20261108\gui-config.json --port 0 --manager-port 58620` で再開できる。既存の所有プロセスの終了を確認してから使う。今回のGUIはPID62140・UTC開始2026-11-07T19:29:08.9187042Z、Webは127.0.0.1の58620番で確認し、利用者が触れるよう検証ブラウザーを残した。2時間の検証用ランナーの寿命がある。

## 残る範囲と制約

- GUIで関数の作成項目を一つにし、戻り値で既存の実行ラッパーを選ぶ。フォームの列挙戻り値の検出は現在IEnumerable<T>を対象とする。配列や任意の独自列挙型を同じフォームで編集する拡張は未対応。
- 今回の18オブジェクトは共有型の列で再適用を確認した。追加実装によりDB定義DLL固有の列値も対応付けて移行する。[DLL世代間の列値移行](20261108-migration-record-values.md) に型と保存プロファイルごとの確認範囲を記載する。collectibleコンテキストの回収をGC後の弱参照で実測する試験は未実行。
- SQLの直接照合はテーブルと結合を対象とした。C#で宣言したビュー・関数をGatewayのSQLオブジェクトへ自動登録する追加経路の受入は含めない。ブラウザーの全ビュー・関数は共通管理APIから実行済み。
- スキーマを追加した後の改名・削除・ドラッグによる所属変更、外部IDE実操作、Linux・タッチ端末の今回候補の実行確認は未実行。
- メモリ圧迫時のGUI新規DB作成失敗と、既知のPublicationCrash共有違反は、配布・全体受入のゲートとして保持する。今回の成功で解決へ移さない。最初のDB作成失敗時はマシン使用率89〜91%、発生箇所と因果関係は未確定。メモリ回復後に同じ隔離インスタンスの正常系が成功した。
- aih acceptは現行CLIに未実装のため、repoのInvoke-LocalProcessと通常dotnet試験を使用した。
