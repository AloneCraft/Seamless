[CmdletBinding()]
param(
    [string]$Endpoint='http://127.0.0.1:64342/stream',
    [string]$Tool,
    [hashtable]$Arguments=@{},
    [string]$ProjectPath,
    [int]$TimeoutSeconds=60
)
$ErrorActionPreference='Stop'
$uri=[Uri]$Endpoint
if(-not $uri.IsLoopback){throw 'Use the local DataGrip MCP server.'}
$headers=@{Accept='application/json, text/event-stream';'MCP-Protocol-Version'='2025-03-26'}
function Invoke-Rpc([int]$Id,[string]$Method,[hashtable]$Parameters){
    $body=@{jsonrpc='2.0';id=$Id;method=$Method;params=$Parameters} | ConvertTo-Json -Depth 40 -Compress
    $response=Invoke-WebRequest -Uri $Endpoint -Method Post -ContentType 'application/json' -Headers $headers -Body $body -TimeoutSec $TimeoutSeconds
    if($response.Headers.ContainsKey('mcp-session-id')){$headers['Mcp-Session-Id']=[string]$response.Headers['mcp-session-id'][0]}
    $document=$response.Content | ConvertFrom-Json -Depth 60
    if($document.error){throw ($document.error | ConvertTo-Json -Depth 10 -Compress)}
    return $document.result
}
$session=$null
try{
    $initial=Invoke-Rpc 1 'initialize' @{protocolVersion='2025-03-26';capabilities=@{};clientInfo=@{name='Seamless-DataGrip';version='0.1'}}
    $session=$headers['Mcp-Session-Id']
    if(-not $Tool){Invoke-Rpc 2 'tools/list' @{};return}
    if($ProjectPath){$Arguments['projectPath']=$ProjectPath}
    $result=Invoke-Rpc 2 'tools/call' @{name=$Tool;arguments=$Arguments}
    if($result.isError){throw ($result | ConvertTo-Json -Depth 30 -Compress)}
    $result
}finally{
    if($session){try{Invoke-WebRequest -Uri $Endpoint -Method Delete -Headers $headers -TimeoutSec 5 -SkipHttpErrorCheck | Out-Null}catch{Write-Verbose ('MCP session close: '+$_.Exception.Message)}}
}
