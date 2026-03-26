USE [airlitics]
GO

IF COL_LENGTH('dbo.results', 'region_id') IS NULL
BEGIN
    ALTER TABLE dbo.results ADD region_id INT NULL;
END
GO

IF COL_LENGTH('dbo.results', 'region_id') IS NOT NULL
AND OBJECT_ID('dbo.region', 'U') IS NOT NULL
AND NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_results_region'
)
BEGIN
    ALTER TABLE dbo.results
    ADD CONSTRAINT FK_results_region
        FOREIGN KEY (region_id) REFERENCES dbo.region(id);
END
GO
