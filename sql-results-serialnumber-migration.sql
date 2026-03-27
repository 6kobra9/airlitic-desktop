USE [airlitics]
GO

IF COL_LENGTH('dbo.results', 'serial_number') IS NULL
BEGIN
    ALTER TABLE dbo.results ADD serial_number nvarchar(255) NULL;
END
GO
