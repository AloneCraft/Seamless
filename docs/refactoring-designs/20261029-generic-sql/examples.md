# 利用例：実在と提案の区別

## 既存の型付きC#・FLinq経路
以下は既存AggregateJoin.cs.templateと同じAPI形。databaseは生成済みfixtureの型付きDBで、コードはクエリモジュール内の通常C#としてFLinqを含めてビルドする。SQL文字列を生成しない。
```csharp
using Seamless.Execution;
using Seamless.Linq;
using var scope=new ExecutionScope(new(){CancellationToken=cancellationToken});
foreach(var parent in scope.Query(database.dbo.FixtureParent.AsEnumerable()).OrderBy(row=>row.Id)){
    cancellationToken.ThrowIfCancellationRequested();
    Console.WriteLine(parent.Id);
}
```
この抜粋自体を単独でコンパイルしたとは扱わない。実際のDB型/引数/MemoryPack結果/ビルド入口は既存TestSeamless/QueriesとQueryModuleWriterを参照する。native remoteは既存NativeQueryClientと生成TypedClientでquery ID+引数を送り、DLLは管理者が事前配置する。

## 提案ADO.NET（未実装）
```csharp
using Seamless.AdoNet;
using var connection=new SeamlessConnection(
    @"Data Source=D:\SeamlessData\Shop;Dialect=Portable");
await connection.OpenAsync(cancellationToken);
using var command=connection.CreateCommand();
command.CommandText="SELECT Id,Name FROM Customers WHERE Age>=@age ORDER BY Id";
var parameter=command.CreateParameter();
parameter.ParameterName="@age";
parameter.DbType=System.Data.DbType.Int32;
parameter.Value=20;
command.Parameters.Add(parameter);
using var reader=await command.ExecuteReaderAsync(cancellationToken);
while(await reader.ReadAsync(cancellationToken)){
    Console.WriteLine($"{reader.GetInt32(0)}:{reader.GetString(1)}");
}
```
前提はCustomers(Id,Name,Age)をC#または移行ツールで構築済みであること。test.dbというファイル名だけで動的schemaの作成を意味しない。SQL Server等へのnetwork比較では各DBの公式ADO Providerを使い、上のSeamlessConnectionはインプロセスを測る。

## 同等の型付き照会
```csharp
var query=database.Customers.Where(customer=>customer.Age>=20)
    .OrderBy(customer=>customer.Id)
    .Select(customer=>new{customer.Id,customer.Name});
foreach(var row in query){Console.WriteLine($"{row.Id}:{row.Name}");}
```
database.Customersは例のアプリケーションモデルのpropertyで製品固定APIではない。FLinq適用は生成interceptorと実行結果で確認する。SQL側のstring/collationと.NETの比較が異なる場合、このまま同等とは判定せず比較子またはworkload条件を明示する。

## 共通SQL例と方言差
```sql
SELECT c.Id, COUNT(o.Id) AS OrderCount, SUM(o.Amount) AS Total
FROM Customers AS c
LEFT JOIN Orders AS o ON o.CustomerId=c.Id
WHERE c.Age>=20
GROUP BY c.Id
ORDER BY c.Id;
```
COUNT(*)への置換は外部結合の意味を変えるため行わない。ページングはSQL ServerのORDER BY/OFFSET/FETCH、MySQL/PGのORDER BY/LIMIT/OFFSETを別ファイルにして理由を記録する。SQL Serverの@age、PG wireの$1等の違いはparameter bindingで扱い、値を文字列連結しない。

## 将来のpackage利用（提案・未公開）
```xml
<ItemGroup>
  <PackageReference Include="Seamless.AdoNet" Version="$(SeamlessVersion)" />
  <PackageReference Include="Seamless.Compatibility.SqlServer" Version="$(SeamlessVersion)" />
</ItemGroup>
```
これは存在するpackageのインストール案内ではない。nativeだけを使うアプリはSQL/通信packageを追加する必要がない。有償GSPを標準依存へ追加しない。現行TestSeamlessは既存Database.SqlServer/Gateway/Client等を同一版のPackageReferenceで利用する。

## 既存SQL Server比較の操作（既存入口、全工程の合格は未完）
```powershell
$env:ConnectionStrings__Source='<環境変数またはsecretで解決した接続文字列>'
pwsh -File ./TestSeamless/Invoke-Comparison.ps1 -Operation Pack -Configuration Release
pwsh -File ./TestSeamless/Invoke-Comparison.ps1 -Operation Rebuild -Configuration Release
pwsh -File ./TestSeamless/Invoke-Comparison.ps1 -Operation Compare -Configuration Release
```
元DBへの接続資格情報を文書・世代へ保存しない。比較用DBはreadonly、CRUDは専用fixture。将来の3DB比較も同じcase IDとdialect別SQLを選んで実行し、in-processとremoteは別列で報告する。

## GUI操作例
SSMSは検証済み世代のTDSへ接続しDB→schema→table/columnを列挙、共通SQL照会、fixtureでCOMMIT/ROLLBACKを行う。実UIで接続・操作できたことを証跡にする。WorkbenchはMySQL wire、pgAdminはPG wire、DBeaverは選択driverと対応wireが必要。パーサー導入やsocket接続だけではこれらのGUI対応完了にならない。現在MySQL/PG wireとそのGUI手順の実行結果は未実装/未検証。
