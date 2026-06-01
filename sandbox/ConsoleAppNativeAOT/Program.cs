

using ZLinq;

var source = ValueEnumerable.Range(1, 100)
    .Where(x => x % 2 == 0)
    .Skip(10)
    .Select(x => x * 100);

foreach (var item in source)
{
    Console.WriteLine(item);
}
