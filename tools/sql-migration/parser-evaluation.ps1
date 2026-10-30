param(
    [Parameter(Mandatory)][string]$DllPath,
    [Parameter(Mandatory)][string]$ResultPath,
    [string]$ExpectedSha256 = '0C80F92BC658C0A55FEB3F9A51FBCB5EC48368A0389D609EC1C3FEA8AC886518'
)
$ErrorActionPreference = 'Stop'
$dll = (Resolve-Path -LiteralPath $DllPath).Path
$actualHash = (Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash
if ($actualHash -ne $ExpectedSha256) { throw "DLL SHA256 mismatch: $actualHash" }
$out = [IO.Path]::GetFullPath($ResultPath)
if ($out -notmatch '^[DdFf]:\\') { throw 'ResultPath must be on D: or F:.' }
$assembly = [Reflection.Assembly]::LoadFrom($dll)
if ($assembly.GetName().Version.ToString() -ne '0.6.5.0') { throw 'Expected SqlParser assembly version 0.6.5.0.' }
function Check([bool]$condition, [string]$message) { if (-not $condition) { throw $message } }
function TypeName($obj) { if ($null -eq $obj) { return '' }; return $obj.GetType().FullName }
function ExpectedType($obj, [string]$name) { Check ((TypeName $obj) -eq ('SqlParser.Ast.' + $name)) "Expected $name, got $(TypeName $obj)" }
$cases = @(
 @{id='select'; sql='SELECT id, name FROM users WHERE id = 1 ORDER BY name'; type='Select'},
 @{id='insert'; sql="INSERT INTO users (id, name) VALUES (1, 'Ada')"; type='Insert'},
 @{id='update'; sql="UPDATE users SET name = 'Ada' WHERE id = 1"; type='Update'},
 @{id='delete'; sql='DELETE FROM users WHERE id = 1'; type='Delete'},
 @{id='join'; sql='SELECT a.id, b.name FROM users a JOIN roles b ON a.role_id = b.id'; type='Select'},
 @{id='cte'; sql='WITH x AS (SELECT id FROM users) SELECT id FROM x'; type='Select'},
 @{id='aggregate'; sql='SELECT role_id, COUNT(*) AS n FROM users GROUP BY role_id HAVING COUNT(*) > 1'; type='Select'},
 @{id='multiple'; sql='SELECT 1; SELECT 2;'; type='Select'; count=2},
 @{id='invalid_select'; sql='SELECT FROM WHERE;'; invalid=$true},
 @{id='invalid_parenthesis'; sql='SELECT (1 + 2'; invalid=$true},
 @{id='invalid_insert'; sql='INSERT INTO users (id) VALUES ('; invalid=$true},
 @{id='semantic_duplicate'; sql='CREATE TABLE t (x int, x int)'; type='CreateTable'}
)
$dialects = @(
 @{name='mssql'; type='SqlParser.Dialects.MsSqlDialect'; specific='SELECT TOP 2 [id] FROM [users]'},
 @{name='postgresql'; type='SqlParser.Dialects.PostgreSqlDialect'; specific='SELECT "id" FROM "users" LIMIT 2'},
 @{name='mysql'; type='SqlParser.Dialects.MySqlDialect'; specific='SELECT `id` FROM `users` LIMIT 2'}
)
$results = [Collections.Generic.List[object]]::new()
foreach ($dialect in $dialects) {
 $d = [Activator]::CreateInstance($assembly.GetType($dialect.type))
 $all = @($cases) + @(
  @{id='dialect_specific'; sql=$dialect.specific; type='Select'},
  @{id='cast'; sql="SELECT CAST('123' AS INT)"; type='Select'},
  @{id='semantic_conversion_error'; sql="SELECT CAST('abc' AS INT)"; type='Select'},
  @{id='top'; sql='SELECT TOP 2 id FROM users'; type='Select'},
  @{id='limit'; sql='SELECT id FROM users LIMIT 2'; type='Select'},
  @{id='incomplete_cast'; sql='SELECT CAST(1 AS)'; invalid=$true; line=1; column=17},
  @{id='position_ascii'; sql="SELECT 'a' + CAST(1 AS)"; invalid=$true; line=1; column=23},
  @{id='position_bmp'; sql="SELECT '漢' + CAST(1 AS)"; invalid=$true; line=1; column=23},
  @{id='position_emoji'; sql="SELECT '😀' + CAST(1 AS)"; invalid=$true; line=1; column=24},
  @{id='position_crlf'; sql="SELECT 1;`r`nSELECT '漢😀' + CAST(1 AS)"; invalid=$true; line=2; column=25}
 )
 foreach ($case in $all) {
  $ast=$null; $parseError=$null; $errors=[Collections.Generic.List[string]]::new()
  try { $ast=@([SqlParser.SqlQueryParser]::new().Parse($case.sql, $d)) } catch { $parseError=$_.Exception; while ($parseError.InnerException) { $parseError=$parseError.InnerException } }
  try {
   if ($case.invalid) {
    Check ($null -ne $parseError) 'Expected parser to reject invalid syntax.'
    Check ($parseError.GetType().FullName -eq 'SqlParser.ParserException') 'Expected ParserException.'
    if ($case.ContainsKey('line')) { Check ($parseError.Line -eq $case.line -and $parseError.Column -eq $case.column) "Unexpected diagnostic position: $($parseError.Line):$($parseError.Column)" }
   } else {
    Check ($null -eq $parseError) "Unexpected parser exception: $parseError"
    $count=if ($case.ContainsKey('count')) { $case.count } else { 1 }
    Check ($ast.Count -eq $count) "Expected $count statements, got $($ast.Count)"
    foreach ($statement in $ast) { ExpectedType $statement ('Statement+' + $case.type) }
    if ($case.type -eq 'Select') {
     $query=$ast[0].Query; ExpectedType $query.Body 'SetExpression+SelectExpression'; $select=$query.Body.Select
     Check ($select.Projection.Count -gt 0) 'Projection missing.'
     switch ($case.id) {
      'select' { Check ($select.Projection.Count -eq 2 -and $select.From.Count -eq 1 -and $null -ne $select.Selection -and $query.OrderBy.Expressions.Count -eq 1) 'SELECT fields missing.'; ExpectedType $select.Selection 'Expression+BinaryOp'; ExpectedType $select.From[0].Relation 'TableFactor+Table' }
      'join' { Check ($select.From[0].Joins.Count -eq 1) 'JOIN missing.'; ExpectedType $select.From[0].Joins[0].JoinOperator 'JoinOperator+Inner'; ExpectedType $select.From[0].Joins[0].JoinOperator.JoinConstraint 'JoinConstraint+On' }
      'cte' { Check ($null -ne $query.With) 'CTE missing.' }
      'aggregate' { ExpectedType $select.GroupBy 'GroupByExpression+Expressions'; ExpectedType $select.Projection[1].Expression 'Expression+Function'; Check ($null -ne $select.Having) 'HAVING missing.' }
      { $_ -in @('cast','semantic_conversion_error') } { $expr=$select.Projection[0].Expression; ExpectedType $expr 'Expression+Cast'; ExpectedType $expr.DataType 'DataType+Int'; ExpectedType $expr.Expression.Value 'Value+SingleQuotedString'; $expected=if ($_ -eq 'cast') {'123'} else {'abc'}; Check ($expr.Expression.Value.Value -eq $expected) 'CAST operand mismatch.' }
      'top' { ExpectedType $select.Top.Quantity 'TopQuantity+Constant'; Check ($select.Top.Quantity.Quantity -eq 2) 'TOP mismatch.' }
      'limit' { ExpectedType $query.Limit 'Expression+LiteralValue'; Check ($query.Limit.Value.Value -eq '2') 'LIMIT mismatch.' }
      'dialect_specific' { if ($dialect.name -eq 'mssql') { ExpectedType $select.Top.Quantity 'TopQuantity+Constant' } else { ExpectedType $query.Limit 'Expression+LiteralValue' } }
     }
    }
   }
  } catch { $errors.Add($_.Exception.Message) }
  $results.Add([pscustomobject]@{dialect=$dialect.name; id=$case.id; sql=$case.sql; accepted=($null -eq $parseError); statementTypes=@($ast | ForEach-Object { TypeName $_ }); diagnostic=if ($parseError) { @{type=$parseError.GetType().FullName;message=$parseError.Message;line=$parseError.Line;column=$parseError.Column} } else {$null}; passed=($errors.Count -eq 0); errors=@($errors)})
 }
}
$report=[ordered]@{parser='SqlParserCS'; version=$assembly.GetName().Version.ToString(); dllPath=$dll; dllSha256=$actualHash; runtime=[Environment]::Version.ToString(); pwsh=$PSVersionTable.PSVersion.ToString(); createdUtc=[DateTime]::UtcNow.ToString('o'); scope='Syntax and selected AST properties only; no execution or performance assessment.'; results=@($results)}
$parent=[IO.Path]::GetDirectoryName($out); if (-not [IO.Directory]::Exists($parent)) { throw "Result directory does not exist: $parent" }
$report | ConvertTo-Json -Depth 15 | Set-Content -LiteralPath $out -Encoding utf8
$failed=@($results | Where-Object { -not $_.passed })
Write-Output "Cases: $($results.Count); failed: $($failed.Count); report: $out"
if ($failed.Count -gt 0) { $failed | Format-Table dialect,id,errors | Out-String | Write-Output; exit 1 }
