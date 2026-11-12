# Seamlessアプリケーションのテンプレート

`Seamless.Application/Seamless.Application.slnx`は、編集・ビルドできるテンプレートの正本です。リポジトリの`Seamless.slnx`の`Template`フォルダーからも、同じ4プロジェクトを開けます。

| プロジェクト | 役割 |
|---|---|
| Server | EXE。利用者の具体的なスキーマ型をコードから参照せず、collectible AssemblyLoadContextでServerSchemaの入口を読み込む。 |
| Schema | 利用者が書く公開スキーマ・契約。partialプロパティの実体・コンストラクター・MemoryPack処理を生成する。 |
| ServerSchema | DBコンテナーとプログラマーが書く処理本体。Schemaを参照する。 |
| ClientSchema | 公開メソッドの通信クライアントを生成する。ServerSchemaを参照しない。 |

ServerのProjectReferenceはモジュールのビルド・配置にも使いますが、ProgramはSchema/ServerSchemaの具体型を使用しません。Schemaのpartial型を別DLLから完成させる構成ではありません。

## 開発と生成

VS Codeでテンプレートまたは生成したアプリのフォルダーを開くと、`.vscode/tasks.json`の「ビルド (Debug)」「ビルド (Release)」で四プロジェクトをビルドできる。既定のビルドタスクはDebugとする。必要なのは.NET 10 SDKであり、ManagerからのDB定義のコンパイルや稼働版への適用とは別の操作である。外部保存の自動取り込み・Managerの未適用タブ表示は[外部ソースの設計](../docs/refactoring-designs/20261104-external-source-draft-state.md)を参照し、テンプレートのビルド成功だけでその同期機能が実装済みとは扱わない。

.NET 10 SDKを使用します。リポジトリ内では既存のSeamlessプロジェクトを参照します。リポジトリ外へ生成した場合は、`SeamlessRepositoryRoot`で開発リポジトリを指定するか、対応版のSeamlessパッケージを取得できるNuGetフィードと`SeamlessPackageVersion`を設定してください。未公開版が公開NuGetフィードに存在すると仮定しません。

```powershell
dotnet new install ./Template/Seamless.Application
dotnet new seamless -n MyApplication
dotnet build ./MyApplication/MyApplication.slnx -c Debug -p:SeamlessRepositoryRoot=G:/GitHub/Seamless
```

インストールされる表示名は「Seamlessプロジェクト」です。VS CodeではCLIで生成したslnxを開けます。Visual Studioの新規プロジェクト画面での表示・生成は、対応するテンプレートエンジン・登録環境で別に検証します。

## ビルド済みNativeパッケージ

ここでのNativeは、Seamless専用の型付きRPCを指すコード上の名称です。生成クライアントがメソッドID・引数・契約識別を送り、サーバーの生成アダプターが利用者のC#メソッドへ接続します。DLLはCLR上で動作する.NETのマネージDLLです。クエリパッケージは、このDLLと公開メソッド・依存DB・ハッシュ等の登録情報をまとめたフォルダーです。

Serverの通常ビルドは、`Directory.Build.targets`から生成モジュールのメタデータ出力を実行します。既定の出力先はServerの出力フォルダー内の`native`です。`native-build.json`を固定の入口とし、`generations/<ID>`へモジュール・DB定義DLLと`native-source.json`を保存します。ファイル参照は相対パスで記録するため、出力したフォルダーをまとめて移設できます。再出力では新しい世代を作り、書き出し完了後に固定の入口を置き換えます。

メタデータの出力だけを実行する場合は、ビルド済みServerへ出力先を渡します。

```powershell
dotnet ./Server/bin/Debug/net10.0/Seamless.Application.Server.dll --export-native F:/Seamless/ApplicationPackage
```

このモードは生成した型付き契約から定義を取り出します。通常モードではDBを起動し、受信した要求に応じて問い合わせ本体を実行します。起動時に既定の`native/native-build.json`を検査・ロードし、そのモジュールをNativeサーバーへ登録します。依存するSchemaやSeamlessランタイム等のDLLは、共通ソリューションのビルド出力と一緒に配置します。

読取時にはファイル参照の範囲と各DLL・メタデータのハッシュを検査し、ロード時に実行中DBのDLLと公開型・通信契約を照合します。既存の生成スキーマ識別があるDBにはその値を使い、それを持たないDBには定義DLL全体のSHA-256を使います。生成問い合わせは、読み取り専用の解析結果を接続するまで更新可能として登録します。

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
./Template/Verify.ps1 -WorkRoot F:/Seamless/TemplateWork -LogRoot F:/Seamless/TemplateLogs
```

作業領域とログには未使用のFドライブ配下を指定し、共通最大8CPUで実行します。元テンプレートと、独立したテンプレートhiveへ登録して別名で生成したソリューションのDebug/Releaseをビルドし、各Serverを`--self-test`で実行します。

- 4プロジェクトの生成、名前・名前空間とProjectReferenceの対応。
- partialプロパティ・コンストラクター、生成Fのフィールド順とサイズ（64bit環境でbyte/long/byteを24から16バイトへ）。
- 最適化未指定時の従来配置、値・参照・参照を含む値型・Nullable・nullの往復。
- 参照を含むレコードの生成formatterが論理順を維持すること。
- メモリDB、FLinqを適用する処理、生成クライアントからLoopback上のNative v1サーバーへの実接続。
- 通常ビルドのNativeパッケージ出力、実行中DBとの照合、パッケージからロードした生成モジュールによるRPC。
- 公開メソッドの事前生成クライアントによる集約・更新。
- 通信の明示取引、処理失敗・取引取消によるロールバックと、取引確定後の集約結果。
- 生成レコードの論理順に基づく受信前検査、参照を含むnullable値の往復。

初期データは自己試験でだけ追加します。通常起動では空のメモリDBを開き、Loopbackの動的ポートで既存の実験用Native v1を待ち受けます。永続DBの自動初期化・変換は行いません。

## 対応範囲

`ClientContract`からの公開メソッド生成は、引数一つと同期の戻り値を持つ、オーバーロードのない非genericメソッドに対応します。列・集合は行ストリームへ、単一値は一つの結果へ接続します。生成クライアントの入口は`IAsyncEnumerable<T>`と`Task<T>`であり、通常接続と明示取引を扱います。Graph形式で引数全体を正規化し、参照の共有と循環を保持します。非対応の契約は`DBGEN004`で拒否します。

## 生成RPCの自己試験

公開契約の `IApplicationQueries` は `Read`・`Aggregate`・`Insert`・`Fail` を宣言し、ServerSchemaの `ApplicationQueries` が処理を実装します。ClientSchemaの `ApplicationQueriesClient` から同じメソッドを呼びます。

`--self-test` は通常ビルドで出力したパッケージをロードし、集約・更新・単文の失敗・取引全体の取消・取引の確定を実接続で確認します。処理本体はサーバー実装DLLに置き、公開契約と生成クライアントのDLLを分離します。
