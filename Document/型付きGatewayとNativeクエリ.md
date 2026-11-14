# 型付き Gateway の起動と Native クエリ

`GatewayDatabaseHost` は新しい専用保存領域を作り、SQL の CREATE／ALTER／DROP TABLE を型付き C# の生成・ビルド・全表移行へ接続する。接続を維持し、外側の COMMIT で参加 DB と Native クエリを公開する。旧 Seamless 保存形式をこのプロファイルで開いて移行する機能はない。

## 実サーバー

`seamless-server --gateway <configuration.json>` を使う。従来の `--generation` は既存の比較環境用である。

`Seamless.Database.Server.csproj` は `seamless-server` を提供する .NET ツールパッケージで、Gatewayの実装を参照する。管理用のServerWindows・ServerLinux・WindowsUI ZIPとは別の成果物である。Gatewayの変更を配布物で検証するときは、ツールに含まれる `Seamless.Database.Gateway.dll` を照合する。

ツールパッケージの作成には .NET SDK と、PATHから起動できる PowerShell 7（`pwsh`）が必要。パック時に実際の発行ファイルから依存物を一覧化し、`LICENSE`、`THIRD-PARTY-NOTICES.md`、`ThirdParty/packages.json` と各通知を同梱する。

```json
{
  "StorageDirectory": "F:/GatewayLab/store",
  "WorkingDirectory": "F:/GatewayLab/work",
  "CacheDirectory": "F:/GatewayLab/generated",
  "GeneratorAssemblyPath": "F:/packages/seamless.database.generator/<version>/analyzers/dotnet/cs/Seamless.Database.Generator.dll",
  "InitialCatalogs": [
    {
      "Name": "gateway_lab",
      "Schemas": [{ "Name": "dbo", "Tables": [] }],
      "DefaultCollation": "Japanese_CI_AS"
    }
  ],
  "Endpoints": {
    "TdsPort": 14333,
    "HttpPort": 8088,
    "NativePort": 14335,
    "Http": { "ListenAddress": "127.0.0.1" }
  }
}
```

ディレクトリと compiler のパスは絶対パスにする。初期カタログは新規領域で必要であり、再起動時は共通チェックポイントと WAL に保存した状態を使う。`--ready-file <path>` は PID・生成時刻・実際のポートを JSON で書き、`--stop-file <path>` はその専用ファイルが作られたときに停止する。ポート 0 は空いているポートを選ぶため、接続先は ready ファイルから取得する。

`Endpoints.Http.ListenAddress` はIPv4またはIPv6のアドレスを文字列で指定する。上の例は同一PCからのHTTP接続だけを受け付ける。省略した場合は[HTTPの既定公開設定](Kestrel通信基盤.md#httpの公開設定)が適用される。設定の読み取りや起動に失敗した場合は、CLIが標準エラーに理由を出し、終了コード1を返す。

開発・比較では `TestSeamless/Invoke-LocalProcess.ps1` を通し、全子孫を共通の最大 8 論理 CPU に制限する。この比較環境では作業・実データ・キャッシュ・ログを F に置く。

SQL Server から初期データを入れるプログラムでは、`GatewayDatabaseHost.OpenAsync` の `initialDataSource` に `IDatabaseImportSource` の factory を渡す。生成した行型で逐次読み込み、非公開 seed へ入れる。factory が返した `IDisposable` は取り込み後に解放する。キーなし表の重複行と、取り込み元の IDENTITY watermark を保持する。通常の SQL Server 取り込み経路も維持する。

## Native のソース登録

`GatewayTransactionManager.InstallNativeQueryAsync(NativeQuerySourceProject)` に、ModuleId・入口型名・C# ファイル・query 定義・依存 DB 名を渡す。`GeneratedAssemblyBuildOptions.NativeAnalyzerPaths` には、必要な MemoryPack 等の source generator を設定できる。ファイル、生成プロジェクト、ビルド結果は隔離領域へ保存し、永続カタログへ `System.Type` や delegate を保存しない。

NativeでもFLinqを使う場合は、`NativeAnalyzerPaths`に配布した`Seamless.FLinq.Generator.dll`の絶対パスを設定する。実サーバーのJSON設定も同じ名前で受け取る。生成器の探索や別バージョンへの自動切替えは行わない。Nativeの独立プロジェクトにはinterceptorのcompiler設定を渡し、生成したコードをそのビルドの`LogDirectory/generated`へ保存する。

FLinqの対象メソッドでは、通常のキー付き`Table`と`MaterializedView`をそのまま入力にできる。例えば`db.dbo.Items.Where(row=>row.Id>0).Sum(row=>row.N)`は、対応する形状なら`GetQueryRows()`を介して走査と演算を融合する。版付きGatewayではNativeの呼出しが所有する取引へ委譲し、その読取り時点と自己変更を使う。従来方式では既存の読取sessionと自己overlayを使う。単純なCountもこの区別を保ち、従来方式の現在値件数をMVCCの件数へ流用しない。新しい取引や独立したcoordinatorをこの変換で開始しない。全てのLINQ形状のnative化・並列化を保証するものではなく、生成コードのアクセス・並列・資源・同期コメントで採用した経路を確認する。

依存 DB のスキーマ変更では同じソースを新 DLL に対して再ビルドする。列の削除などでビルドできない場合は、そのモジュールをコンパイル診断付きで無効化する。ソースを自動修正しない。`NativeQueries` で公開中の定義と診断を取得できる。必要な列を戻して再ビルドに成功すれば有効に戻る。

明示的取引では非公開の query 世代も取引が所有する。保存点は DB の物理世代と Native の版を保持し、ROLLBACK で両方を戻す。seal で DB の ALC を開き直した後は、Native の参照も実際の DB ALC に結び直す。共通 WAL の commit と checkpoint はクエリ成果物の参照を持ち、再起動時も schema・DLL・source のハッシュを検証する。

Native の `INativeQueryContext.GetDatabase(name)` は宣言済みの依存 DB だけを返す。書込 query は、読取スコープを開く前に関連 DB を書込参加させる。文終了時に全参加 DB の制約を検証し、同じ保存点・最外側の COMMIT／ROLLBACK に参加させる。

`NativeWireContract` は型名、メンバー順序、型、NULL 注釈、formatter 属性、unmanaged layout をハッシュ化する。登録済みソースの引数・結果契約とクライアントの型を実行前に比較し、不整合は回復可能なエラーにする。public field と property が混在する契約は、全メンバーに `MemoryPackOrder` を指定する。任意の利用者コードが宣言と異なるバイト列を返すことの正しさは、利用者コード自身の責任である。

## 世代の回収と serializer

共通チェックポイントが全参加 DB を保存し WAL prefix を回収した後、全 DB の使用権を持つ時点で回収候補を確定する。使用権の解放後にその集合だけを削除し、後から作った候補を巻き込まない。現在と直前の公開世代を保持し、参照が残る DLL は診断付きで保留する。`LastGenerationCollection` で回収・保留の結果を取得できる。

MemoryPack 1.21.4 は formatter provider の共有辞書に型を強参照し、公開の解除 API を持たない。[利用中バージョンの実装](https://raw.githubusercontent.com/Cysharp/MemoryPack/1.21.4/src/MemoryPack.Core/MemoryPackFormatterProvider.cs) に合わせた所有権 adapter が、実行 lease の終了後に、その ALC の型を含む登録だけを解放する。共通契約の formatter は保持する。検証済みバージョンと辞書構造が一致しない場合は、Native の読み込み前に拒否する。

## 検証の区別

型・列属性の範囲は[対応表](Gateway型付きDDL対応表.md)、不変条件と残る受け入れは[設計](20261114Argon.md)を参照する。自動試験、NuGet consumer、実 SSMS の画面操作は別々に記録し、一方で他方を代用しない。
