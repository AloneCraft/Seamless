using Benchmark.TpcE.Core;
namespace Benchmark.TpcE.Compliance;
/// <summary>
/// EGenLoader(フラットファイル出力)が書いた 33 個の `.txt` を読んで、既存の <see cref="ILoadSink"/> へ流す。
/// 列の並びは TPC-E 仕様 2.2 の表の定義どおり。値の表し方は既存の合成データと同じ(日付は日番号、日時は 0001-01-01 からのミリ秒、真偽は 1/0)。
/// </summary>
internal sealed class EGenFlatFileImporter(string directory){
    private delegate void LineLoader(FlatLine line,ILoadSink sink);
    private static readonly (string File,LineLoader Load)[] Tables=[
        ("Sector.txt",static (line,sink)=>sink.Load(new SectorRow(line.Code8(0),line.Text(1)))),
        ("Industry.txt",static (line,sink)=>sink.Load(new IndustryRow(line.Code8(0),line.Text(1),line.Code8(2)))),
        ("StatusType.txt",static (line,sink)=>sink.Load(new StatusTypeRow(line.Code8(0),line.Text(1)))),
        ("TaxRate.txt",static (line,sink)=>sink.Load(new TaxrateRow(line.Code8(0),line.Text(1),line.Decimal(2)))),
        ("TradeType.txt",static (line,sink)=>sink.Load(new TradeTypeRow(line.Code8(0),line.Text(1),line.Flag(2),line.Flag(3)))),
        ("ZipCode.txt",static (line,sink)=>sink.Load(new ZipCodeRow(line.Code16(0),line.Text(1),line.Text(2)))),
        ("Charge.txt",static (line,sink)=>sink.Load(new ChargeRow(line.Code8(0),line.Long(1),line.Decimal(2)))),
        ("CommissionRate.txt",static (line,sink)=>sink.Load(new CommissionRateRow(line.Long(0),line.Code8(1),line.Code8(2),line.Long(3),line.Long(4),line.Decimal(5)))),
        ("Exchange.txt",static (line,sink)=>sink.Load(new ExchangeRow(line.Code8(0),line.Text(1),line.Int(2),line.Int(3),line.Int(4),line.OptionalText(5),line.Long(6)))),
        ("Address.txt",static (line,sink)=>sink.Load(new AddressRow(line.Long(0),line.OptionalText(1),line.OptionalText(2),line.Code16(3),line.OptionalText(4)))),
        ("Company.txt",static (line,sink)=>sink.Load(new CompanyRow(line.Long(0),line.Code8(1),line.Text(2),line.Code8(3),line.Text(4),line.Text(5),line.Long(6),line.Text(7),line.Date(8)))),
        ("CompanyCompetitor.txt",static (line,sink)=>sink.Load(new CompanyCompetitorRow(line.Long(0),line.Long(1),line.Code8(2)))),
        ("Security.txt",static (line,sink)=>sink.Load(new SecurityRow(line.Code16(0),line.Code8(1),line.Code8(2),line.Text(3),line.Code8(4),line.Long(5),line.Long(6),line.Date(7),line.Date(8),line.Decimal(9),line.Decimal(10),line.Date(11),line.Decimal(12),line.Date(13),line.Decimal(14),line.Decimal(15)))),
        ("Financial.txt",static (line,sink)=>sink.Load(new FinancialRow(line.Long(0),line.Long(1),line.Long(2),line.Date(3),line.Decimal(4),line.Decimal(5),line.Decimal(6),line.Decimal(7),line.Decimal(8),line.Decimal(9),line.Decimal(10),line.Decimal(11),line.Long(12),line.Long(13)))),
        ("LastTrade.txt",static (line,sink)=>sink.Load(new LastTradeRow(line.Code16(0),line.DateTime(1),line.Decimal(2),line.Decimal(3),line.Long(4)))),
        ("DailyMarket.txt",static (line,sink)=>sink.Load(new DailyMarketRow(line.Date(0),line.Code16(1),line.Decimal(2),line.Decimal(3),line.Decimal(4),line.Long(5)))),
        ("NewsItem.txt",static (line,sink)=>sink.Load(new NewsItemRow(line.Long(0),line.Text(1),line.Text(2),line.Utf8(3),line.DateTime(4),line.Text(5),line.OptionalText(6)))),
        ("NewsXRef.txt",static (line,sink)=>sink.Load(new NewsXrefRow(line.Long(0),line.Long(1)))),
        ("Broker.txt",static (line,sink)=>sink.Load(new BrokerRow(line.Long(0),line.Code8(1),line.Text(2),line.Long(3),line.Decimal(4)))),
        ("Customer.txt",static (line,sink)=>sink.Load(new CustomerRow(
            line.Long(0),line.Code24(1),line.Code8(2),line.Text(3),line.Text(4),line.OptionalText(5),line.OptionalText(6),line.Int(7),line.Date(8),line.Long(9),
            line.OptionalText(10),line.OptionalText(11),line.OptionalText(12),line.OptionalText(13),
            line.OptionalText(14),line.OptionalText(15),line.OptionalText(16),line.OptionalText(17),
            line.OptionalText(18),line.OptionalText(19),line.OptionalText(20),line.OptionalText(21),
            line.OptionalText(22),line.OptionalText(23)))),
        ("CustomerTaxrate.txt",static (line,sink)=>sink.Load(new CustomerTaxrateRow(line.Code8(0),line.Long(1)))),
        ("CustomerAccount.txt",static (line,sink)=>sink.Load(new CustomerAccountRow(line.Long(0),line.Long(1),line.Long(2),line.OptionalText(3),line.Int(4),line.Decimal(5)))),
        ("AccountPermission.txt",static (line,sink)=>sink.Load(new AccountPermissionRow(line.Long(0),line.Code8(1),line.Code24(2),line.Text(3),line.Text(4)))),
        ("WatchList.txt",static (line,sink)=>sink.Load(new WatchListRow(line.Long(0),line.Long(1)))),
        ("WatchItem.txt",static (line,sink)=>sink.Load(new WatchItemRow(line.Long(0),line.Code16(1)))),
        ("Trade.txt",static (line,sink)=>sink.Load(new TradeRow(
            line.Long(0),line.DateTime(1),line.Code8(2),line.Code8(3),line.Flag(4),line.Code16(5),line.Int(6),line.Decimal(7),line.Long(8),line.Text(9),
            line.OptionalDecimal(10),line.Decimal(11),line.Decimal(12),line.Decimal(13),line.Flag(14)))),
        ("TradeHistory.txt",static (line,sink)=>sink.Load(new TradeHistoryRow(line.Long(0),line.DateTime(1),line.Code8(2)))),
        ("TradeRequest.txt",static (line,sink)=>sink.Load(new TradeRequestRow(line.Long(0),line.Code8(1),line.Code16(2),line.Int(3),line.Decimal(4),line.Long(5)))),
        ("Settlement.txt",static (line,sink)=>sink.Load(new SettlementRow(line.Long(0),line.Text(1),line.Date(2),line.Decimal(3)))),
        ("CashTransaction.txt",static (line,sink)=>sink.Load(new CashTransactionRow(line.Long(0),line.DateTime(1),line.Decimal(2),line.OptionalText(3)))),
        ("Holding.txt",static (line,sink)=>sink.Load(new HoldingRow(line.Long(0),line.Long(1),line.Code16(2),line.DateTime(3),line.Decimal(4),line.Int(5)))),
        ("HoldingHistory.txt",static (line,sink)=>sink.Load(new HoldingHistoryRow(line.Long(0),line.Long(1),line.Int(2),line.Int(3)))),
        ("HoldingSummary.txt",static (line,sink)=>sink.Load(new HoldingSummaryRow(line.Long(0),line.Code16(1),line.Int(2)))),
    ];
    /// <summary>33 表の数。仕様の表の数と一致することをテストで確かめる。</summary>
    public static int TableCount=>Tables.Length;
    /// <summary>全ファイルを読んで <paramref name="sink"/> へ流す。表ごとの行数を返す。</summary>
    public IReadOnlyDictionary<string,long> Import(ILoadSink sink,Action<string,long>? progress=null){
        var counts=new Dictionary<string,long>(StringComparer.Ordinal);
        var line=new FlatLine();
        foreach(var (file,load) in Tables){
            var path=Path.Combine(directory,file);
            if(!File.Exists(path)&&file=="TradeRequest.txt"){
                // 初期データには未約定の注文が無いので、EGen はこのファイルを出さない。
                counts.Add(file,0);
                continue;
            }
            if(!File.Exists(path)){
                throw new FileNotFoundException($"EGen output {file} was not found in {directory}.",path);
            }
            long rows=0;
            using var reader=new StreamReader(path,System.Text.Encoding.UTF8,false,1<<20);
            string? text;
            while((text=reader.ReadLine()) is not null){
                if(text.Length==0){
                    continue;
                }
                line.Set(text);
                load(line,sink);
                rows++;
            }
            counts.Add(file,rows);
            progress?.Invoke(file,rows);
        }
        return counts;
    }
}
