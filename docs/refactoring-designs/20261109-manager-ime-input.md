# ManagerのIME確定と名前入力

更新日 2026年11月9日

## 対象と操作契約

DB作成・DB名変更・スキーマ作成が共有する`インライン名前入力`と、操作メニューの検索入力を対象とする。

- IME変換中のEnterはIMEの確定に使う。名前の確定や検索したコマンドの実行には使わない。
- IME変換中のEscapeはIMEの取消に使う。名前入力や操作メニューの取消には使わない。
- 変換終了後の通常のEnter・Escapeは、従来の確定・取消を行う。
- 名前入力はフォーカス移動でも確定し、空欄は取消す。

## 共通実装

`interaction.js`の`IME入力を保護`を両方の入力で再利用する。`compositionstart`から`compositionend`までの対象、キーボードイベントの`isComposing`、終了キーとして通知される`keyCode=229`を調べる。終了後の固定待ち時間は設けない。

対象キーのアプリへの伝播を止め、ブラウザーのIME確定・取消の既定動作は維持する。操作メニューはdocumentのcaptureフェーズでキーを処理するため、同じ判定をメニュー自身のキー処理の入口にも適用する。

インライン入力は、既存のJS参照解放処理と初期化・破棄の直列化を使用する。メニューを閉じたときも、IMEイベント購読を既存のメニューイベント購読と一緒に解放する。

## 検証入口と現在の範囲

- C#の既存近接試験は`Tests/Management.Tests/InlineNames.cs`。Enter、フォーカス移動、Escape、空欄、二重確定の防止、名前拒否後の訂正、対象DBの保持を扱う。今回の候補でWindows Debugの11ケースが成功した。証拠は`F:/Seamless/visualization-20261109-0e63c1c7/near-debug-r5/checks.trx`。同じ実行の合計64ケースと、この入力の11ケースを区別する。
- DOM試験は`TestSeamless/ManagerImeDom.mjs`の`verifyManagerIme(page,moduleUrl)`。所有するPlaywrightのページと、同じ候補の`interaction.js`のURLを渡す。IME中のEnter・Escape、`compositionend`直後の229、次の通常キー、入力保持、イベント購読の解放を、Windowsの所有ヘッドレスブラウザーで確認した。`F:/Seamless/visualization-20261109-0e63c1c7/browser-additions-r4/additions.json`に`passed=true`、`physicalImeVerified=false`を記録した。
- DOM試験のイベントは実ブラウザーへ送る合成イベントである。OSのIME、物理キーボード、変換候補の表示は未検証。DOM試験の成功をそれらの合格には含めない。

入力操作の他の境界は作業ツリーの`W02-INPUT`へ残す。
