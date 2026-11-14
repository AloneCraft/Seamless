# 最小マルチプロジェクトテンプレート

DB 定義・Server・Client の3プロジェクトをビルドするための出発点。

| プロジェクト | 役割 |
|---|---|
| Database | 共通の要求型と `DatabaseContainer` 派生型を定義するライブラリ |
| Server | メモリDBを初期化して正常終了するコンソールアプリ |
| Client | 共通の要求型とパッケージ参照を確認して正常終了するコンソールアプリ |

Seamless 本体は3プロジェクトとも `PackageReference` で参照する。Server と Client はアプリ内の Database プロジェクトを `ProjectReference` で参照する。

## ビルド

リポジトリルートから実行する。.NET 10 SDK を使用する。

```powershell
dotnet build MultiProjectTemplate/MultiProjectTemplate.slnx -c Debug
dotnet build MultiProjectTemplate/MultiProjectTemplate.slnx -c Release
```

この小さなソリューションと、ルートの `Seamless.slnx` の `MultiProjectTemplate` フォルダーは、同じ3プロジェクトを参照する。

## パッケージの取得

既定版は `Directory.Build.props` の `SeamlessPackageVersion=0.0.0`。`NuGet.Config` で Seamless の取得元を `Work/VirtualDbFeed` に固定し、その他の依存パッケージは nuget.org から取得する。復元先は `Work/MultiProjectTemplate/packages`。

`Work/VirtualDbFeed` は開発環境のローカルフィードであり、Git 管理外。別環境では対応する `.nupkg` 一式をこの場所へ配置するか、`NuGet.Config` の `SeamlessLocal` を利用できるフィードへ変更する。

## Visual Studio

`Seamless.slnx` を開き、`MultiProjectTemplate` フォルダー内のプロジェクトをビルドする。3プロジェクトだけを開く場合は、このディレクトリの `MultiProjectTemplate.slnx` を使用する。Server または Client をスタートアッププロジェクトにして実行する。

## VS Code

この `MultiProjectTemplate` ディレクトリをフォルダーとして開く。C# 拡張を導入し、ビルドタスクで3プロジェクトをビルドする。実行とデバッグから「Server の起動確認」「Client の起動確認」を選べる。

## CLI での起動確認

```powershell
dotnet run --project MultiProjectTemplate/Server/MultiProjectTemplate.Server.csproj --no-build
dotnet run --project MultiProjectTemplate/Client/MultiProjectTemplate.Client.csproj --no-build
```

現在の段階の受入対象は、パッケージ復元・ビルド・両 EXE の起動。クライアントとサーバーの通信、テーブル・ビューの自動生成、正規化ストラテジ等は、中間仕様書に従う後続の実装対象。

中間仕様書は `Document/20261114Argon.md` にある。
