USE [Seamless_Comparison_Fixture];
GO
-- 専用 fixture で、実行前に ID -2000000101 が未使用であることを確認する。
BEGIN TRANSACTION;
INSERT INTO dbo.FixtureParent (Id,Name) VALUES (-2000000101,N'comparison fixture');
COMMIT TRANSACTION;
BEGIN TRANSACTION;
UPDATE dbo.FixtureParent SET Name=N'updated' WHERE Id=-2000000101;
SELECT Id,Name FROM dbo.FixtureParent WHERE Id=-2000000101;
ROLLBACK TRANSACTION;
SELECT Id,Name FROM dbo.FixtureParent WHERE Id=-2000000101;
DELETE FROM dbo.FixtureParent WHERE Id=-2000000101;
GO
