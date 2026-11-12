# Manager起動時の個人下書き照会

## 観測した問題

別の利用者が公開したDBをServer＋BrowserManagerで開くと、現在の利用者の個人下書きが存在しない。共通画面は `GetDraftAsync` のNotFoundをcatchして公開定義を表示するが、デバッガーにはそのCLR例外が通知される。実画面コンポーネントの起動試験で、対象例外のFirstChance通知4回を再現した。

## 実装方針

下書きの有無を調べる `FindDraftAsync` を共通操作へ追加し、存在しない場合はnullを返す。IPCは `DraftLookup` オブジェクトへnullableな下書きを包んで返す。認証・認可・版指定の検査を既存の共通サービスへ集約する。

画面起動とソースから下書きを保存する際の存在確認にこの経路を使う。下書きを必須とする取得・ビルド・適用は `GetDraft` の契約を引き継ぐ。保存データと取引形式への変更は不要である。

個人下書きがある場合も、取り込み済みDLLが未選択であることは通常状態である。この存在確認に `FindImportedSourceBuildAsync` と `SourceBuildLookup` を追加し、未取り込み・束縛の失効ではnullを返す。指定した下書き版の競合、認証失効、権限不足は従来のエラーを返す。サーバー側ファイルへ関わる照会は所有者専用IPC・所有者ブラウザーに限定する。

## 確認条件

- 他の利用者が公開したDBを、個人下書きなしで開ける。
- 共通画面の起動中に、対象のNotFoundのFirstChance通知が0回となる。
- 組み込み・同居・所有者IPCで、下書きなしと下書きありを取得できる。
- 他の利用者の下書きを返さず、認証失効・権限不足・必須取得時のエラーを検証する。
- Server＋BrowserManagerの起動から、ブラウザーで公開定義を表示する。

## 実行確認

修正前の画面起動試験はFirstChance通知4回で失敗した。修正後は実際の `管理ワークスペース` と `ConcurrentManagementOperations` を使い、個人下書きなし・ありの両ケースでNotFoundのFirstChance通知0回を確認した。認証失効、権限変更、他利用者の下書きの隔離、組み込み・所有者IPC・TCPを含む近接18件がDebug・Releaseの両方で成功した。

Debugの正式ソース連携入口は94件成功し、Webホストと生成したプロジェクトのビルドも成功した。Windowsの通常 `serve` とOS所有者IPCから発行したセッションで、個人下書きのない `PublishedDb` の公開版v1がブラウザーに表示された。DOMと画面画像を保存し、ブラウザーのerror・warnログは0件だった。実行体・試験参照・現在の通常ソースは17アセンブリのDLL／PDBで照合した。IDEのデバッガーによる再起動操作は別途未検証であり、FirstChanceの回帰検査とは区別する。

Releaseの正式ソース連携入口は94件中93件成功・1件失敗だった。`NativeRowEditingTests.StaleRowsAndGenerationsCannotOverwriteCurrentData` がRoslynの `MetadataReference.CreateFromFile` でOutOfMemoryExceptionとなった。原因は未確定であり、広範囲Releaseの全件合格には含めない。WebホストのReleaseビルドは警告0・エラー0で成功し、通常ソースの17アセンブリ照合も成功した。同じRelease候補の近接18件は独立プロセスで成功した。

追加されたAIハーネス規約に従い `aih accept` を実行したが、現行CLIにはacceptサブコマンドがなく終了コード2だった。ai-harness経由の受入はBLOCKEDとして扱う。上記の成功は既存のリポジトリ試験入口と実サーバー・ブラウザーによる確認結果である。

証跡は `F:\Seamless\manager-cross-platform-20261107\draft-startup-verification.json` へ集約し、修正前後のTRX・実行記録・DLL／PDB照合・ブラウザーDOM・画像を対応付ける。

利用者の2026年11月8日の起動例外修正指示に基づく。
