using System.Linq;

var numbers = new List<int> { 1, 2, 3, 4, 5 };

Console.WriteLine("non-capturing");
var fastQuery = numbers.Where(n => n > 2);
foreach (var number in fastQuery)
{
    Console.WriteLine(number);
}

Console.WriteLine("capturing");
var threshold = 3;
var capturedQuery = numbers.Where(n => n > threshold);
foreach (var number in capturedQuery)
{
    Console.WriteLine(number);
}
