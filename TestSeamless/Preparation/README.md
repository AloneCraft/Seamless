# SQL Server 比較用データの準備

`Prepare-Fixture.ps1` は Windows 認証で `Seamless_Comparison_Fixture` を新規作成します。既存の場合はバージョン・列型・キー・初期行を検証し、一致しなければ変更せず失敗します。比較操作で変更した fixture を初期状態へ戻す処理は行いません。 実行時は BGO で workspace S・専用 profile X・SQL Server インスタンス X を取得してください。

TPC-E は通常の再構築から分離して明示的に `Invoke-Prepare-TpcE.ps1` で準備します。SQL Server 接続が許可された Windows PowerShell 5.1 で実行してください。EGen 1.14.0 を `-b 1 -c 1000 -t 1000 -f 500 -w 10 -i <flat_in> -o <専用空フォルダー> -x` で実行し、`preflight.py` に flat フォルダー、`tpce-tables.json`、結果 JSON を渡して全行検査します。生成と検査は BGO の workspace S・専用 profile X・cpu:heavy X を取得して行います。BGO の正常終了 receipt と生成 spec、preflight 結果を `New-GenerationManifest.ps1` に渡してハッシュと件数を保存します。

`Prepare-TpcE.ps1 -FlatDirectory <フォルダー> -ManifestPath <manifest.json>` は Windows 認証で `TPC_E` を新規作成し、ファイルハッシュ、全表の行数、主キーと外部キーを検証します。SQL Server インスタンス X・専用 profile X・workspace S・cpu:heavy X を宣言して BGO から実行してください。成功後は manifest の識別子・スキーマ指紋・表件数・制約を照合して再利用し、既存の不一致 DB は変更しません。削除された完成 DB をこのスクリプトは再作成しません。

途中で失敗した場合、未完了の DB やファイルは自動削除・再投入しません。BGO の実行結果と SQL Server の DB 状態を確認し、保全すべき内容がないと明確になった場合に、利用者がバックアップを取得したうえで明示的に片付け、新しい空フォルダーと新しい manifest で準備してください。未完了 DB を完成済みとして扱わないでください。
