# SQL Server 比較用データの準備

`Prepare-Fixture.ps1` は Windows 認証で `Seamless_Comparison_Fixture` を新規作成します。既存の場合はバージョン・列型・キー・初期行を検証し、一致しなければ変更せず失敗します。比較操作で変更した fixture を初期状態へ戻す処理は行いません。同じデータベースを変更する処理を同時に実行しないでください。

TPC-E は通常の再構築から分離して明示的に `Invoke-Prepare-TpcE.ps1` で準備します。起動には PowerShell 7、SQL Server 接続用の子プロセスには Windows PowerShell 5.1 が必要です。EGen 1.14.0 を `-b 1 -c 1000 -t 1000 -f 500 -w 10 -i <flat_in> -o <専用空フォルダー> -x` で実行し、`preflight.py` で全行検査します。ローカルプロセス helper は各子プロセスを最大 8 論理 CPU に制限し、各操作の spec・receipt・stdout/stderr・結果を保存します。生成 spec の SHA-256 と正常終了結果、preflight 結果を `New-GenerationManifest.ps1` に渡してハッシュと件数を保存します。

`Prepare-TpcE.ps1 -FlatDirectory <フォルダー> -ManifestPath <manifest.json>` は Windows 認証で `TPC_E` を新規作成し、ファイルハッシュ、全表の行数、主キーと外部キーを検証します。同じ出力先や SQL Server の対象 DB を変更する別処理と同時に実行しないでください。成功後は manifest の識別子・スキーマ指紋・表件数・制約を照合して再利用し、既存の不一致 DB は変更しません。削除された完成 DB をこのスクリプトは再作成しません。

途中で失敗した場合、未完了の DB やファイルは自動削除・再投入しません。保存された実行結果と SQL Server の DB 状態を確認し、保全すべき内容がないと明確になった場合に、利用者がバックアップを取得したうえで明示的に片付け、新しい空フォルダーと新しい manifest で準備してください。未完了 DB を完成済みとして扱わないでください。
