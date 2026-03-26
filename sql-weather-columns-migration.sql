USE [airlitics];
GO

IF COL_LENGTH('dbo.results', 'weather_temperature') IS NULL
    ALTER TABLE dbo.results ADD weather_temperature nvarchar(255) NULL;
GO

IF COL_LENGTH('dbo.results', 'weather_wind_direction') IS NULL
    ALTER TABLE dbo.results ADD weather_wind_direction nvarchar(255) NULL;
GO

IF COL_LENGTH('dbo.results', 'weather_wind_speed') IS NULL
    ALTER TABLE dbo.results ADD weather_wind_speed nvarchar(255) NULL;
GO

IF COL_LENGTH('dbo.results', 'weather_precipitation') IS NULL
    ALTER TABLE dbo.results ADD weather_precipitation nvarchar(255) NULL;
GO

IF COL_LENGTH('dbo.results', 'weather_cloud_cover') IS NULL
    ALTER TABLE dbo.results ADD weather_cloud_cover nvarchar(255) NULL;
GO

