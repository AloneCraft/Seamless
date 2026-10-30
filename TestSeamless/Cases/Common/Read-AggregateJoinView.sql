USE [Seamless_Comparison_Fixture];
GO
-- 同じ SELECT を SSMS と比較実行プログラムで使用する。
SELECT p.Id AS ParentId,
       p.Name AS ParentName,
       COUNT(i.Id) AS ItemCount,
       SUM(i.Amount) AS AmountTotal
FROM dbo.FixtureParent AS p
LEFT JOIN dbo.FixtureItem AS i ON i.ParentId = p.Id
GROUP BY p.Id, p.Name
ORDER BY p.Id;
GO
