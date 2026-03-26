USE [airlitics];
GO

IF COL_LENGTH('dbo.results', 'weapon_id') IS NOT NULL
BEGIN
    ALTER TABLE dbo.results DROP COLUMN weapon_id;
END
GO

