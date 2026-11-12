# Browser ManagerのC#同期競合の解消

## 合意と観察

実施順3のソース同期を進める。既存のSourceWorkspaceは競合の保持、三者比較、非競合部分を含む採用候補、追加入力の退避、ディスクハッシュの再照合、ジャーナルによる確定を備え、共通APIとBrowser Managerへ接続した。

## 実装

SourceProjectServiceから既存のInspectConflict・ConflictPreserves・PreserveConflictEditor・ResolveConflictを呼ぶ。所有者向けの共通ソースAPI、同期直接呼び出し・所有者IPC・ブラウザーのアダプターへ接続する。外部認証の管理接続は既存のソース公開範囲を引き継ぐ。

競合発生時は自動同期を停止し、確認ボタンを表示する。ファイルごとに基準・Manager・外部保存を比較し、Manager優先・外部優先の候補または手動編集を明示選択する。取消では入力とディスクを保持する。保存時は競合ID・Managerハッシュ・ディスクハッシュを再確認する。競合記録後の追加入力は既存の復旧用退避を使い、返された退避先を表示する。解消後のDB下書き反映と公開は既存の明示操作を使用する。

## 検証

非競合変更の保持、両側の採用、手動入力、取消、比較後の外部再保存の拒否、追加入力の退避を近接試験で確認する。Compile対象の追加・削除は既存のプロジェクト再評価の契約に従う。競合解消APIでのCompile対象ファイル削除は書き込み前に拒否し、比較画面は内容の採用・編集を提供する。既存52件は変更に無関係な工程の証拠として再利用し、ソース同期に近い試験をまとめて実行する。実IDE操作の成立しない環境を合格には含めない。

## 2026年11月8日の確認範囲

共通APIからSourceWorkspaceへ接続し、所有者IPC・インプロセス・Browser Managerに公開した。OwnerSourceConflictsV1で画面の対応可否を判定する。未解消の競合を含むプロジェクトは保存済みManager入力で開き、自動同期を止めて比較へ進める。

Debug・Releaseの近接各13件が成功した。SourceProjectConflictsの10件とSourceUiOperationsの3件をまとめて実行し、手動内容の再読込、Compileファイル削除の無変更拒否、両候補、コメントと非競合変更の保持、古い比較の拒否、取消、追加入力の退避、未解消状態の再開、実所有者IPCを確認した。証跡は `F:\Seamless\manager-ide-20261108\results-final2\source-final2.trx` と `F:\Seamless\manager-ide-20261108\results-release\source-release.trx`。

WindowsのBrowser Managerで8項目を確認した。.slnxからIdeSampleを表示し、近接試験が保持した競合のある.csprojを通常の開く操作で読み込んだ。基準Value=1・Manager=10・外部=30の表示、未選択時の保存無効化、閉じて再表示、外部優先候補、手動Value=40の保存、フォルダーからの再読込を確認した。サーバー側ファイルのValue=40も照合した。試験用DBは既存の検証領域からオフラインバックアップした別領域を使い、確認後に正規停止した。

比較画面の証跡は `Work/source-conflict-20261108.png`、保存後は `Work/source-conflict-saved-20261108.png`。この時点のブラウザー比較は既存APIが作成した競合を使った。実IDEの編集・保存とブラウザー上での競合発生は次節で追加確認した。IDEビルド、Visual Studio、Linuxの未確認条件を引き継ぐ。

## 実VS Codeとブラウザーによる双方向同期

VS Code 1.145.2のテスト用プロファイル・拡張配置・ワークスペースを `F:\Seamless\ide-live-1108` に分離し、TestSeamless/Invoke-LocalProcess.ps1の共通0xFFで起動した。通常のユーザーIDEへ入力を送らず、開発用拡張からworkspace.applyEditとTextDocument.saveを使用した。Managerは同じホストの別の隔離管理DBで起動し、ブラウザーから通常のソース操作を行った。APIの用途と拡張ホストは公式仕様に従う。[VS Code拡張API](https://code.visualstudio.com/api/references/vscode-api)、[拡張の実行・試験](https://code.visualstudio.com/api/working-with-extensions/testing-extension)

入力はnet10.0の最小SDKプロジェクトで、DatabaseGenerator型、IdeLive名前空間、Rowと実行入口を含む。DB生成パッケージ・業務表・DLL取り込みの受入とは区別する。

1. VS CodeからFirstを11へ編集・保存し、Managerで.slnxを開いて同じファイルと名前空間を表示した。
2. 実IDEから12へ編集・保存し、Managerの通常の自動同期で12が表示されることを確認した。
3. Managerのファイル参照を開いて自動同期が停止する通常状態で、C#入力を20へ編集した。ディスクが12のままであることを確認した後、実IDEで30へ保存した。
4. ブラウザーから同期し、基準12・Manager20・外部30が三者比較へ表示された。比較を閉じてもManager入力20を保持した。
5. 比較を再表示し、保存候補を手動で40へ変更してファイルへ保存した。実VS Codeの文書とディスクが40へ揃い、文書が未変更状態になることを確認した。
6. ブラウザーを再読込し、フォルダーから開き直して40・IdeLive・コメントを確認した。.csprojからの表示も確認した。

ビルドには環境上の不足が残った。新しいVS Codeプロファイルはworkspace.isTrustedがfalseで、初回の編集・保存後のtasks.fetchTasksが返らなかった。取得待ちに期限を設けていなかった試験側の問題を記録し、自分のIDEだけを取消経路で停止した。未信頼状態と取得停止の因果は断定しない。改訂した試験では未信頼状態を先に検査してタスクをUnavailableとし、信頼・保護設定を変更しない。取得処理にも期限を付けた。SDK restoreの成功とIDEビルドの合格を区別する。Visual Studio 18 Communityは導入済みだが、今回の実操作確認には含めていない。以前のCOM呼出し拒否を現在の不合格と同一視せず、別の再開条件として保持する。

VS Code拡張ホストの返信reply-v2-1.json・reply-v2-2.jsonは保存成功、reply-v2-3.jsonは外部40の読込成功とビルドUnavailableを別に記録する。vscodeの初回はCancelled、vscode-v2と専用Managerのserverは通常終了した。製品のコード変更がないため、既存の両構成13件とソース構成試験を再実行せず、今回の実環境操作の証拠を追加した。

文字列比較は `Work/ide-live-conflict-20261108.json` と `Work/ide-live-results-20261108.json`、画面は `Work/ide-live-conflict-20261108.jpg` と `Work/ide-live-resolved-20261108.jpg`。プロファイル、拡張コード、入力・返信、起動・終了、DLLとソースの対応は `F:\Seamless\ide-live-1108` のverification.jsonへ保存する。
