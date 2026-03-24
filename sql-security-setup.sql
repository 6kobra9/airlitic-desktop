/* ===== SQL Server security setup for AirLitic =====
   Server: localhost\SQLEXPRESS
   DB: airlitics
*/

USE master;
GO

/* ===== 0) Parameters (change passwords before run) ===== */
DECLARE @AppDb sysname             = N'airlitics';
DECLARE @AppLogin sysname          = N'airlitic_user';
DECLARE @AppPassword nvarchar(128) = N'StrongPass!123';      -- TODO: change

DECLARE @SqlAdminLogin sysname          = N'airlitic_admin';
DECLARE @SqlAdminPassword nvarchar(128) = N'VeryStrong!456'; -- TODO: change
GO

/* ===== 1) Ensure DB exists ===== */
IF DB_ID(N'airlitics') IS NULL
BEGIN
    RAISERROR(N'База airlitics не знайдена. Спочатку CREATE DATABASE airlitics.', 16, 1);
    RETURN;
END
GO

/* ===== 2) SQL admin (safety access) ===== */
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'airlitic_admin')
BEGIN
    EXEC('CREATE LOGIN [airlitic_admin] WITH PASSWORD = ''VeryStrong!456'', CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;');
END
ELSE
BEGIN
    EXEC('ALTER LOGIN [airlitic_admin] WITH PASSWORD = ''VeryStrong!456'';');
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.server_role_members m
    JOIN sys.server_principals r ON r.principal_id = m.role_principal_id
    JOIN sys.server_principals p ON p.principal_id = m.member_principal_id
    WHERE r.name = N'sysadmin' AND p.name = N'airlitic_admin'
)
BEGIN
    ALTER SERVER ROLE [sysadmin] ADD MEMBER [airlitic_admin];
END
GO

/* ===== 3) App SQL login ===== */
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'airlitic_user')
BEGIN
    EXEC('CREATE LOGIN [airlitic_user] WITH PASSWORD = ''StrongPass!123'', CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;');
END
ELSE
BEGIN
    EXEC('ALTER LOGIN [airlitic_user] WITH PASSWORD = ''StrongPass!123'';');
END
GO

ALTER LOGIN [airlitic_user] ENABLE;
GO

/* ===== 4) DB user + permissions ===== */
USE [airlitics];
GO

IF EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'airlitic_user')
    DROP USER [airlitic_user];
GO

CREATE USER [airlitic_user] FOR LOGIN [airlitic_user];
GO

-- DEV option: full permissions in this DB
ALTER ROLE [db_owner] ADD MEMBER [airlitic_user];
GO

/* ===== 5) Deny Windows login for current account ===== */
USE master;
GO

DECLARE @CurrentWindowsLogin sysname = SUSER_SNAME();
PRINT N'Current Windows login: ' + ISNULL(@CurrentWindowsLogin, N'<NULL>');

IF @CurrentWindowsLogin IS NOT NULL
   AND EXISTS (SELECT 1 FROM sys.server_principals WHERE name = @CurrentWindowsLogin)
BEGIN
    DECLARE @sql nvarchar(max) =
        N'DENY CONNECT SQL TO ' + QUOTENAME(@CurrentWindowsLogin) + N';';
    EXEC sp_executesql @sql;
    PRINT N'DENY CONNECT SQL applied to: ' + @CurrentWindowsLogin;
END
ELSE
BEGIN
    PRINT N'Windows login principal not found in SQL Server (skip DENY).';
END
GO

/* ===== 6) Verification ===== */
SELECT name, type_desc, is_disabled
FROM sys.server_principals
WHERE name IN (N'airlitic_admin', N'airlitic_user', SUSER_SNAME());
GO

USE [airlitics];
GO
SELECT dp.name AS db_user, dp.type_desc
FROM sys.database_principals dp
WHERE dp.name = N'airlitic_user';
GO

/* ===== Rollback DENY for current Windows login (run only if needed) =====
USE master;
GO
DECLARE @me sysname = SUSER_SNAME();
DECLARE @undoSql nvarchar(max) = N'GRANT CONNECT SQL TO ' + QUOTENAME(@me) + N';';
EXEC sp_executesql @undoSql;
GO
*/
