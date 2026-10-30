USE [AdventureWorks2016];
GO
-- AdventureWorksの同名DBをSQL ServerとSeamlessの両方で選択して実行する。
-- SQL・引数・データ世代をそろえ、各結果の列情報・値・NULL・重複を比較する。
-- このファイルは読み取り専用。実行・結果一致の検証は別途必要。

-- AW-BASIC-01: 主キー順の基本照会
SELECT TOP (20) BusinessEntityID, JobTitle, HireDate
FROM HumanResources.Employee
ORDER BY BusinessEntityID;
GO

-- AW-BASIC-02: 集計（文字列の比較・照合順序も検証対象）
SELECT JobTitle, COUNT(*) AS EmployeeCount
FROM HumanResources.Employee
GROUP BY JobTitle
ORDER BY JobTitle;
GO

-- AW-BASIC-03: 生成された2テーブルのJOIN
SELECT TOP (20) e.BusinessEntityID, p.FirstName, p.LastName, e.JobTitle
FROM HumanResources.Employee AS e
INNER JOIN Person.Person AS p ON p.BusinessEntityID = e.BusinessEntityID
ORDER BY e.BusinessEntityID;
GO

-- AW-BASIC-04: 元のビュー定義を実行（保存済み結果の置換は不可）
SELECT BusinessEntityID, FirstName, LastName, JobTitle, Department
FROM HumanResources.vEmployeeDepartment
ORDER BY BusinessEntityID, Department, FirstName, LastName, JobTitle;
GO
