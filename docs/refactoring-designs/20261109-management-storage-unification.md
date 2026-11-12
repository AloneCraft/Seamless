# 管理データと実績履歴のSeamless永続化への統一

更新日 2026年11月9日

## 承認された保存方針

利用者の追加指示により、次を現在有効な設計とする。

- 実績履歴は、期間ごとの独立した`Seamless.Execution.Store`と`Tsavorite.Set`へ保存する。途中の履歴削除・破損があっても、稼働DBとサーバーの通常処理、健全な期間の履歴参照を継続する。
- 利用者、ロール、権限、サーバー設定を含む管理データ全体を、型付き`Seamless.Database.Table<TKey,TRecord>`とTsavoriteの永続化基盤へ統一する。物理コレクションの目標は`Tsavorite.Set`とする。
- 保存方式の変更後も、JSONによる交換とHTMLによる共有・閲覧を提供する。出力時の認可、ソース・リテラルのマスキング、版・対応情報の検証を通す。

本書の実装状況は以下の調査版と近接試験のソースに基づく。期間Store履歴はWindows Debugで保存・反復読取・欠損検査・正常処理の継続を確認した。型付き管理行への移行、SharedV2の物理Set化、最新候補のRelease・Linux・配布受入は継続中である。

## 現在の実保存経路

### 管理領域のTableとJSON payload

`ManagementStore.cs`内の`ManagementDatabase`は`DatabaseContainer`を継承し、`Table<ManagementKey,ManagementRecord> Entries`を持つ。`ManagementRecord`は`Id / Kind / Subject / Json / ChunkCount / JsonLength / PayloadHash`を持ち、利用者や設定等の型別データをJSON文字列へ直列化して格納する。

JSON文字列は64Ki文字単位に分割され、上限は64Mi文字である。分割時の長さ、件数、SHA-256を読取時に検査する。管理キーは`Kind + NUL + Subject`のSHA-256先頭16バイトから生成し、取得した行のKindとSubjectも照合する。

現行の主なpayloadは次のとおり。

| 分類 | 現在の保存先・内容 |
| --- | --- |
| アカウント | `user`へ`UserAccount`。表示名、パスワードハッシュ、有効状態、管理DB権限を含む |
| 共通認可 | `authorization/active`、`authorizationVersion`、`authorizationChange`、`authorizationOperation`へポリシー全文・版・変更履歴・操作結果。主体の有効状態は`authorizationSubjectStatus`と`authorizationSubjectOperation` |
| サーバー設定 | `serverSettings/active`へ設定の版と設定値 |
| DB・クエリ定義 | `active / version / draft / build / validation / validationVersion / lastValidation / sourceBuild / queryAudit`へ定義、個人下書き、成果物対応、検証、問い合わせの監査メタデータ |
| 確定境界と索引 | `header / operation / databaseRename / history / auditOrigin / catalog / catalogHeader`と分割payloadの`chunk` |

管理Tableが存在することと、管理payloadが個別の型付き行へ分離されていることを区別する。現在の利用者・ロール等の保存は、上記の共通Entriesを通る。

### 既に型付きになっている監査

`ManagementAuditDatabase`は、`AuditEvents / AuditEventIds / AuditDays / ReadSummaries / ReadSummaryIds / AuditBatches / AuditState`の七つの型付きTableを持つ。行はMemoryPack対応のレコードであり、監査イベント、重複検出、日付範囲、読取集計、転記状態を保持する。これらは共通EntriesのJSON payloadと分離されている。

### SharedV2の現在値と版付き履歴

管理領域は形式6で`Transactions=SharedV2`と`PreparedTransactionWal=true`を指定し、`TransactionHistory`は未指定のためnullである。`DatabaseContainer.CreateSharedTable`は`CreateTsavorite<TKey,TRecord>`と`TransactionalTable<TKey,TRecord>`を使用し、現在値を型付き`DictionaryStore`へ保存する。最初の管理payload型付き化は、この共通取引と分割WALを維持する。

`TransactionHistory`を明示した版付きプロファイルには、`CreateVersionedParticipant`から`CreateTsavoriteHistory`へ進む経路がある。その`HistoryStore`は`CollectionStore<byte[],byte[]>`で、commit version、削除行、旧版参照、コピー更新を支える。現在のstandalone分割WALは`TransactionHistory`との同時指定を拒否する。定義の`version`行を管理APIで保持することと、取引のMVCCを区別する。

現在の`Set<TKey,TRecord>`は`IRecord<TKey>`を要求する。現行の`ManagementRecord`や監査行へ単純に型名を当てはめても、既存のSharedV2参加者、codec、WAL、checkpointとの互換は成立しない。物理Set化や版付きプロファイルの変更は、管理payloadを型付き行へ分ける段階の後に検証する。

### ファイルとして存在する情報

| 情報 | 現在の形と用途 |
| --- | --- |
| 旧実績履歴 | `QueryExecutionHistoryStore`が独立したJSONセグメントを保存する。期間Store化の比較基準として凍結する |
| 起動と初期識別 | `management.format`、所有者lock、起動設定のroot・証明書・LocalUser、証明書ファイル。DBを開く前に必要な情報を含む |
| 共通Store | `store.json`のcheckpoint manifestと各コレクションのlog・object log・checkpoint |
| 開発ソース同期 | SourceWorkspaceの`.seamless/workspace.json / pending.json`等と、開発者のC#・projectファイル |
| SDK成果物 | DLL、PDB、source、`manifest.json / source-build.json`。管理Table側の版・hash・取り込み対応と照合する |

管理の型付きデータ、起動に必要な記述、開発者が編集するソース、配布成果物を用途別に扱う。期間履歴のデータ本体をSetへ移す場合にも、共通Storeが使用するcheckpoint記述を含めて配置・復旧を検査する。

## 管理の型付き行化で維持する契約

1. `ManagementStore.Write`のgateとSharedV2取引を維持する。行、ヘッダーrevision、監査を一括確定し、Commit要求後のDispose完了を待って認可epochを公開する。
2. 利用者名は既存のFormC・大文字化によるキーを使用し、表示名を保持する。新規同名拒否、取得時SHA stampのCAS、自己管理権限の剥奪拒否を保つ。
3. サーバー設定の`ExpectedVersion`と版の増加、起動引数・保存値・起動設定の優先順位、実行形態で使用できる項目を保つ。設定と履歴を同一取引で確定する。
4. 共通認可のpolicy version・hash CAS、操作IDと版の一意、資源の親子、規則の対象、ロールと割り当ての参照を検査する。保存が確定してから新しい認可を公開する。ロールはGuidで識別されており、今回の調査では名前一意制約を確認していない。型付き化だけを理由に新しい名前一意制約を導入しない。
5. DB名変更の期待旧名、同名、既存の権限名、公開処理との競合、操作IDによる再送結果を保つ。定義、個人下書き、検証、権限、履歴との関係を同じ確定境界へ置く。
6. 監査のEventId再利用判定を保つ。既存のMemoryPack hash、値比較、`DateTimeOffset.EqualsExact`を移行だけで変更しない。正規化・比較・ハッシュは既存の型固定契約に接続し、保存形式の都合で値や参照の意味を変えない。

型付き行化では、アカウント、DB権限、資源、ロール、規則、割り当て、設定、認可版・操作結果等を、それぞれキーと型付き値で表現する。定義・ソース・成果物対応も対象として棚卸しを続け、共通Json列へ移し替えただけの状態を全体完了にしない。

物理Set化は、その後にSharedV2の旧版参照、コピー更新、削除行、確定版公開を満たす形で進める。通常の上書きSetへ短絡して、読取開始時の版や取消・復旧を失わせない。物理レコードとcodecの対応、旧形式の読取、checkpointの扱いは、この段階の実装・近接試験で具体化する。

## 期間別の任意実績Store

- 期間ごとに独立した`Seamless.Execution.Store`を作り、型付き`Tsavorite.Set`へ記録する。物理キーはcollectorのsessionと全体連番で一意にし、payloadへ実行IDと実行内Sequence、対象定義・オブジェクト・実行版の確定状態を保持する。
- consumerがStoreを所有し、DB実行スレッドを要求しない。親のメモリ・ディスク予算、キュー上限、取消と終了時の解放を既存資源管理へ接続する。
- `Store.Open`はフォルダーや`store.json`がない場合に新規扱いする。履歴の読取は、既存期間の識別、形式、必須manifest、checkpoint等を検査してから開く。欠けた期間を空Storeとして再生成しない。
- 削除・破損がある期間は欠落理由と完全性の不足を表示し、健全な期間を列挙する。問題のある期間の新規記録は別の識別子のStoreへ切り替える。管理DB・利用者DBの復旧規則と、任意履歴の欠損許容を分離する。
- 単一期間内の一部破損から全行を救済できることは、現在の観測では保証していない。残存履歴の受入は、まず健全な独立期間を引き続き参照できることから確認する。
- 期間の大きさ、物理codec、保持上限の数値、同一期間内の救済範囲は後続の実装・実測対象とする。計測していない完全性や復旧性能を表示しない。
- 未完了の`writing-*`領域が残っている場合に、新しい領域を増やさず`IncompletePeriod`で書込みだけを停止する。完了期間の読取りとDB操作は継続する。未完領域は自動削除せず、保持量にはその物理ファイルも算入する。安全な自動回収と、native checkpointを含む瞬間最大物理ディスク量の厳密な上限はW11の残件とする。
- 保持件数とは独立した65,537期間の検査上限を設ける。既に保持件数を超えた期間も検査して、新規公開分の一期間を空けてから記録する。検査上限超過や回収失敗は`RetentionFailed`として新しい保存を止め、健全期間の読取りとDB操作を継続する。

## 形式6からの明示移行

既存の`ManagementStoreUpgrade.UpgradePreparedWal`は、停止中の排他取得、明示バックアップ、新しいstageへのexport/import、stageの再openと検査、manifest付きのinstall、失敗時のrollbackを行う。新しい型付き形式への移行も、この方式を基礎として次を実施する。

1. 稼働管理領域の受付・終了を既存の停止規則で処理し、offline lockを取得する。
2. 明示バックアップを完成させてから、新規stageへ旧版を読み出す。
3. キー、版、参照、同名、正常化、hash・stamp、件数と監査を検査して型付き行へ変換する。衝突や不足は対象を示して停止する。
4. stageを閉じて再openし、元の型付き公開契約、認可判定、設定、定義・成果物対応、通常操作を照合する。
5. 中断回復manifestを記録して確定先へ切り替える。失敗時に旧世代を維持するか、明示rollbackで戻せるようにする。

形式7の第1単位を実装し、隔離領域で近接試験を進めている。起動時の自動migrationや、管理領域の自動削除・空作成で移行を代用しない。

### 最初の実装単位

UsersとServerSettingsを形式7の型付きTableへ分離する。形式6は従来のDatabase型とcatalog identityで開き、明示offline移行で新しいstageを構築する。形式7は監査schemaを継承した専用Database型へUsers・UserGrants・ManagedTextParts・ServerSettingsの4表を登録する。キーは既存のManagementKey、正規化済みsubjectとキー衝突の照合を引き継ぐ。

保存行はMemoryPack対応の値型とし、Grantは利用者subjectとordinalで順序付けた子行へ写す。通常のWAL codecがclassを受け付けないため、公開DTOのclassを行へ直接入れず、利用者・設定の各値を保存用structへ写す。Grant配列のnull・空・null要素を表現し、読取りで独立した配列へ戻す。公開generic Readの型付きhot pathと、他のTへの従来JSON投影の互換経路を用意する。

既存の64Mi文字の入力上限に合わせ、長い権限先や保存済みpassword hashは最大32Ki UTF16文字の子行へ分割し、長さ・個数・SHA-256を検査する。分割点のサロゲート対を壊さないよう、物理行はchar配列で保持する。権限件数は上限検査と親予算予約を通して配列化する。通常入力はJSONサイズの安全な上界で検査し、上界が閾値を超えた場合だけ保存せず文字数を数えるJSON writerで元の容量契約を検査する。

移行は既存のbackup・raw転送・監査転送・stage再open・install／rollbackを使う。移行対象のJSON本体とchunkを転送対象から外し、一覧用catalogを保持する。型付き変換では通常Putのcatalog新規登録を通さず、重複のない行を挿入する。ユーザーのProfile stamp、設定版、ヘッダー、定義のversion行・個人下書き・操作結果・監査を照合する。今回の承認範囲は第1単位の実装と隔離試験で、稼働管理領域の移行は含まない。

移行前のEntries実体件数とcatalog経由の変換件数も照合する。catalog行だけが欠けたuserや設定を、転送対象から外してそのまま消失させることを防ぐ検査である。不一致はstageの確定前に停止し、元の形式6を維持する。独立レビューで追加した検査と、形式7の物理コピー・再実行・未知形式は`near-debug-r31/checks.trx`の新4ケースで成功した。関連44件も成功し、新しい固定候補のRelease／Linux確認へ進む。

CLIは`upgrade-managed-types --root <停止中の形式6管理領域> --backup <空の退避先>`と、中断後の`rollback-managed-types-upgrade --root <管理領域>`を追加した。新規initの既定は形式6である。形式7の全物理領域のbackup／restoreは既存CopyLockedを使用する。Entriesだけを転送する旧ExportRawは形式7を明示拒否し、後続の型付き移行で4表を含む転送を実装するまで不完全なexportを作らない。

型付き再転送の次単位では、旧raw転送を保ったまま、形式番号・行種別・長さを持つ有界フレームへEntriesと4表のMemoryPack行を流し、表別件数と全体のhashを末尾manifestで確定する。受入側は未知形式・不正長・キー重複・Grant ordinalと文字列partの不足・hash不一致を拒否し、元のinstanceとheaderを持つ独立stageへ取り込む。stageを閉じて再openし、generic Read・Profile stamp・設定版・監査と原本の一致を検査してから既存の明示installへ渡す。取消・失敗時の現行領域維持、部分フレーム、重複、64Mi文字境界、全4表を含む再転送往復をこの単位の受入条件とする。現在の物理backup／restoreと、後続形式へ移すための論理転送は個別に検証する。

設定の通常APIは`ManagementServerSettings.Validate()`の戻り値を保存し、IPアドレスは`IPAddress.ToString()`、公開起点は`Uri.AbsoluteUri`へ正規化する。先頭0が大量にあるIPやHTTPSポート表記を.NETが受理しても保存する標準形は短くなる。利用者名と正規化subjectは既存の1〜64文字制限を使う。内部のraw Putで検証を通さず作った非標準設定の移行は、通常APIの受入範囲と分けて扱う。

Windows Debugでは`typed-first-r2`の正常移行1件、`r3`のコピー独立性・CAS・一括取消・欠損／hash／異常件数の3件、`r4`の例外rollback4点とstage取消の5件が成功した。`r5`では実プロセスのOldMoved／Committed／Published強制終了と明示復旧の3件、転送拒否を加えた更新1件が成功した。証拠は`F:/Seamless/visualization-20261109-0e63c1c7/typed-first-r*/checks.trx`へ保存した。最初の正常系fixtureはrawのGrants=nullを既存UserProfiles一覧に混ぜて失敗したため、rawのnull往復と通常APIのstamp検査を分けた。元の形式6でもProfileがnull配列を受け付けないことを、保存機能の不具合と区別している。

同DLLの`typed-first-r6`では形式6の205件user paging、設定の共通操作・IPC・CAS、組込HTTPの所有者／CSS、publish entryのstatic-assets manifest選択が4件成功した。最後に移行用catalog走査へordinalごとの取消確認とlong上限のoverflow防止を追加した。`typed-first-r7`はServerが0警告でcompileし、Testsは未更新のUI参照DLLで停止した。UI更新後の`near-debug-r30/checks.trx`で疎なcatalog取消と正常移行を再確認し、関連38件が成功した。第1単位の固有近接ケースは13件となる。

その後の`near-debug-r31`は、通常APIの巨大設定入力の標準化と7への移行・全物理copy・再実行、未知形式拒否、user／settings各catalog欠損の新4件を含む。分割した近接証拠は固有17ケースへ増えた。旧候補の強制終了確認と、最新の移行検査の代表確認を区別し、同一固定候補の全体受入は継続する。

固定候補`candidate-p7-r3`のLinux Releaseでは、`linux-3c20847888c14ac08e034d0afd579bc2/near-tests/typed`の14件と`typed-crash`の3件が同じDLLで成功した。通常移行・長いGrant・stamp・CAS・全体取消・4点の切替例外・catalog欠損・未知形式・形式7の全物理backup／restore・再実行と、OldMoved／Committed／Publishedの実プロセス強制終了を含む。ビルドは0警告・0エラー、固定source1498ファイルの集合とhashは前後一致し、通常利用PID90899も同一だった。検証DBは隔離したLinux home領域を使い、稼働管理領域は移行していない。

Windows Debugの固定R3では、隔離DBだけを明示backupして6→7へ移行した。移行前591ファイルはbackupとhash一致、DB世代・実績／Traceの差分は0だった。形式7を通常openした後のAPI16件・ブラウザーの4問い合わせと計画、利用者一覧・設定読取・Trace復元を確認した。設定は監査有効状態を保持し、転記間隔だけ5秒から6秒へGUI保存してv1を確認し、F5と正常停止・再open後もv1／6を確認した。他入力は保持した。証拠は`windows-candidate-r3/typed-write-acceptance.json`と`browser-v7`〜`browser-v9`の個別JSON・画像へ保存した。運用DBの移行は行っていない。

同じ固定R3のWindows Releaseでも、`windows-candidate-r3/near-release/typed`の14件と`typed-crash`の3件が同DLLで成功した。最初に全参照を含むReleaseビルドを0警告・0エラーで行い、以降は同DLLを再利用した。固定source1498ファイルと、既存Debug出力、Release出力285ファイルの集合・hash・更新ticksを前後照合した。WindowsとLinuxそれぞれで、UI44・Trace Core9・Recorder8・実Native／Timer／IPC4・履歴17・型付き移行14・強制終了3の計99実行、重複を除く93ケースが成功している。近接試験の成功範囲と、形式7の実画面受入は個別に記録する。

Linuxの形式7実ブラウザー、64Mi文字境界と巨大件数の負荷測定、型付き再export、共通認可と定義・成果物対応の型付き化、SharedV2物理Set化を残す。

## 実Tsavoriteの復旧受入

`HistoryPeriodProbe`をReleaseの同一DLLで実行した。通常の`Execution.Store.Open`、`CreateTsavoriteSet<long,Row>`、`Save`、破棄後の別プロセス再openを通した。2048行の6個のlong値、Count、内容digestを照合し、149824のtailと10個のhlog segment、native snapshot/indexを実ディスク上で確認した。

証跡は`F:/Seamless/history-period-probe-5563e5fbf68f44b9b0ec3667f53cb54b/memory32`にある。入力DLLと試験SourceのSHA-256、変更前ファイル一覧、故障対象の解決絶対パス、TRX、例外を保存した。16MiB条件の復旧予約不足は親ディレクトリの初回結果へ分離した。

| 観測 | 結果 |
| --- | --- |
| 正常保存・別プロセス再open | 32MiBメモリ上限、4KiB page、16KiB segment、8KiB log memory、1KiB indexで2048行一致。小indexとspillの列挙は約39秒 |
| 16MiBメモリ上限での再open | 常駐約8.51MBに復旧追加約8.39MBを要求し、ResourceLimitException。32MiBの別rootで正常系を再確認した |
| indexのht.dat.0だけを削除 | 全行一致で復旧。これだけを破損検出の証明に使わない |
| cpr-checkpoints削除、hlog先頭segment削除 | TsavoriteExceptionで復旧失敗 |
| logとsnapshotの中間1byte変更 | 選んだ未使用位置では全行一致。任意破損の検出保証とはしない |
| Row64のValueを192から154へ1byte変更 | 例外なし、Countは2048のまま。全値とdigestの比較だけが意味内容の破損を検出した |
| store.json削除 | 例外なしで空Store扱いとなり、0行へ新規化された。履歴readerの事前manifest検査が必要 |
| 一つ目の期間を破損、二つ目は健全 | 一つ目を診断した後、独立した二つ目の2048行を継続読取できた |

この結果から、期間のseal、必須manifest・fileの事前検査、各行の意味内容hashを履歴側の必須条件とする。nativeのopen成功や保存Countだけで健全と判断しない。旧JSON履歴は自動移行せず、新しい期間Store領域と寿命を分ける。

| 確認対象 | 現在の状態 | 結果・候補・証拠 |
| --- | --- | --- |
| 型付きSetの保存・閉鎖・再open | Windows Debug近接成功 | 隔離native実保存に加え、MemoryPack payloadの製品保存、同期間の反復読取と別recorderでの再openを確認 |
| 一期間の削除・manifest欠損 | Windows Debug近接成功 | 欠損を拒否し、manifestを再作成せず健全期間を読取。期間保持数と未完stagingの停止も確認 |
| checkpoint・logの部分欠損／破損 | Windows Debug近接成功・配布受入待ち | 行hash、log・checkpoint・seal改変を検出して健全期間を継続。全ての破損位置からの救済は保証しない |
| 読取開始後・consumer書込中の削除 | 実験待ち | 実際のネイティブI/Oの挙動を記録する |
| 履歴不調後の新期間記録・サーバー再起動 | 不調後の受入待ち | Linux固定Releaseでは正常停止・再起動後に4実行ID・20frame・4終端を通常UIから復元した。証拠は可視化計画のP7節へ集約し、履歴不調を組み合わせた受入と分ける |
| 形式6のbackup・stage移行・取消・復旧 | 第1単位はWindows Debug近接成功 | 利用者・設定の型付き4表と明示6→7移行・rollback・実強制終了の確認範囲は本書の管理データ節へ記載。管理全体の移行、Release／Linux受入、物理Set化は継続する |

旧候補のJSON履歴ファイル削除の確認記録と、SharedV2用`TsavoriteHistoryTests`は、今回の期間Setの破損受入とは別の証拠として扱う。現在の`QueryExecutionHistory`は期間Setに合わせた試験へ更新されている。`SystemSecurityStoreTests`のMockは実管理Tableへの保存成功と区別する。

製品の近接証拠は`F:/Seamless/visualization-20261109-0e63c1c7/near-debug-r17/checks.trx`の15成功と、保持数修正後の`near-debug-r18`2成功、未完stagingの`near-debug-r19`1成功へ分かれる。共通raw期間と種類付きsealへの変更後は`near-debug-r23`で履歴代表、旧seal互換、TraceRecorderの8ケースを再確認した。実タイマー、正常・失敗・取消、IPCの通知枠分離、返却前の権限再検査は`near-debug-r27`の4ケースが成功した。UI12ケースは`near-debug-r26`で成功した。

BestEffortの満杯時に終端frameが欠落することを実観測した。`Captured=21 / Saved=18 / Dropped=3 / SaveFailures=0`であり、保存保証を検査する失敗終端はCompleteで確認する。複合試験`near-debug-r25`はFドライブの高いI/O負荷とqueue16を観測した条件で600秒に達し、合格には含めない。同じDLLを分割した後続試験では停止を再現していない。負荷条件を統制した停止・性能受入を残す。

## 調査版の識別

2026年11月9日の未コミット作業ツリーを読んだ。以下は調査時点のファイルSHA-256であり、後続の保存実装・受入候補と区別する。

| ファイル | SHA-256 |
| --- | --- |
| `Seamless.Management.Server/ManagementStore.cs` | `06FE46530DBCCC332D294A147DB5996C1D109BE665810BEAFA0E8AEB979717F8` |
| `Seamless.Management.Server/ManagementAuditDatabase.cs` | `66E93AEE30211F20BD1F1F7742C2403D84E1CAE0DC4B120F3B0C80744589EA2B` |
| `Seamless.Management.Server/StoredAuthorization.cs` | `9BB4D3A55049B60BB4A544B5AE3222D56DEEEDAB1A69DB8D9B77C9E27D5B849A` |
| `Seamless.Database/DatabaseVersionedTransactions.cs` | `1794CF924192825FF46FE7CBDCBF08A8A22FE27FBC8B031BF37E80D80C4AA256` |
| `Seamless.Collections.Tsavorite/Stores/HistoryStore.cs` | `F0ADC93AECD229688F6864AD2D56A591E69477695BD2A78FC853A7945A3CEFC1` |
| `Seamless.Execution/Store.cs` | `E6D7F6D8E38D57CD6684171E266923805B765BFE9CA58C0B57B12C924D388417` |
| `Seamless.Management.Server/QueryExecutionHistoryStore.cs` | `4484D25DF590AC5C0FEE41A1423D4C138939F367996402F5371C68EA950D92C2` |
