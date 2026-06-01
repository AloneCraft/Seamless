
using ZLinq;

[assembly: ZLinq.ZLinqDropInAttribute("MyApp", ZLinq.DropInGenerateTypes.Everything, DisableEmitSource = false)]


var items = new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 5, 12, 13, 14, 15 };


var standardLinq = items.Skip(1).Where(x => x % 2 == 0).Take(100).Select(x => x * x);
var zlinq = items.AsValueEnumerable().Skip(1).Where(x => x % 2 == 0).Take(100).Select(x => x * x);
foreach (var item in zlinq)
{

}



Enumerable.Range(1, 10).AsValueEnumerable().ToList();
Enumerable.Range(1, 10).AsValueEnumerable().ToList();

//for (int i = 0; i < 100000; i++)
//{
//    if (i % 100 == 0) Console.WriteLine(i);
//    Enumerable.Range(1, i).AsValueEnumerable().ToList();
//    Enumerable.Range(1, i).ToArray().AsValueEnumerable().ToList();
//}
