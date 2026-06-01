using ZLinq;

var skip = 0;
var take = 0;

var data = new int[] { 1, 2, 3 };

var expected = data.AsEnumerable()
                   .Order()
                   .Skip(skip)
                   .Take(take)
                   .ToArray();

var results = data.AsValueEnumerable()
                  .Order()
                  .Skip(skip)
                  .Take(take);


var r = results.ToArray();

Console.WriteLine("foo");
