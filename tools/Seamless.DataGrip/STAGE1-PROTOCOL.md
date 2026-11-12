# 段階1の実験通信契約

正式な汎用接続APIの安定版ではない。loopback専用。製品の通常起動へ組み込まない。

フレームは little endian の符号付き32bit長（長さ自身を除く）、1byteの種別、本文。最大8MiB。接続の最初に handshake(version=1001, DB名) を行い、製品名・実験版・接続IDを受け取る。接続前の他操作、再handshake、未対応versionを拒否する。同時接続は最大8、1結果は最大5000行の実証用上限。結果の切断・欠損を成功へ数えない。

要求: 1=handshake、2=metadata(kind,schema pattern,table pattern,catalog,column pattern,table types)、3=SQL(SQL本文,最大行数,型付き引数)、4=取引操作、5=表読取。4・5は試作由来の候補であり段階1の実用化受け入れ対象にしない。取消は要求IDと所有権の設計前なので公開しない。

取引操作のbegin・rollbackは、パラメーター付き更新と取引内読取の回帰試験に使用する。SQL経路ではエンジン内部のセッション実行管理へ委ね、外側で重複するRunを作らない。保存点、commitの確定結果、取消・切断を含む取引契約全体の受け入れは段階2に残る。

段階2のJDBC局所検証では、commit後の再接続読取、保存点へのrollback、接続所有と失効も追加確認した。保存点の利用者名とは別に接続内の取引世代・番号からwire名を作り、他接続のSavepointを送信前に拒否する。確定結果照会、取消の要求ID、切断・障害時の全契約までを受け入れたものではない。

応答: 1=greeting、2=列記述、3=行バッチ、4=結果終了と更新件数、5=要求終了、9=SQLエラー。結果終了と要求終了を分け、複数結果終了後のJDBC `getMoreResults()` は繰り返しても待機しない。SQLエラーにはSQLSTATE・エラー番号を含む。

値タグ: 0=NULL、1=boolean、2=int32（tinyint/smallint含む）、3=int64、4=float32、5=float64、6=decimalの不変カルチャ文字列、7=UTF-8文字列、8=byte列、9=date、10=time、11=datetime、12=datetimeoffset、13=GUID。文字列・byte列の長さはint32、文字列の-1はNULL。日時文字列はISO形式、小数秒7桁を保持する。decimalは現在CLR decimalの範囲であり38桁全域には未対応。CLR型名を送らない。

表・列・主キーは現在の登録済みカタログから取得し、`sys.*`／`INFORMATION_SCHEMA` SQLを生成しない。catalogのNULLは制約なし、空文字はcatalogなしを表す。patternでは `%` と `_` をワイルドカード、`\` をescapeとして扱う。キー等の名前完全一致、UNIQUE・routineの契約網羅、世代ID・行トークン、送受信予算の厳密化は後続段階で扱う。

メモリcodecのbinary/JSON比較は `dotnet DataGrip.Stage1.Server.dll --wire-survey` で実行する。JSON側はタグなしで同じ型契約を満たしておらず、通信・SQL解析・GUI時間も含まない。結果だけで恒久的な通信方式の優劣を決めない。
