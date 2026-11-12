# WAL要求前の容量拒否と個別取引の中止

## 観測と原因

管理認可の大きな変更を、通信を使わずに保存する試験でも再現した。`SharedLegacyChanges.EncodeBounded`が既定4 MiBのWALフレームを超えて`InvalidDataException`を投げる。この時点ではjournalへappendしていないが、`TransactionCoordinator.Commit`は共通failureとwriterを停止し、`DatabaseContainer.EndTransaction`もDBを故障状態にする。

管理のJSON文書はチャンク化されるが、過去版と稼働版を一つの取引で保存し、WALのcanonical文字列はUTF-16で記録する。通信のJSON容量とWAL容量は一致しない。上限を引き上げて解決済みとは扱わない。

## 修正境界

従来型journalの要求前・適用前で、共通failureとwriterが健全な場合に限り、その取引をAbortedとして返す。appendを呼ぶ直前と適用を開始する直前に境界を記録する。prepared journal、append要求後の結果不明、耐久確定後や適用開始後の失敗は既存の停止・復旧を維持する。

DB側は当該SharedScopeのIDと一致するAborted例外で、coordinatorも健全な場合だけ、外側の取引も中止と認識する。別取引や適用後の例外を誤って継続可能にしない。中止した一時overlayと予約は既存のスコープ終了で解放する。最近の中止結果の有界な照会と、永続的な確定結果の保持は区別する。

## 検証

上限超過を拒否した後、同じ管理DBインスタンスで旧ポリシーを取得し、小さい変更を確定できることを検査する。再open後に旧版と小さい新変更だけが残ることを確認する。取引の既存試験で、writerの失敗、append受付後の失敗、適用途中の失敗は引き続き停止を要求することを確認する。

大容量の管理変更を複数WALフレームで原子的に確定する対応は別の残作業とする。保存形式を起動時に自動変換せず、既存DBの再初期化も行わない。

## 確認結果

Windows Debugの最小再現は修正後に合格し、取引基盤の既存試験はDebug/Release各166件合格、失敗・スキップなし。耐久確定後の適用失敗・後処理失敗を含む従来の停止条件も確認した。証跡は `F:/Seamless/Verification/amr655c1996/result-Debug/authorization.trx` と `F:/Seamless/Verification/tcrc21cde8e` の `result-Debug/authorization.trx`・`result-Release/authorization.trx`。この結果は管理API全体や最終配布版の受入を代替しない。
