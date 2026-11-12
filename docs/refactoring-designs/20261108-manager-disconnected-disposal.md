# ブラウザー切断時のコンポーネント破棄

## 観測と修正方針

2026年11月8日に利用者がブラウザー終了時のJSDisconnectedExceptionを報告した。管理ワークスペースのDisposeAsyncでは操作オブジェクトだけをcatchし、その後のモジュール破棄は保護していない。操作メニューも同じ構造であり、モジュール破棄で例外が出ると後続の.NET参照やキャンセル資源の解放に到達しない。

切断後のJS参照破棄は完了不能なため、JSDisconnectedExceptionだけを終了状態として扱う。その他のJSエラーは呼び出し元へ伝え、finallyで管理側の参照と寿命を解放する。各コンポーネントの破棄を再実行してもJSの解放を繰り返さない。既存の接続中のイベント解除とモジュール解放を維持する。

MicrosoftのJS相互運用の説明でも、切断後のIJSObjectReference.DisposeAsyncは失敗し得るため、破棄時にJSDisconnectedExceptionを扱う方式が示されている。[公式資料](https://learn.microsoft.com/en-us/aspnet/core/blazor/javascript-interoperability/?view=aspnetcore-10.0#javascript-interop-calls-without-a-circuit)

## 確認条件

- 画面本体と操作メニューで、操作用JS参照またはモジュールが切断されても破棄が完了する。
- 切断以外のJSエラーは伝播し、管理側の資源も解放する。
- 初期化前・正常接続中・切断後・再度の破棄を確認する。
- 実ブラウザーで操作メニューを開いてから閉じ、サーバー側に未処理の破棄例外が出ないことを確認する。

スキーマ混在の承認済み作業を保持し、本不具合の確認後に続行する。

## 確認結果

修正前の試験で5件の失敗を確認した。共通の `スクリプト参照.解放Async` へ操作用参照とモジュールの解放を集約した。管理ワークスペースと操作メニューの.NET参照はfinallyで解放し、二重破棄を防ぐ。切断以外のJS例外は呼び出し元へ伝播する。

修正後は初期化前・正常接続・切断・再破棄・想定外エラー、既存の起動と定義編集を含む13件がDebug・Releaseとも成功した。実ブラウザーの別タブでPublishedDbを開き、操作メニューを表示してタブを閉じた。その後もOS所有者IPCが応答し、隔離サーバーの正規停止が成功した。サーバーの標準エラーは空で、標準出力にJSDisconnectedException・Unhandled exception・fail/critはなかった。IDEのデバッガーによる閉じる操作は未検証であり、処理済み例外のFirstChance通知0回を保証する試験とは区別する。

証跡は `F:\Seamless\schema-20261108` の `red-disposal-results`、`green-disposal-results`、`release-disposal-results`、`disposal-server`、`disposal-after-close`、`manager-disposal-dom-20261108.txt`、`manager-disposal-menu-20261108.png`。既存タブの入力状態を保護し、終了試験専用のタブ17だけを閉じた。
