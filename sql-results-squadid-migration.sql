USE [airlitics];
GO

IF COL_LENGTH('dbo.results', 'squad_id') IS NULL
BEGIN
    ALTER TABLE dbo.results ADD squad_id INT NULL;
END
GO

-- (опционально) FK results.squad_id -> squad.id
IF COL_LENGTH('dbo.results', 'squad_id') IS NOT NULL
AND COL_LENGTH('dbo.squad', 'id') IS NOT NULL
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM sys.foreign_keys
        WHERE name = N'FK_results_squad'
    )
    BEGIN
        -- Если FK мешает (например, существующие данные), блок можно пропустить.
        ALTER TABLE dbo.results
            ADD CONSTRAINT FK_results_squad
            FOREIGN KEY (squad_id) REFERENCES dbo.squad(id);
    END
END
GO

