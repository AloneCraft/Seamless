using Seamless.Database.SqlServer.Functions;
using F=Seamless.Database.SqlServer.Functions.StringFunctions;
using static Database.SqlServer.Functions.Tests.SqlServerOracle;
namespace Database.SqlServer.Functions.Tests;
public sealed class StringFunctions{
    private const string Ci=" COLLATE SQL_Latin1_General_CP1_CI_AS";
    private const string Bin=" COLLATE Latin1_General_BIN2";
    private static SqlCollation Japanese=>SqlCollation.Get("Japanese_CI_AS");
    [Fact,Trait("Category","Unit")]public void Replace(){
        {
            //大文字小文字を区別しない照合順序では区別せずに置き換える
            Assert.Equal("xbcxBC",F.Replace("abcABC","a","x",Latin));
        }
        {
            //_BIN2 では区別する
            Assert.Equal("xbcABC",F.Replace("abcABC","a","x",Binary));
        }
    }
    [Fact,Trait("Category","Unit")]public void Soundex(){
        {
            //SQL Server では H と W も前の子音の番号を切る
            Assert.Equal("A226",F.Soundex("Ashcraft"));
            Assert.Equal("R163",F.Soundex("Robert"));
        }
    }
    [SqlServerFact,Trait("Category","SqlServer")]public void MatchesSqlServer(){
        Check(
            ("ASCII('A')",()=>F.Ascii("A",Latin)),
            ("ASCII('')",()=>F.Ascii("",Latin)),
            ("ASCII(CAST(N'é'"+Ci+" AS varchar(10)))",()=>F.Ascii("é",Latin)),
            ("CHAR(65)",()=>F.Char(65,Latin)),
            ("CHAR(256)",()=>F.Char(256,Latin)),
            ("CHAR(233)",()=>F.Char(233,Japanese)),
            ("CHAR(97)",()=>F.Char(97,Japanese)),
            ("NCHAR(12354)",()=>F.NChar(12354)),
            ("UNICODE(N'あ')",()=>F.Unicode("あ")),
            ("LEN('abc   ')",()=>F.Len("abc   ")),
            ("LEN('   ')",()=>F.Len("   ")),
            ("DATALENGTH(N'abc ')",()=>F.DataLength("abc ",true,Latin)),
            ("DATALENGTH(CAST('abc ' AS varchar(10)))",()=>F.DataLength("abc ",false,Latin)),
            ("LEFT('abcdef',3)",()=>F.Left("abcdef",3)),
            ("LEFT('abc',10)",()=>F.Left("abc",10)),
            ("LEFT('abc',-1)",()=>F.Left("abc",-1)),
            ("RIGHT('abcdef',2)",()=>F.Right("abcdef",2)),
            ("RIGHT('abc',-1)",()=>F.Right("abc",-1)),
            ("SUBSTRING('abcdef',2,3)",()=>F.Substring("abcdef",2,3)),
            ("SUBSTRING('abcdef',0,3)",()=>F.Substring("abcdef",0,3)),
            ("SUBSTRING('abcdef',-1,3)",()=>F.Substring("abcdef",-1,3)),
            ("SUBSTRING('abcdef',10,3)",()=>F.Substring("abcdef",10,3)),
            ("SUBSTRING('abcdef',2,-1)",()=>F.Substring("abcdef",2,-1)),
            ("LTRIM('  a  ')",()=>F.LTrim("  a  ")),
            ("RTRIM('  a  ')",()=>F.RTrim("  a  ")),
            ("TRIM('  a  ')",()=>F.Trim("  a  ")),
            ("TRIM('x' FROM 'xxaxx')",()=>F.Trim("xxaxx","x")),
            ("SPACE(3)",()=>F.Space(3)),
            ("SPACE(-1)",()=>F.Space(-1)),
            ("UPPER(N'abcé'"+Ci+")",()=>F.Upper("abcé",Latin)),
            ("LOWER(N'ABCÉ'"+Ci+")",()=>F.Lower("ABCÉ",Latin)),
            ("CHARINDEX('B','abcabc'"+Ci+")",()=>F.CharIndex("B","abcabc",Latin)),
            ("CHARINDEX('B','abcabc'"+Bin+")",()=>F.CharIndex("B","abcabc",Binary)),
            ("CHARINDEX('b','abcabc'"+Ci+",3)",()=>F.CharIndex("b","abcabc",Latin,3)),
            ("CHARINDEX('b','abcabc'"+Ci+",0)",()=>F.CharIndex("b","abcabc",Latin,0)),
            ("CHARINDEX('b','abcabc'"+Ci+",10)",()=>F.CharIndex("b","abcabc",Latin,10)),
            ("CHARINDEX('','abc'"+Ci+")",()=>F.CharIndex("","abc",Latin)),
            ("PATINDEX('%[0-9]%','ab12c'"+Ci+")",()=>F.PatIndex("%[0-9]%","ab12c",Latin)),
            ("PATINDEX('%b_d%','abcd'"+Ci+")",()=>F.PatIndex("%b_d%","abcd",Latin)),
            ("PATINDEX('ab%','abcd'"+Ci+")",()=>F.PatIndex("ab%","abcd",Latin)),
            ("PATINDEX('%z%','abcd'"+Ci+")",()=>F.PatIndex("%z%","abcd",Latin)),
            ("REPLACE('abcABC'"+Ci+",'a','x')",()=>F.Replace("abcABC","a","x",Latin)),
            ("REPLACE('abcABC'"+Bin+",'a','x')",()=>F.Replace("abcABC","a","x",Binary)),
            ("REPLACE('abc'"+Ci+",'','x')",()=>F.Replace("abc","","x",Latin)),
            ("REPLACE('aaa'"+Ci+",'aa','b')",()=>F.Replace("aaa","aa","b",Latin)),
            ("STUFF('abcdef',2,3,'XY')",()=>F.Stuff("abcdef",2,3,"XY")),
            ("STUFF('abcdef',0,1,'X')",()=>F.Stuff("abcdef",0,1,"X")),
            ("STUFF('abcdef',7,1,'X')",()=>F.Stuff("abcdef",7,1,"X")),
            ("STUFF('abcdef',6,10,'X')",()=>F.Stuff("abcdef",6,10,"X")),
            ("STUFF('abcdef',2,-1,'X')",()=>F.Stuff("abcdef",2,-1,"X")),
            ("STUFF('abcdef',2,2,NULL)",()=>F.Stuff("abcdef",2,2,null)),
            ("TRANSLATE('2*[3+4]','[]','()')",()=>F.Translate("2*[3+4]","[]","()")),
            ("TRANSLATE('abc','ab','x')",()=>F.Translate("abc","ab","x")),
            ("REPLICATE('ab',3)",()=>F.Replicate("ab",3)),
            ("REPLICATE('ab',-1)",()=>F.Replicate("ab",-1)),
            ("REVERSE('abc')",()=>F.Reverse("abc")),
            ("CONCAT('a',NULL,'b')",()=>F.Concat("a",null,"b")),
            ("CONCAT_WS(',','a',NULL,'b')",()=>F.ConcatWs(",","a",null,"b")),
            ("QUOTENAME('a]b')",()=>F.QuoteName("a]b")),
            ("QUOTENAME('a''b','''')",()=>F.QuoteName("a'b","'")),
            ("QUOTENAME('ab','x')",()=>F.QuoteName("ab","x")),
            ("QUOTENAME(REPLICATE('a',129))",()=>F.QuoteName(new string('a',129))),
            ("STR(123.456)",()=>F.Str(123.456)),
            ("STR(123.456,8,2)",()=>F.Str(123.456,8,2)),
            ("STR(123.456,5,2)",()=>F.Str(123.456,5,2)),
            ("STR(123456,3)",()=>F.Str(123456,3)),
            ("STR(-2.5)",()=>F.Str(-2.5)),
            ("STR(0.5,3)",()=>F.Str(0.5,3)),
            ("SOUNDEX('Robert'"+Ci+")",()=>F.Soundex("Robert")),
            ("SOUNDEX('Rupert'"+Ci+")",()=>F.Soundex("Rupert")),
            ("SOUNDEX('Ashcraft'"+Ci+")",()=>F.Soundex("Ashcraft")),
            ("SOUNDEX('Tymczak'"+Ci+")",()=>F.Soundex("Tymczak")),
            ("SOUNDEX('Pfister'"+Ci+")",()=>F.Soundex("Pfister")),
            ("SOUNDEX('Lee'"+Ci+")",()=>F.Soundex("Lee")),
            ("SOUNDEX(''"+Ci+")",()=>F.Soundex("")),
            ("SOUNDEX('123'"+Ci+")",()=>F.Soundex("123")),
            ("SOUNDEX('1abc'"+Ci+")",()=>F.Soundex("1abc")),
            ("SOUNDEX('  Robert'"+Ci+")",()=>F.Soundex("  Robert")),
            ("SOUNDEX('ab-cd'"+Ci+")",()=>F.Soundex("ab-cd")),
            ("SOUNDEX('Shaw'"+Ci+")",()=>F.Soundex("Shaw")),
            ("DIFFERENCE('Green'"+Ci+",'Greene'"+Ci+")",()=>F.Difference("Green","Greene")),
            ("DIFFERENCE('Smith'"+Ci+",'Smythe'"+Ci+")",()=>F.Difference("Smith","Smythe")),
            ("DIFFERENCE('Robert'"+Ci+",'Rupert'"+Ci+")",()=>F.Difference("Robert","Rupert")),
            ("DIFFERENCE('Apple'"+Ci+",'Zebra'"+Ci+")",()=>F.Difference("Apple","Zebra")),
            ("FORMAT(1234.5,'N2','en-US')",()=>F.Format(Session,1234.5,"N2","en-US")),
            ("FORMAT(CAST('2020-01-02' AS date),'yyyy/MM/dd','en-US')",()=>F.Format(Session,new DateTime(2020,1,2),"yyyy/MM/dd","en-US")),
            ("FORMAT(12,'D5')",()=>F.Format(Session,12,"D5"))
        );
    }
}
