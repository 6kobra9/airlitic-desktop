USE [airlitics];
GO


-- 1) Таблиця subtype
IF OBJECT_ID('dbo.subtype', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.subtype
    (
        id   INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        name NVARCHAR(255) NULL
    );
END
GO

-- 2) Колонка weapon.subtype_id
IF COL_LENGTH('dbo.weapon', 'subtype_id') IS NULL
BEGIN
    ALTER TABLE dbo.weapon
        ADD subtype_id INT NULL;
END
GO

-- 3) FK (м’яко: додаємо тільки якщо її ще немає)
IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_weapon_subtype'
)
BEGIN
    ALTER TABLE dbo.weapon
        ADD CONSTRAINT FK_weapon_subtype
        FOREIGN KEY (subtype_id) REFERENCES dbo.subtype(id);
END
GO

