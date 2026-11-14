# Gateway 型付き DDL の型と列属性

この表は新しい `GatewayDatabaseHost` 保存プロファイルの現在の実装を示す。全段階の受け入れ完了を表すものではない。通常の単独 C# 利用と既存取り込みの保存形式は、それぞれの仕様に従う。

| SQL 型 | 生成レコードの表現 | CREATE／ALTER 宣言・保存 | 代入・移行 | TDS／実測状況 |
| --- | --- | --- | --- | --- |
| bit | Boolean／Nullable | 対応 | 数値 0／非 0、NULL | 実接続で確認 |
| tinyint／smallint／int／bigint | Byte／Int16／Int32／Int64 | 対応 | 整数範囲を検査、小数部を切捨て | 境界と bigint 移行を確認 |
| real／float(p) | Single／Double | 対応、p 1～24 は Single | NaN・無限大を拒否 | 実接続、浮動メタデータ回帰を確認 |
| decimal／numeric(p,s) | Decimal | p 1～28 を受理 | scale を丸め、precision 超過を拒否 | 丸めの実機比較、全境界比較は未完了 |
| decimal／numeric の p 29～38 | 拡張表現が必要 | 宣言を変更前に拒否 | CLR decimal へ黙って狭めない | 未対応 |
| money／smallmoney | Decimal | 対応 | scale 4、丸めと型固有の最小・最大値を検査 | 最小・最大値・桁あふれと正負の丸めを実機比較 |
| char／varchar／nchar／nvarchar／sysname／text／ntext | String | 対応、長さ・照合順序を保持 | 代入・縮小 ALTER で切詰めを拒否、固定長を補完 | 日本語・NULL を実接続で確認、コードページ全比較は未完了 |
| binary／varbinary／image | Byte[] | 対応 | 長さを検査、固定長を補完。整数は SQL の幅と左側補完・切詰め | バイト列・NULL・整数 CAST を実機比較、全 CAST 規則は未完了 |
| uniqueidentifier | Guid | 対応 | 正規形・波括弧、SQL の末尾切詰め、16 byte の内部順序。32桁形式・先頭空白を拒否 | NEWID・実接続と変換の実機比較 |
| xml | String | 対応 | 非型付き XML の fragment、空要素・CDATA の正規化、xml:space、DTD の拒否。型付き schema collection は拒否 | XML・NULL・無効 XML・DTD・複数要素を実機比較 |
| date | DateOnly | 対応 | 日付変換 | 実接続、0001-01-01／9999-12-31 を実機比較 |
| time(n) | TimeOnly | 対応、n 0～7 | 精度を丸め、日末では最終時刻へ留める | 型付き値・文字列・日末を実機比較 |
| datetime2(n) | DateTime | 対応、n 0～7 | 精度を丸め、日付繰上げと範囲を検査 | 精度と日付繰上げを実機比較 |
| datetime／smalldatetime | DateTime | 対応 | datetime の 1/300 秒、smalldatetime の分丸め・範囲 | 29.998／29.999 秒と日付範囲を比較 |
| datetimeoffset(n) | DateTimeOffset | 対応、n 0～7 | offset を保持し、精度・範囲を検査 | 実接続と日付繰上げを比較 |
| rowversion／timestamp | Byte[8] | 対応、表ごとに１列。SQL カタログでは timestamp | DB 共通の永続採番、INSERT／UPDATE、追加列の既存行生成、@@DBTS。明示 INSERT は DEFAULT 以外拒否、明示 UPDATE は拒否 | SQL Server の取消・DEFAULT・NULL メタデータを比較。Native・取り込み・列削除・checkpoint／再起動も確認 |
| sql_variant／CLR UDT／空間型など | 型別の表現・演算が必要 | 型付き DDL の対応表外 | バイナリ取り込みと専用演算を同一視しない | 全受け入れ未完了 |

## 列属性と制約

- IDENTITY は seed／increment、INSERT、IDENTITY_INSERT、値の WAL 予約、取消・checkpoint・再起動後の非再利用を扱う。@@IDENTITY／SCOPE_IDENTITY／IDENT_CURRENT、sys.identity_columns の sql_variant の値、RPC の内外のスコープも実機比較した。Native の型付き挿入は明示値を保持し、同じ永続採番位置を進める。IDENTITY 列の更新を拒否する。
- DEFAULT は定数・式・組み込み日時・NEWID・T-SQL scalar UDF、WITH VALUES、INSERT の DEFAULT と UPDATE DEFAULT を扱う。WITH VALUES は新しい列への値の生成に使い、既存列だけに DEFAULT を追加する場合は既存の NULL を書き換えない。保存済みの値を再オープンで再評価しない。
- 計算列は式の型を SQL 評価器で推論し、生成 getter もその世代の評価器へ接続する。PERSISTED は計算した値を文・取引確定前に保存値へ反映する。非決定的な PERSISTED、計算列から別の計算列への参照、直接の subquery、計算列への SQL の明示書込を拒否する。
- PK／UNIQUE／FK／CHECK／DEFAULT の追加・削除と、SQL・Native の NO ACTION／CASCADE／SET NULL／SET DEFAULT を扱う。REFERENCES の参照列省略は相手の主キーを使い、自己参照も解決する。無効・未信頼制約、すべての列属性・照合順序・依存関係の網羅は残る。
- scalar UDF は CREATE／ALTER／DROP、変数、分岐、ループ、表参照、SELECT 変数代入、関数呼出、最大 32 段の再帰、基本的な SCHEMABINDING と NULL 入力オプションを扱う。table-valued UDF、任意の T-SQL 文・関数の対応完了とは扱わない。
- sys.check_constraints／default_constraints／key_constraints／foreign_keys／foreign_key_columns と UNIQUE の sys.indexes／index_columns を共通 SQL 経路で参照できる。親表・列の永続 ID で結合し、OBJECT_ID／sys.objects も同じ制約オブジェクトを返す。自動生成した制約名の区分と列内 CHECK の所有列を保存し、SQL Server 取り込み・ALTER・再起動でも保持する。

## SQL Server との値・メタデータ比較

34 種類の宣言について、最小値・最大値・NULL を SQL Server の専用一時表と型付き Gateway に保存し、SqlClient が受け取る値、型名、長さ、精度、scale、NULL 許容性を比較する。変更のない全表を２回の ALTER でコピーした後も同じ結果を確認する。decimal の各 precision／scale の組、全コードページ、全変換を網羅する試験ではない。

DML の値式は SELECT・DEFAULT と同じ SQL の型推論・変換・関数評価を使う。UPDATE／DELETE の対象行の条件は SQL の照合順序と NULL の規則に従って評価する。行の列を型付き SQL パラメーターへ結び、利用者のパラメーター名と衝突させない。DML の相関 scalar subquery は変更前に拒否する。

対応と記載した型でも、SQL Server の全変換・全演算・全エラーが一致するという意味ではない。[型付き DDL 設計](20261114Argon.md) の受け入れ条件は継続して適用する。
