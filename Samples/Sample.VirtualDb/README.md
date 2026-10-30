# Sample.VirtualDb

Seamless の NuGet パッケージを参照して、架空の証券会社データベースを動かす .NET 10 コンソールサンプルです。

- `Seamless` パッケージが提供する Tsavorite / Persisted の永続ストアから起動時に選択
- FLinq source generator がストアの値列挙に対する `Where` / `OrderBy` / `OrderByDescending` / `ThenBy` / `Take` をコンパイル時に処理
- `Store.Save()` で保存し、次回起動時に復元
- 同一の銘柄データで稼働銘柄数と値上がり上位を表示

## 実行

NuGet フィードに本リポジトリのパッケージを配置した後、サンプルのフォルダで実行します。

```powershell
dotnet run -- tsavorite
dotnet run -- persisted
dotnet run -- tsavorite .\my-data
```

既定の保存先は実行ファイルの隣の `VirtualDbData/<backend>` です。明示した保存先は起動引数の2番目で指定します。`Store.Open` を利用するため、データを消す場合はサンプルを終了した後に該当フォルダを削除してください。

## ローカルパッケージを作成して使う

リポジトリのルートから、Release workflow と同じ11パッケージをローカルフィードへ出力します。

```powershell
$feed = Join-Path $PWD 'Work\VirtualDbFeed'
$projects = 'Seamless.Execution','Seamless.Collections','Seamless.Collections.Persisted','Seamless.Collections.Tsavorite','Seamless.Linq','Seamless.Database','Seamless.Database.Generator','Seamless.Database.Rdbms','Seamless.FLinq.Generator','Seamless','Seamless.Database.Tool'
foreach ($project in $projects) {
    dotnet pack "$project\$project.csproj" -c Release -m:1 -nodeReuse:false -p:Version=0.0.0 -o $feed
    if ($LASTEXITCODE -ne 0) { throw "pack failed: $project" }
}
dotnet nuget add source $feed --name seamless-local
dotnet run --project Samples\Sample.VirtualDb\Sample.VirtualDb.csproj -c Release -- tsavorite
```

`Work/VirtualDbFeed` は生成物の置き場で、Git 管理対象ではありません。実際に公開する場合は各 `.nupkg` を同じローカル NuGet フィードへ配置し、サンプルの `0.0.0` を公開バージョンへ合わせてください。
