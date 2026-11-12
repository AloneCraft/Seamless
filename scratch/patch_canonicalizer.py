with open('Benchmark/Benchmark.Dynamic.Core/ElementCanonicalizer.cs', 'rb') as f:
    text = f.read().decode('utf-8')

target = '            CollidingKey item=>"K:"+item.Value.ToString(CultureInfo.InvariantCulture),'
replacement = target + """
            bool item => "b:" + (item ? "1" : "0"),
            System.Runtime.CompilerServices.ITuple tuple => "T:" + string.Join(";", Enumerable.Range(0, tuple.Length).Select(i => Canonical(tuple[i]))),
            Realistic.Trade item => $"Tr:{item.Id},{item.AccountId},{item.Symbol},{item.Quantity},{DecimalBits(item.Price)},{(byte)item.Status},{item.TimestampTicks},{item.IsCash}",
            Realistic.Account item => $"Ac:{item.Id},{item.CustomerId},{item.BrokerId},{item.Name},{DecimalBits(item.Balance)},{item.Tier}",
            Realistic.Security item => $"Sc:{item.Symbol},{item.Sector},{item.CompanyId},{DecimalBits(item.LastPrice)}",
            Realistic.Customer item => $"Cu:{item.Id},{item.Name},{item.City},{item.Age},{item.IsPremium}",
            Realistic.SalesOrder item => $"So:{item.Id},{item.CustomerId},{DecimalBits(item.Amount)},{item.OrderedAtTicks},{(int)item.Status},{item.CouponCode}","""

assert target in text, 'target not found'
text = text.replace(target, replacement, 1)

with open('Benchmark/Benchmark.Dynamic.Core/ElementCanonicalizer.cs', 'wb') as f:
    f.write(text.encode('utf-8'))
print('Successfully patched ElementCanonicalizer.cs')
