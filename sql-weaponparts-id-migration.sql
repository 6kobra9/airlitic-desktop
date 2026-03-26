USE [airlitics];
GO

-- 1) weapon_parts.id (identity PK)
IF COL_LENGTH('dbo.weapon_parts', 'id') IS NULL
BEGIN
    ALTER TABLE dbo.weapon_parts
        ADD id INT IDENTITY(1,1) NOT NULL;
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'PK_weapon_parts_id'
)
BEGIN
    ALTER TABLE dbo.weapon_parts
        ADD CONSTRAINT PK_weapon_parts_id PRIMARY KEY CLUSTERED (id);
END
GO

-- 2) results.weapon_part_id
IF COL_LENGTH('dbo.results', 'weapon_part_id') IS NULL
BEGIN
    ALTER TABLE dbo.results
        ADD weapon_part_id INT NULL;
END
GO

-- 3) (опционально) FK results.weapon_part_id -> weapon_parts.id
IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_results_weapon_parts'
)
BEGIN
    ALTER TABLE dbo.results
        ADD CONSTRAINT FK_results_weapon_parts
        FOREIGN KEY (weapon_part_id) REFERENCES dbo.weapon_parts(id);
END
GO

