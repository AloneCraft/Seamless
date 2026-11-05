# Seamlessアプリケーションのテンプレート

`Seamless.Application/Seamless.Application.slnx`は、編集・ビルドできるテンプレートの正本です。リポジトリの`Seamless.slnx`の`Template`フォルダーからも、同じ4プロジェクトを開けます。

| プロジェクト | 役割 |
|---|---|
| Server | EXE。利用者の具体的なスキーマ型をコードから参照せず、collectible AssemblyLoadContextでServerSchemaの入口を読み込む。 |
| Schema | 利用者が書く公開スキーマ・契約。partialプロパティの実体・コンストラクター・MemoryPack処理を生成する。 |
| ServerSchema | DBコンテナーとプログラマーが書く処理本体。Schemaを参照する。 |
| ClientSchema | 公開メソッドの通信クライアントと、型付きラムダの呼出位置を生成する。ServerSchemaを参照しない。 |

ServerのProjectReferenceはモジュールのビルド・配置にも使いますが、ProgramはSchema/ServerSchemaの具体型を使用しません。Schemaのpartial型を別DLLから完成させる構成ではありません。

## 開発と生成

.NET 10 SDKを使用します。リポジトリ内では既存のSeamlessプロジェクトを参照します。リポジトリ外へ生成した場合は、`SeamlessRepositoryRoot`で開発リポジトリを指定するか、対応版のSeamlessパッケージを取得できるNuGetフィードと`SeamlessPackageVersion`を設定してください。未公開版が公開NuGetフィードに存在すると仮定しません。

```powershell
dotnet new install ./Template/Seamless.Application
dotnet new seamless -n MyApplication
dotnet build ./MyApplication/MyApplication.slnx -c Debug -p:SeamlessRepositoryRoot=G:/GitHub/Seamless
```

インストールされる表示名は「Seamlessプロジェクト」です。VS CodeではCLIで生成したslnxを開けます。Visual Studioの新規プロジェクト画面での表示・生成は、対応するテンプレートエンジン・登録環境で別に検証します。

## プロパティ宣言とフィールド配置

Schemaの`DatabaseGenerator.OptimizeFieldLayout=true`で、新しいスキーマの配置最適化を明示的に有効にします。未指定の既存スキーマは従来の配置を維持します。

```csharp
public partial struct Item{
    public partial byte Kind{get;}
    public partial long Id{get;}
    public partial byte State{get;}
}
```

生成器はprivateの`F`と`_F`を生成し、プロパティを`_F`の対応フィールドへ接続します。参照を含まない値と参照を含む型をまとめ、値側は推定サイズの大きいものから配置します。同じサイズの列の順は安定させます。`string`等だけでなく、参照を含む値型も参照側へ分類します。実際のABI、パディング、GC参照の配置はCLRが決めるため、最適性は測定対象です。

プロパティ・コンストラクター引数と生成formatterの論理順は維持します。MemoryPackのunmanaged型の生メモリ経路は物理配置そのものを転送するため、並べ替えを通信・保存互換な変更とみなしません。固定された公開契約とサーバー専用の内部配置を分け、既存の保存領域へこのオプションを後付けするときは明示的な移行が必要です。

この変更はTsavoriteの固定領域と可変領域を新たに分割するserializerの実装ではありません。現在のストアが選択するunmanaged/MemoryPackの経路を維持し、その入力になるレコードの生成フィールドを整理しています。

## 基本試験

```powershell
./Template/Verify.ps1
```

試験は独立したDの作業領域、Fのキャッシュ・ログ、共通最大8CPUで実行します。元テンプレートと、独立したテンプレートhiveへ登録して別名で生成したソリューションのDebug/Releaseをビルドし、各Serverを`--self-test`で実行します。

- 4プロジェクトの生成、名前・名前空間とProjectReferenceの対応。
- partialプロパティ・コンストラクター、生成Fのフィールド順とサイズ（64bit環境でbyte/long/byteを24から16バイトへ）。
- 最適化未指定時の従来配置、値・参照・参照を含む値型・Nullable・nullの往復。
- 参照を含むレコードの生成formatterが論理順を維持すること。
- メモリDB、FLinqを適用する処理、生成クライアントからLoopback上のNative v1サーバーへの実接続。
- クライアント専用DLLで記述したブロックラムダの構造化、PrepareAsyncと初回の自動準備、複数集約の一括実行。
- 準備済み更新の再利用、権限拒否、ローカルと通信の取引、処理失敗・外側の取引取消によるロールバック。
- 生成レコードの論理順に基づく受信前検査、参照を含むnullable値の往復。

初期データは自己試験でだけ追加します。通常起動では空のメモリDBを開き、Loopbackの動的ポートで既存の実験用Native v1を待ち受けます。永続DBの自動初期化・変換は行いません。

## 対応範囲

`ClientContract`からの公開メソッド生成は「引数一つ・IEnumerable<T>戻り値・オーバーロードなし」のNative v1用です。非対応の契約は`DBGEN004`で拒否します。

ラムダには`connection.Lambda<IApplicationDatabase>("application")`を使用します。以下のように定義し、`PrepareAsync`で先に準備するか、`ExecuteAsync`の初回に自動準備します。

```csharp
var client=connection.Lambda<IApplicationDatabase>("application");
var query=client.DefineQuery<long,long>(static(db,min)=>{
    var rows=db.Items.Where(row=>row.Id>=min);
    var count=rows.Count();
    var sum=rows.Sum(row=>row.Id);
    return count*100+sum;
});
await query.PrepareAsync();
var result=await query.ExecuteAsync(7);
```

生成器はラムダを有限の構造へ変換し、サーバーが登録済みの型・操作・権限を検査してRoslynとFLinqで準備します。`PrepareAsync`はデータ処理を実行しません。準備済みの実行ではハンドルと引数を送り、複数の集約も一つの要求に含めます。一つの要求にまとめることと走査を融合することは別で、すべての集約を一走査にできる保証はありません。

ブロック、ローカル変数、return、条件分岐、foreach、対応するLINQ、追加・削除・置換を扱います。実行ごとの値は型付きの明示引数へ渡します。未対応構文・捕捉・呼出先は`DBGEN006`で拒否し、クライアントでの実行へ切り替えません。独立した更新は一つの取引で確定し、`transaction.Lambda<IApplicationDatabase>()`では外側の取引へ参加します。

現在のラムダ入口はLoopbackでの機能検証用です。認可は登録時に渡すコールバックで行います。管理サーバーの認証セッションへの統合、ストリーミング結果、動的なDB世代への再束縛、実行の資源上限、NuGetだけでの配布は、別途実装・検証が必要です。

準備キャッシュは既定256件です。満杯では使用していない項目を利用順に回収し、すべて使用中の場合は実行前に拒否します。失効・Dispose後も実行中のDLLを保持し、最後の実行リースが外れた後にDelegateとロードコンテキストへの参照を解放します。`RegisterLambda`の第3引数で容量を設定でき、`CacheStatistics`で有効件数・失効後の保持件数・実行リースを取得できます。通信型はランタイムへの登録前に`Expose<T>()`し、登録後のカタログは変更しません。

MemoryPackの読み込み前には、検証済みの1.21.4向けの型別検査を行います。長さ・残量・乗算・UTF-8・メンバー数・末尾を検査し、独自formatter・循環する型・未対応の形式は受け付けません。Seamless生成レコードは`NativeWireLayoutAttribute`で示す論理順と、既存のnullableの保存形式を検査に使用します。引数はDB表現の束縛前、結果は更新の確定前とクライアントのデシリアライズ前に検査します。

既定はデータ16MiB未満、文字列65536文字、配列要素の合計65536、検査ノード100万です。サーバーでは`NativeLambdaSchema.WireLimits`、クライアントでは`Lambda<T>(database,limits)`で上限を設定します。業務上の値域・必須項目などの検証は公開処理側で行います。過大な準備要求はフレームのヘッダーを読んだ時点で拒否し、本文の領域を確保しません。

MemoryPack自身は未信頼入力を対象外としており、この制限付き検査だけでインターネット公開の安全性を保証しません。対応プロファイルの検査・認証・TLS・実行資源制限・異常入力試験を合わせて検証します。[MemoryPack公式説明](https://github.com/Cysharp/MemoryPack)

このテンプレートのNative v1待受には、設計中のTLS・統合認証・新しい公開契約交渉は接続していません。運用用の外部公開サーバーとして扱わず、外部アドレスへバインドを変更して利用しないでください。通常の管理サーバー・Managerの既存の起動方法は変更しません。
