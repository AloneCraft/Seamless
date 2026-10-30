# TDS の Unicode PLP 送信

## 背景と根拠

`nvarchar(max)` の長い値を送ると、Microsoft.Data.SqlClient 7.0.0 の `TryReadChars` が受信バッファ残量を基準にコピーし、パケット残量を超えて `_inBytesPacket` が負になる例を実測した。SQL Server と同じクライアントを使った比較では SQL Server の読取は成功し、Gateway の初回読取は失敗した。独立デコーダーによる元の PLP 値と後続列の復元は成功している。MS-TDS は文字データのパケット境界跨ぎを禁止していないため、以下は特定の実クライアントとの相互運用条件であり、プロトコルの一般的要件とはしない。

## 送信契約

- Unicode の PLP 値を偶数バイト長の正の chunk に分割する。4096 バイトの TDS packet にヘッダー 8 バイトと chunk 長 4 バイトを含め、chunk data は最大 4084 バイトとする。
- 各 chunk の長さフィールドとデータ全体を一つの専用 packet に載せる。前後の ROW、total、terminator、次列は別の fragment とし、padding は加えない。UTF-16 surrogate pair が chunk 間に分かれても元のバイト列と順序は保持する。
- 行はこれまでどおり 16 MiB の予算を検査してから全列を serialize し、その後送る。chunk の範囲だけを追加記録し、応答全体を貯めない。NULL と空値の PLP 表現、ANSI・binary と非 PLP の動作は保つ。
- ログイン時は ENVCHANGE で選択 packet size 4096 を通知する。

## 受入

独立 decoder で packet 内の各 chunk、total と終端、値、後続列・後続行を照合する。実 SqlClient の新プロセス初回読取で同期・非同期と SequentialAccess の有無を確認し、2 Unicode 列、空・NULL、境界前後、surrogate pair、後続固定列・後続行を Debug と Release で検証する。既存の ANSI・binary と packet-size 交渉の回帰も実行する。
