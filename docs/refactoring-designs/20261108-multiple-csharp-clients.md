# 同居Managerと独立C#プロセスの業務更新

## 引き継ぐ契約と対象

サーバー同居型ではDB・管理サービス・Kestrelを同じPIDで所有し、複数の通常C#クライアントを接続経由で受け付ける。問い合わせ内部の並列化は初版後の範囲である。今回のW08代表試験は、公開ManagementApplicationHostとManagementClientの公開クエリRPCを通す。

## 試験の構成

MultipleCSharpClientsの試験用C#実行入口を4つの独立PIDで起動する。各プロセスは証明書を検証するTCP/TLSで接続し、認証を済ませ、接続先インスタンスID・PID・CPU affinityを準備ファイルへ記録する。親は全員の準備を確認してから開始を合図する。各クライアントは5回、異なるキーを追加する公開更新クエリを実行し、その時点の件数を受け取る。

同居Managerの所有者ブラウザー操作アダプターも同じ稼働DBを読み取る。20更新の戻り件数が1〜20と一致し、異なるキーの20行がManagerから見えることを確認する。テストDBは専用の隔離領域に作成し、子プロセスはDBファイルを開かず通信を使う。全プロセスは同じ0xFFの最大8論理CPUを継承する。

別の第5プロセスでは、行を追加した後に大きな結果を列挙する更新クエリを実行し、最初の結果を受け取った時点で列挙を破棄して接続を閉じる。結果を最後まで送信していない更新が取り消され、その後の別接続とManagerからの件数が20であることを確認する。サーバーを通常停止・再オープンし、20行の保持も検査する。

## 実行結果

WindowsのDebug・Releaseで代表試験各1件が成功した。両方とも試験本体は約40秒だった。確認した実体は次のとおり。

| 構成 | サーバーとManagerのPID | 更新クライアントのPID | 途中切断のPID | 最終件数 |
|---|---|---|---|---|
| Debug | 18800 | 56584・20696・64456・68444 | 29656 | 20 |
| Release | 47344 | 71540・54292・23608・24864 | 39148 | 20 |

全更新クライアントのインスタンスIDは、その構成のサーバーと一致した。各プロセスの戻り件数が交互に進み、20回の確定に欠落・重複がないことを確認した。途中切断では結果を1件受け取った後に接続を閉じ、追加したキー9999が最終20行へ含まれないことを確認した。

新規試験の初回ビルドでは同名のテスト型を製品型として参照したコンパイルエラーがあり、完全修飾名へ直した。成功後にCPU affinity取得の対応OSをWindows／Linuxへ明示し、CA1416警告を解消した。最終Debug・Releaseビルドは警告・エラー0件。この最後の変更は対応OSのガードであり、Windowsの試験動作を維持するため、成功済みの実行結果を再利用した。

証跡は `F:\Seamless\multiple-clients-20261108` のdebug2-tests・debug2-results、release-tests・release-results、final-Debug-build・final-Release-build、verification.json。PID・インスタンス・CPU affinity・戻り件数は以下のフィクスチャのclients-evidence.jsonに記録した。

- Debugは `F:\Seamless\clients-1108\clients-fe623bf8d746484d9cb023e25b262f59`。
- Releaseは `F:\Seamless\clients-1108\clients-e6e864dc4e2e4023ac8ccad93f7dfa8a`。

## 次の実装境界

ManagementApplicationHostから同じConcurrentManagementOperationsの要求枠をTCP／IPCへ接続した。ConcurrentCapacityはManagerと通常の管理RPCで共有し、ManagementServerの32接続上限とDBごとのゲートも維持する。実装と追加の受入範囲は [共通要求実行枠](20261108-shared-management-admission.md) へ集約する。

今回の独立プロセスはManagement.Testsの専用実行入口を使用した。正式な事前生成業務クライアント、外部認可・資源上限・停止中・タイムアウト、実デバッガー、Linux・正式配布物の受入はW08・W09・W11・W18へ続ける。実ブラウザーの新規操作は今回行っておらず、同居ManagerのPIDと所有者操作アダプターの結果を確認した。
