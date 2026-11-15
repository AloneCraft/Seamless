using F=Seamless.Database.SqlServer.Functions.MathematicalFunctions;
using static Database.SqlServer.Functions.Tests.SqlServerOracle;
namespace Database.SqlServer.Functions.Tests;
public sealed class MathematicalFunctions{
    [Fact,Trait("Category","Unit")]public void Round(){
        {
            //桁数を使い、半分は0から遠い方へ(LinqDB は桁数を無視して切り上げていた)
            Assert.Equal(123.5m,F.Round(123.456m,1));
            Assert.Equal(-123.5m,F.Round(-123.45m,1));
        }
        {
            //負の桁数は小数点より左を丸める
            Assert.Equal(200m,F.Round(150.75m,-2));
        }
        {
            //3つ目の引数が0以外なら切り捨て
            Assert.Equal(123.4m,F.Round(123.456m,1,1));
        }
    }
    [SqlServerFact,Trait("Category","SqlServer")]public void MatchesSqlServer(){
        Check(
            ("ABS(-5)",()=>F.Abs(-5)),
            ("ABS(CAST(-2147483648 AS int))",()=>F.Abs(int.MinValue)),
            ("ABS(-1.5)",()=>F.Abs(-1.5m)),
            ("SIGN(-3)",()=>F.Sign(-3)),
            ("SIGN(CAST(0 AS float))",()=>F.Sign(0d)),
            ("CEILING(-1.5)",()=>F.Ceiling(-1.5m)),
            ("FLOOR(-1.5)",()=>F.Floor(-1.5m)),
            ("CEILING(CAST(1.2 AS float))",()=>F.Ceiling(1.2d)),
            ("ROUND(123.456,1)",()=>F.Round(123.456m,1)),
            ("ROUND(123.456,-1)",()=>F.Round(123.456m,-1)),
            ("ROUND(150.75,-2)",()=>F.Round(150.75m,-2)),
            ("ROUND(123.456,1,1)",()=>F.Round(123.456m,1,1)),
            ("ROUND(-123.45,1)",()=>F.Round(-123.45m,1)),
            ("ROUND(CAST(2.5 AS float),0)",()=>F.Round(2.5d,0)),
            ("ROUND(1234,-2)",()=>F.Round(1234,-2)),
            ("POWER(2,10)",()=>F.Power(2,10d)),
            ("POWER(2,-1)",()=>F.Power(2,-1d)),
            ("POWER(2,0.5)",()=>F.Power(2,0.5d)),
            ("POWER(CAST(2 AS float),0.5)",()=>F.Power(2d,0.5d)),
            ("POWER(2,40)",()=>F.Power(2,40d)),
            ("SQRT(16)",()=>F.Sqrt(16)),
            ("SQRT(-1)",()=>F.Sqrt(-1)),
            ("SQUARE(3)",()=>F.Square(3)),
            ("EXP(1)",()=>F.Exp(1)),
            ("LOG(0)",()=>F.Log(0)),
            ("LOG(8,2)",()=>F.Log(8,2)),
            ("LOG10(1000)",()=>F.Log10(1000)),
            ("PI()",()=>F.Pi()),
            ("SIN(1)",()=>F.Sin(1)),
            ("ASIN(2)",()=>F.Asin(2)),
            ("ATN2(1,1)",()=>F.Atn2(1,1)),
            ("COT(1)",()=>F.Cot(1)),
            ("DEGREES(PI())",()=>F.Degrees(Math.PI)),
            ("RADIANS(180)",()=>F.Radians(180)),
            ("RADIANS(CAST(180 AS float))",()=>F.Radians(180d)),
            ("DEGREES(1)",()=>F.Degrees(1))
        );
    }
}
