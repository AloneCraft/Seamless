using System;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Text;
public static class TpcEFlatLoader {
    public static long Load(SqlConnection connection, string file, string table, string[] names, string[] types, bool[] nullable) {
        var data=new DataTable();
        for(int i=0;i<names.Length;i++) {
            string type=types[i];
            Type runtime=type.StartsWith("varchar") ? typeof(string) : type.StartsWith("varbinary") ? typeof(byte[]) : type=="bigint" ? typeof(long) : type=="int" ? typeof(int) : type=="bit" ? typeof(bool) : type.StartsWith("decimal") ? typeof(decimal) : typeof(DateTime);
            data.Columns.Add(names[i],runtime);
        }
        long total=0;
        using(var reader=new StreamReader(file,Encoding.UTF8,false,1048576))
        using(var bulk=new SqlBulkCopy(connection,SqlBulkCopyOptions.KeepNulls,null)) {
            int batchLimit=table=="NEWS_ITEM" ? 50 : 5000;
            bulk.DestinationTableName="dbo.["+table+"]";bulk.BatchSize=batchLimit;bulk.BulkCopyTimeout=0;
            foreach(string name in names) bulk.ColumnMappings.Add(name,name);
            string line;
            while((line=reader.ReadLine())!=null) {
                if(line.Length==0) continue;
                string[] fields=line.Split('|');
                if(fields.Length!=names.Length && !(fields.Length==names.Length+1 && fields[fields.Length-1]=="")) throw new FormatException(table+" row "+(total+1)+" has "+fields.Length+" fields, expected "+names.Length);
                var row=data.NewRow();
                for(int i=0;i<names.Length;i++) {
                    string value=fields[i],type=types[i];
                    if(value.Length==0 && nullable[i]) row[i]=DBNull.Value;
                    else if(type.StartsWith("varchar")) row[i]=value;
                    else if(type.StartsWith("varbinary")) row[i]=Encoding.UTF8.GetBytes(value);
                    else if(type=="bigint") row[i]=long.Parse(value,CultureInfo.InvariantCulture);
                    else if(type=="int") row[i]=int.Parse(value,CultureInfo.InvariantCulture);
                    else if(type=="bit") {if(value!="0" && value!="1") throw new FormatException("Invalid Boolean at "+table+" row "+(total+1));row[i]=value=="1";}
                    else if(type.StartsWith("decimal")) row[i]=decimal.Parse(value,NumberStyles.AllowLeadingSign|NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture);
                    else row[i]=DateTime.ParseExact(value,type=="date"?new[]{"yyyy-MM-dd"}:new[]{"yyyy-MM-dd HH:mm:ss.fff","yyyy-MM-dd HH:mm:ss"},CultureInfo.InvariantCulture,DateTimeStyles.None);
                }
                data.Rows.Add(row);total++;
                if(data.Rows.Count>=batchLimit) {bulk.WriteToServer(data);data.Clear();}
            }
            if(data.Rows.Count!=0) bulk.WriteToServer(data);
        }
        return total;
    }
}
