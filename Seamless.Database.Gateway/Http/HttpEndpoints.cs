using System.Text;
using System.Text.Json;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;

namespace Seamless.Database.Gateway.Http;

public static class HttpEndpoints{
    public static async Task HandleRequestAsync(
        global::System.Net.HttpListenerContext context,
        IDatabaseAdapter adapter,
        SqlExecutionEngine sqlEngine,
        CancellationToken cancellationToken=default){

        var req=context.Request;
        var res=context.Response;

        // CORS ヘッダ設定
        res.Headers.Add("Access-Control-Allow-Origin","*");
        res.Headers.Add("Access-Control-Allow-Methods","GET, POST, OPTIONS");
        res.Headers.Add("Access-Control-Allow-Headers","Content-Type");

        if(req.HttpMethod=="OPTIONS"){
            res.StatusCode=204;
            res.Close();
            return;
        }else{
        }

        var path=req.Url?.AbsolutePath??string.Empty;

        try{
            if(path=="/"||path=="/index.html"){
                await ServeHtmlAsync(res,adapter.DatabaseName,cancellationToken);
            }else if(path=="/api/tables"&&req.HttpMethod=="GET"){
                await HandleGetTablesAsync(res,adapter,cancellationToken);
            }else if(path.StartsWith("/api/tables/")&&req.HttpMethod=="GET"){
                var tableName=path.Substring("/api/tables/".Length);
                await HandleGetTableDataAsync(res,tableName,adapter,cancellationToken);
            }else if(path=="/api/sql"&&req.HttpMethod=="POST"){
                await HandlePostSqlAsync(req,res,adapter,sqlEngine,cancellationToken);
            }else{
                res.StatusCode=404;
                await WriteJsonAsync(res,new{error="Not Found"},cancellationToken);
            }
        }catch(Exception ex){
            res.StatusCode=500;
            await WriteJsonAsync(res,new{error=ex.Message},cancellationToken);
        }finally{
            res.Close();
        }
    }

    private static async Task HandleGetTablesAsync(global::System.Net.HttpListenerResponse res,IDatabaseAdapter adapter,CancellationToken ct){
        var tables=adapter.GetTables();
        var list=new List<object>();
        foreach(var t in tables){
            var cols=t.Columns.Select(c=>new{c.Name,c.SqlTypeName,c.IsPrimaryKey,c.IsNullable}).ToList();
            list.Add(new{name=t.TableName,columns=cols});
        }
        await WriteJsonAsync(res,list,ct);
    }

    private static async Task HandleGetTableDataAsync(global::System.Net.HttpListenerResponse res,string tableName,IDatabaseAdapter adapter,CancellationToken ct){
        using var session=new SessionContext(0,adapter.DatabaseName);
        var result=adapter.ExecuteSelect(tableName,null,null,session);
        var rows=new List<Dictionary<string,object?>>();
        foreach(var r in result.Rows){
            var dict=new Dictionary<string,object?>();
            for(var i=0;i<result.Columns.Count;i++){
                dict[result.Columns[i].Name]=r[i];
            }
            rows.Add(dict);
        }
        await WriteJsonAsync(res,new{table=tableName,total=rows.Count,rows=rows},ct);
    }

    private static async Task HandlePostSqlAsync(
        global::System.Net.HttpListenerRequest req,
        global::System.Net.HttpListenerResponse res,
        IDatabaseAdapter adapter,
        SqlExecutionEngine sqlEngine,
        CancellationToken ct){

        using var sr=new StreamReader(req.InputStream,req.ContentEncoding);
        var body=await sr.ReadToEndAsync(ct);
        var json=JsonDocument.Parse(body);
        if(!json.RootElement.TryGetProperty("query",out var queryProp)){
            res.StatusCode=400;
            await WriteJsonAsync(res,new{error="Missing 'query' property"},ct);
            return;
        }else{
        }

        var sql=queryProp.GetString()??string.Empty;
        using var session=new SessionContext(0,adapter.DatabaseName);
        var result=sqlEngine.Execute(sql,session);

        var colNames=result.Columns.Select(c=>c.Name).ToList();
        var rows=new List<Dictionary<string,object?>>();
        foreach(var r in result.Rows){
            var dict=new Dictionary<string,object?>();
            for(var i=0;i<result.Columns.Count;i++){
                dict[result.Columns[i].Name]=r[i];
            }
            rows.Add(dict);
        }

        await WriteJsonAsync(res,new{
            columns=colNames,
            rows=rows,
            rowsAffected=result.RowsAffected
        },ct);
    }

    private static async Task WriteJsonAsync(global::System.Net.HttpListenerResponse res,object data,CancellationToken ct){
        res.ContentType="application/json; charset=utf-8";
        var bytes=JsonSerializer.SerializeToUtf8Bytes(data,new JsonSerializerOptions{WriteIndented=true,Encoder=global::System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping});
        res.ContentLength64=bytes.Length;
        await res.OutputStream.WriteAsync(bytes,ct);
    }

    private static async Task ServeHtmlAsync(global::System.Net.HttpListenerResponse res,string dbName,CancellationToken ct){
        res.ContentType="text/html; charset=utf-8";
        var html=$@"<!DOCTYPE html>
<html lang=""ja"">
<head>
    <meta charset=""UTF-8"">
    <title>SeamlessDB Web UI - {dbName}</title>
    <style>
        body {{ font-family: 'Segoe UI', Meiryo, sans-serif; margin: 20px; background: #f8f9fa; color: #333; }}
        h1 {{ color: #0066cc; margin-bottom: 5px; }}
        .header {{ margin-bottom: 20px; border-bottom: 2px solid #0066cc; padding-bottom: 10px; }}
        .tables-bar {{ margin-bottom: 15px; display: flex; gap: 8px; flex-wrap: wrap; }}
        .btn {{ padding: 6px 14px; border: 1px solid #ccc; background: #fff; cursor: pointer; border-radius: 4px; font-size: 14px; }}
        .btn:hover {{ background: #e9ecef; }}
        .btn-primary {{ background: #0066cc; color: #fff; border-color: #0066cc; }}
        .btn-primary:hover {{ background: #0052a3; }}
        .sql-box {{ width: 100%; height: 80px; font-family: Consolas, monospace; font-size: 14px; padding: 8px; box-sizing: border-box; border: 1px solid #ccc; border-radius: 4px; }}
        .controls {{ margin: 10px 0 20px 0; }}
        table {{ border-collapse: collapse; width: 100%; background: #fff; box-shadow: 0 1px 3px rgba(0,0,0,0.1); }}
        th, td {{ border: 1px solid #dee2e6; padding: 8px 12px; text-align: left; font-size: 14px; }}
        th {{ background: #f1f3f5; font-weight: bold; }}
        tr:nth-child(even) {{ background: #fdfdfd; }}
        .status {{ margin-top: 8px; color: #666; font-size: 13px; }}
    </style>
</head>
<body>
    <div class=""header"">
        <h1>SeamlessDB Web UI</h1>
        <div>Database: <strong>{dbName}</strong> | TDS Port: <strong>14333</strong> | HTTP Port: <strong>8088</strong></div>
    </div>
    <div class=""tables-bar"" id=""tableButtons""></div>
    <textarea class=""sql-box"" id=""sqlInput"">SELECT * FROM 商品;</textarea>
    <div class=""controls"">
        <button class=""btn btn-primary"" onclick=""runSql()"">SQL実行</button>
        <span class=""status"" id=""statusText""></span>
    </div>
    <div id=""tableContainer""></div>
    <script>
        async function loadTables() {{
            const res = await fetch('/api/tables');
            const tables = await res.json();
            const bar = document.getElementById('tableButtons');
            bar.innerHTML = '<strong>テーブル:</strong> ';
            tables.forEach(t => {{
                const btn = document.createElement('button');
                btn.className = 'btn';
                btn.textContent = t.name;
                btn.onclick = () => {{
                    document.getElementById('sqlInput').value = 'SELECT * FROM ' + t.name + ';';
                    runSql();
                }};
                bar.appendChild(btn);
            }});
        }}
        async function runSql() {{
            const sql = document.getElementById('sqlInput').value;
            const status = document.getElementById('statusText');
            status.textContent = '実行中...';
            try {{
                const res = await fetch('/api/sql', {{
                    method: 'POST',
                    headers: {{ 'Content-Type': 'application/json' }},
                    body: JSON.stringify({{ query: sql }})
                }});
                const data = await res.json();
                if(data.error) {{
                    status.textContent = 'エラー: ' + data.error;
                    document.getElementById('tableContainer').innerHTML = '';
                    return;
                }}
                status.textContent = '完了: ' + data.rows.length + ' 件取得 (影響行数: ' + data.rowsAffected + ')';
                renderTable(data.columns, data.rows);
            }} catch(err) {{
                status.textContent = '通信エラー: ' + err.message;
            }}
        }}
        function renderTable(cols, rows) {{
            if(!cols || cols.length === 0) {{
                document.getElementById('tableContainer').innerHTML = '<p>結果セットはありません</p>';
                return;
            }}
            let html = '<table><thead><tr>';
            cols.forEach(c => html += '<th>' + c + '</th>');
            html += '</tr></thead><tbody>';
            rows.forEach(r => {{
                html += '<tr>';
                cols.forEach(c => html += '<td>' + (r[c] !== null && r[c] !== undefined ? r[c] : '<em>NULL</em>') + '</td>');
                html += '</tr>';
            }});
            html += '</tbody></table>';
            document.getElementById('tableContainer').innerHTML = html;
        }}
        loadTables();
        runSql();
    </script>
</body>
</html>";

        var bytes=Encoding.UTF8.GetBytes(html);
        res.ContentLength64=bytes.Length;
        await res.OutputStream.WriteAsync(bytes,ct);
    }
}
