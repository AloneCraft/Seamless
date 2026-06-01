using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Channels;
using System.Text;
using System.Threading.Tasks;
using ZLinq;

[assembly: ZLinq.ZLinqDropInAttribute("", ZLinq.DropInGenerateTypes.Everything, DisableEmitSource = false)]

namespace ConsoleAppNetFramework48
{
    class Program
    {
        static unsafe void Main(string[] args)
        {
            delegate* managed<int, int, int> p2 = &M;


            var r = p2(10, 20);

            Console.WriteLine(r);


            var seq = ValueEnumerable.Range(1, 10);
            foreach (var item in seq)
            {
                Console.WriteLine(item);
            }
        }

        public static int M(int x, int y) => x * y;
    }
}
