# Seamlessネイティブ管理

Windows用WPF管理画面と、Windows/Linux向けの独立したTLS管理ホスト。管理経路は構造化要求を使用し、SQL文字列を生成・実行しない。既存のSQL/DataGrip経路とBenchmark MAUIアプリは変更しない。

## プロジェクト

| プロジェクト | 責務 |
|---|---|
| `Seamless.Management` | 通信契約、TLSクライアント、Roslynの編集投影、ソース交換形式、計画のPE読取とDOT出力 |
| `Seamless.Management.Server` | 認証・権限、永続システムDB、ビルド、検証DB、世代公開、CLI |
| `Seamless.Management.UI` | WPF / AvalonEdit / MSAGLによるWindows管理画面 |
| `Tests/Management.Tests` | 単体・実TLS・永続化・移行・公開境界でのプロセス停止試験 |

`Seamless.Management.slnx`から関連プロジェクトを開ける。既存の管理対象DBへこのホストを自動接続したり、既存の保存形式を起動時に変換したりする機能は持たない。新しい管理領域でDB定義を明示的に公開する。

## 起動

.NET 10 SDK/Runtimeと、Windows GUIには.NET Desktop Runtimeを使用する。TLS証明書は接続するDNS名をSANに持ち、有効期間と信頼チェーンを満たすものを用意する。秘密鍵を持つPFXはサーバー側だけで使用する。

比較環境でのビルド・実行は`TestSeamless/Invoke-LocalProcess.ps1`を通す。作業と実データはD、パッケージキャッシュとログはFとし、合計最大8論理CPUに制限する。

## IDEから同時起動

初めて使う場合は、管理ツールの上部にある「ローカル初期設定」を押し、「はい」を選ぶ。サーバーの保存先は`D:\Seamless\Management\Instance1`が既定で、DB・利用者・定義・履歴を保存する。通常は保存先と起動設定ファイルの既定値を変えず、管理者名と12文字以上のパスワードを入力する。CLIの`setup-local`を明示実行し、新規の空の保存先だけを初期化する。既存DBの自動作成・移行・上書きはしない。

初期設定後に「サーバー起動」を押す。IDEと管理画面からのローカル起動は`--local-ipc`を有効にし、同じOS利用者のNamed Pipeへログイン操作なしで接続する。サーバー・クライアント双方の`CurrentUserOnly`でWindows利用者と昇格レベルを照合する。起動設定の`LocalUser`に対応するSeamless利用者の有効状態とDB権限を毎回確認する。旧設定で項目が省略されている場合、明示的なローカルデバッグ起動の既定は`admin`。別名の利用者なら設定へ`LocalUser`を追加する。TCPはTLSとパスワード認証を維持する。このボタンで起動したサーバーには管理画面を閉じる際に正常停止を要求する。IDEで別途起動したサーバーには停止要求を出さない。

IDEの両プロジェクトは`--workspace-config`により、ソリューション直下の`seamless-management.local.json`を共有する。このファイルはGit管理対象外で、保存先・証明書パス・待受設定だけを持ち、パスワードは持たない。一度初期設定すれば次回から同時起動できる。未設定の場合、サーバーは初期設定が必要であることを表示して終了し、管理ツールは初期設定ボタンを案内する。プロファイル選択だけで起動成功とは判断せず、接続情報とPID・実際の待受開始を照合する。

ZIPから起動する場合は`serve --config <起動設定ファイル>`とGUIの`--config <同じファイル>`を指定できる。GUIの既定設定先は`D:\Seamless\Management\seamless-management.local.json`。既存の管理領域・PFXを使用する場合は、初期設定の「いいえ」で登録する。既存の環境変数による設定も維持する。

CLIで同じ初期設定を行う場合は、`setup-local --root <新しい保存先> --admin <管理者> --config <起動設定ファイル>`を実行し、パスワードを非表示で2回入力する。環境変数`SEAMLESS_MANAGEMENT_PASSWORD`がある場合はその値を使う。ローカル用の自己署名証明書を同じ保存先に作成し、Windowsでは現在の利用者・SYSTEM・管理者だけに保存先へのアクセスを許可する。Linuxでは所有者だけに許可する。正式なリモート接続には接続先DNS名を含む適切な証明書を用意する。

`Seamless.Management.slnx`または`Seamless.slnx`をVisual Studio 2026以降で開き、起動プロファイル「Windows Server + Seamless Manager」を選択する。.NET 10 SDKを利用するため、Visual Studio 2026以降が必要。VS CodeではC# Dev Kitを使い、実行とデバッグから同名の複合構成を選ぶ。各プロジェクトも個別に起動できる。

サーバーは初期設定で作成した保存先と秘密鍵付きPFXを使用する。環境変数方式を選ぶ場合に限り、IDE起動前に`SEAMLESS_MANAGEMENT_ROOT`、`SEAMLESS_MANAGEMENT_CERTIFICATE`、必要なら`SEAMLESS_MANAGEMENT_CERT_PASSWORD`を設定する。初回設定で共通設定ファイルを作成した場合、これらの環境変数は不要。ループバックで起動したサーバーは保存先に`local-connection.json`と秘密鍵を含まない`local-server.cer`を出力する。管理ツールは共通設定または環境変数から保存先を読み、サーバーのPID・生成時刻・公開証明書のSHA-256を照合して接続候補を自動表示する。起動順が逆でも非同期に待つ。サーバー停止後の情報は稼働中として扱わない。

既定の接続先は`localhost:14336`。公開証明書のSANには`localhost`が必要。証明書・ホスト名の検証は省略しない。管理領域はローカルで信頼できる利用者だけが書き込める場所に置く。管理ツールにはパスワードを毎回入力し、保存しない。手動保存したリモート接続先も選択できる。初期化は下記のCLIの`init`を明示的に実行し、自動初期化しない。

```powershell
# Debugのサーバーと試験をビルド・実行
./tools/management/Invoke-Checks.ps1 -Configuration Debug
# Windows GUIをビルド
./tools/management/Invoke-Checks.ps1 -Configuration Debug -BuildUI
# GUIをビルドし、GUI内の編集・検証・計画表示も通す
./tools/management/Invoke-Checks.ps1 -Configuration Debug -WithUI
# Windows/LinuxサーバーとWindows GUIのRelease配布ZIPをDへ作る
./tools/management/Publish.ps1
# 配布後のファイル照合とWindows CLIの初期化・バックアップ・復元
./tools/management/Test-Package.ps1 -Distribution D:\CodexTemp\SeamlessManagement-20261102\distribution-<id>
```

管理CLIには以下の操作がある。パスワードはプロセスの環境変数から取得し、引数・ログ・接続設定ファイルに保存しない。サーバー起動コマンドも比較環境では上記のプロセスラッパーで起動する。

```text
Seamless.Management.Server init --root D:\Seamless\Instance1 --admin admin
  環境変数: SEAMLESS_MANAGEMENT_PASSWORD（12文字以上）

Seamless.Management.Server serve --root D:\Seamless\Instance1
  --certificate D:\Seamless\Certificates\server.pfx
  --address 127.0.0.1 --port 14336
  環境変数: SEAMLESS_MANAGEMENT_CERT_PASSWORD

Seamless.Management.Server backup --root D:\Seamless\Instance1
  --destination D:\Seamless\Backups\Instance1

Seamless.Management.Server restore --backup D:\Seamless\Backups\Instance1
  --destination D:\Seamless\Restored\Instance1

Seamless.Management.Server reset-password --root D:\Seamless\Instance1
  --user admin --backup D:\Seamless\Backups\BeforePasswordReset
  新しいパスワードを非表示で2回入力（12文字以上）
  自動実行のみ環境変数: SEAMLESS_MANAGEMENT_PASSWORD
```

初期化は空の専用ディレクトリだけを受け付ける。現在の管理保存形式は3。異なる形式の自動更新はしない。`backup`/`restore`は停止した管理領域の独立した空ディレクトリへのコピーで、ファイルロックとリンクを検査する。サーバーを停止し、バックアップを別の領域へ復元してログイン・定義・データを確認してから本番の切り替えを行う。既存のルートを削除して復元するコマンドは用意していない。

`serve`の`--ready-file`はPID・生成時刻・インスタンスID・ポートを出力し、`--stop-file`は指定ファイルの作成で正常停止を要求する。バックグラウンド起動の所有確認に利用できる。

GUIの接続設定にはホスト・ポート・利用者・公開証明書の場所だけを保存する。通常の保存先は利用者のLocalApplicationData、比較環境では`SEAMLESS_MANAGEMENT_HOME`にD上の専用ディレクトリを明示する。証明書欄を空にした場合はOSの信頼ストア、指定した場合は明示した公開ルートによる検証を使い、ホスト名の検証も行う。

## 操作の流れ

接続直後は「概要・オブジェクト」を表示する。中央のDB名または左のツリーのDB／スキーマを選ぶと一覧を取得する。名前・スキーマ完全一致・種類でサーバー側で絞り込み、取得したページだけを表示する。「条件を解除」「続きを取得」「選択対象の定義を開く」を使用できる。ダブルクリックまたはEnterでも定義ソースへ移動する。一覧の選択だけでは問い合わせを実行しない。取得失敗と検索結果なしを区別し、取得済み件数と続きの有無を表示する。

接続設定は接続後に折り畳む。未選択時は属性欄を閉じ、診断が空なら下部の診断欄も閉じる。スクロールバーは内容がはみ出す場合だけ表示する。新規パスワードが12文字未満、または確認欄と不一致の場合、ダイアログを閉じず赤いエラーを表示して入力を保持する。

ログイン後、「稼働DB・スキーマ・オブジェクト」を展開して一覧を段階的に取得する。テーブル・ビュー・関数・ストアドプロシージャ・システムテーブルに対応する。スキーマを持たない宣言は「スキーマなし」に表示する。対象オブジェクトの選択で、列・キー型・キーメンバー・引数・戻り値と、保存されている稼働版ソースの宣言位置を表示する。対応が不明な情報は補完しない。メタデータの取得でDBコンストラクター・getter・関数を実行しない。稼働版のソース／DLL／マニフェストの識別情報を照合し、一覧取得後に管理領域の版が変わった場合は再取得を要求する。

「公開済み定義（稼働版）」と「個人の下書き」は別項目。公開定義の選択時に下書きへ自動置換しない。下書きを再開する場合は下書き一覧を選ぶ。対応するソースがないオブジェクトは「ソース未登録」と表示する。全体のManage権限がある利用者にはシステムDBの実際の構造も表示するが、行データやパスワードハッシュを返さず、通常の編集・適用には接続しない。

パスワードを忘れた場合、別の管理者がログインできれば「利用者」から再設定する。最後の管理者もログインできない場合は対象サーバーを停止し、上記`reset-password`を使う。停止中の排他ロックを保持して明示した退避先へバックアップし、その成功後に既存利用者のハッシュだけを更新する。対象利用者がなければ作成せず失敗する。権限・有効無効状態・定義・データを維持し、復旧履歴を残す。バックアップ先が空でない場合や稼働中は変更しない。環境変数の変更だけで既存パスワードは変わらない。オンラインでのパスワード変更後は以前のセッションも失効する。

1. 接続・ログインし、新規DBまたは公開定義を開く。
2. C#または構造図の選択部分で編集する。GUI変更は対応する文字列範囲だけを書き換える。カスタムコード・コメント・入力途中の内容を保存できる。入力途中は最後の有効な図を「更新前」と表示する。
3. 個人の下書きとして保存し、静的計画を確認する。未公開の下書きも「個人の下書き」の一覧から再開できる。保存・解析・図の選択は問い合わせを実行しない。
4. DB定義から空の検証DBを作り、JSONで明示的にデータを投入する。問い合わせの下書きは検証DBでだけ実行できる。既存の個人用検証DBの選択・破棄にも対応する。
5. 差分・依存問い合わせの再コンパイル結果・直近の検証結果・稼働版を確認して明示適用する。未検証・検証失敗は警告として表示し、適用可否の最終判断は信頼する開発者が行う。構文・型・移行・制約エラーは適用を拒否する。
6. 切断や応答喪失では、表示された操作IDで結果を照会する。GUIは直近のIDを接続設定領域に保存する。取消要求の受付を取消完了とは表示しない。

権限はView、Execute、Edit、Validate、Apply、Manageを対象DBごとに設定する。利用者の作成・権限変更には全体のManage権限が必要。一般利用者は許可された公開定義と計画を閲覧し、公開済み問い合わせを実行する。自作C#は信頼する開発者に限る。追加アセンブリ、外部I/O、プロセス、リフレクション、dynamic、取引・保存先・DB寿命の直接制御などはコンパイル前の検査で拒否する。この検査は任意の悪意あるコードのサンドボックスではない。

問い合わせの入口は一意なpublic staticメソッド。`ManagementExecutionContext`から束縛されたDBを取得する。検証実行では稼働DBを名前指定で取得できない。`CancellationToken`による協調的な取消を利用できる。終わらない任意計算を強制中断する隔離実行環境は提供しない。

データを更新する問い合わせは定義の`WritesData`を明示する。読み取り専用定義は読取りセッション、更新定義は取引内で実行する。例外・取消・更新結果の上限超過・列挙途中の破棄では確定しない。結果は最大10,000行、標準1,000行。読み取り結果は上限までを返す。通常GUIは稼働DBの更新問い合わせを実行する前に確認する。

パスワードだけを変更する場合は「パスワード再設定」を使用する。対象利用者の複数DBに対する権限と有効・無効状態を保ち、変更後にその利用者のセッションを失効させる。自身のパスワードを変更した場合は、新しい値でログインし直す。

## 永続化と公開

アカウントのパスワードはASP.NET Core IdentityのPasswordHasherでハッシュ化する。利用者、権限、個人下書き、不変の共有版、ビルド・計画、検証結果、適用ID、履歴はSeamlessの永続取引テーブルへ保存する。定義・履歴のページ取得は永続した連番を使い、全件をメモリへ読み込まない。一覧は軽量な識別情報を返し、ソースは開く対象だけを取得する。大きなソース・計画は同じ取引内の64K文字チャンクへ分割し、長さとSHA-256を検証して読み戻す。

DLLとデータ世代は管理領域の`generations`以下に保存する。DLL、ソース、マニフェスト、保存済み計画の識別情報を照合する。DB変更は稼働世代を保持して非公開世代へ行を逐次移す。新旧アセンブリを別名で参照するC#の型付きコピーを生成し、制約を検証してから、主定義・依存する問い合わせ・操作結果・履歴をシステムDBの同じ取引で公開する。操作の対象DBは待機し、無関係なDBは継続できる。

標準移行は、既存列の名前と型を保つ表追加・値型列追加に対応する。依存する問い合わせも新しいスキーマでコンパイルできる必要がある。表・列の削除、型変更、参照型列への初期値指定、任意のデータ変換、スキーマと複数下書きをまとめる適用はまだ未実装で、事前診断で拒否する。古いコードへ戻す操作をデータ復元とは扱わない。データ復元には明示バックアップと停止・復元手順を使用する。

## 静的計画スキーマ1

FLinqの生成に成功したインターセプトメソッドへ`ExecutionPlanAttribute(int schemaVersion,string data)`を出力する。Debug/Release共通。属性はPEメタデータから読み、アセンブリをロード・実行して抽出しない。

- `SourceOperator`: 元のLINQ呼出しと接続、ファイル・文字列範囲。
- `Fragment` / `PipelineStage`: 実際に生成したFragmentと採用した段。選択済みの方式・終端・融合計画を記録し、候補のAggregateCandidateは採用済みとして記録しない。
- `LoweringDecision`: 生成器が記録した方式判断。
- `RuntimeCondition`: 生成本文に実在する分岐条件。どの分岐が実行されたかは含まない。

計画にはメソッド識別子、生成器版とモジュールMVID、元メソッドと生成本文のハッシュ、最適化履歴を保持する。ソース対応がメソッド単位しか分からない場合は`SourceMapping=Method`、不明な場合はUnknownと明示する。方式を推測したり、行数・時間・メモリ・コストを捏造したりしない。元の演算子と物理Fragmentは異なるノードとして表示する。DOTも同じノード・接続を出力する。

既存生成器にはstaticクラスなどインターセプト対象外の制約がある。GUIの雛形は対応する非staticクラスのpublic static入口を使う。計画のない定義は、その旨を表示する。

## 検証の範囲

WindowsのDebug/Releaseビルドと管理契約の試験を実施する。TLS試験では利用者が許可した一時的なWindowsユーザー暗号鍵を作成し、finallyで削除と不存在を検査する。証明書ストアへの登録は行わない。通常のファイルはD/Fへ出力する。

`-WithUI`は自分のアプリ内APIで、ログイン→コード/GUI編集→コメント保持→入力途中→保存→検証DB→結果グリッド→静的計画を通し、画像とJSON証跡をFへ出力する。OSのキー注入、前面化、カーソル操作は使わない。これは内部操作・描画の試験であり、実OS入力・配布パッケージの導入試験とは区別する。

Linux用のCIジョブは`.github/workflows/management.yml`にある。このローカル環境には実行可能なLinuxエンジンがないため、Linux実行結果は未検証。容量不足・全停止点・巨大スキーマ・すべてのFLinq/SQL/DataGrip経路を網羅した製品受け入れも未完了。二次索引、実測計画、NUMA、他プロセス分析、多DB変換・転送はこの管理モジュールには含めない。SQL定義変換・転送・比較は`tools/sql-migration/README.md`の独立した機能を使用する。

### 2026-11-03の検証結果

| 対象 | 結果 | F側の証跡ディレクトリ |
|---|---|---|
| 管理・TLS・GUI内部操作（Debug） | 20件成功 | `draft-discovery-debug-Debug-59708cd7f60c40d5a43bd29ca714dc5b` |
| 管理・TLS・GUI内部操作（Release） | 20件成功 | `draft-discovery-release2-Release-0e3d26b385fd4680bdbf1265de351a79` |
| 既存実行基盤（Release） | 55件成功 | `current-execution-regression-Release-e81c98f1565948abba738843ea0c6f13` |
| 既存DB取引（Release） | 45件成功 | `transactions-regression-Release-d38acd2ca18f469fb58f39769cca4876` |

基点は`F:/CodexTemp/SeamlessManagement-20261102`。各ディレクトリにTRX、標準出力、PID・生成時刻・CPU affinityを含むreceiptを保存する。GUI内部操作にはコード画面・計画画面のPNGとJSON証跡もある。ビルドされたDLLの静的計画の読取、Where/Select/Join/GroupBy/集約/早期終了、列挙子の破棄、コメント保持、下書き競合、読み取り専用定義からの更新拒否、ロール、検証DB破棄の分離、公開前後の強制停止、バックアップ復元、1MiBの定義とチャンク更新の取消、生成アセンブリの回収を含む。

配布ZIPは`Publish.ps1`がDの一意な`distribution-<id>`に作成し、各パッケージにファイル長・SHA-256・対象RIDの`distribution.json`を添付する。自己完結型にはしていないため、サーバーには対象OSの.NET 10 ASP.NET Core Runtime、GUIにはWindows Desktop Runtimeを導入する。Linux向けpublish成功をLinux上の実行成功とは数えない。

確認した配布物は`D:/CodexTemp/SeamlessManagement-20261102/distribution-f845665ec36c4ee2a7ebc52fd88d8607`。Windows/LinuxサーバーとWindows GUIのSHA-256を全件照合し、配布したWindowsサーバーのCLIでhelp/init/backup/restoreを実行した。証跡は`F:/CodexTemp/SeamlessManagement-20261102/package-test-0b4a8afd909946eb94c138a18914c120`。
