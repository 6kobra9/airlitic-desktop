using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace AirLiticApp.Models;

[Table("results")]
public class Record
{
    public int Id { get; set; }
    public DateTime? Date { get; set; }
    [Column("time")]
    public TimeSpan? Time { get; set; }
    [Column("pilot_id")]
    public int? PilotId { get; set; }
    // weapon_id в таблице `results` больше не используем: вместо него берем конкретную строку `weapon_parts`
    [NotMapped]
    public int? WeaponId { get; set; }
    [Column("weapon_part_id")]
    public int? WeaponPartId { get; set; }
    [Column("flying_result_id")]
    public int? FlyingResultId { get; set; }
    [Column("reason_id")]
    public int? ReasonId { get; set; }
    [Column("serial_number")]
    public string? SerialNumber { get; set; }
    public string Description { get; set; } = string.Empty;

    [Column("weather_temperature")]
    public string? WeatherTemperature { get; set; }
    [Column("weather_wind_direction")]
    public string? WeatherWindDirection { get; set; }
    [Column("weather_wind_speed")]
    public string? WeatherWindSpeed { get; set; }
    [Column("weather_precipitation")]
    public string? WeatherPrecipitation { get; set; }
    [Column("weather_cloud_cover")]
    public string? WeatherCloudCover { get; set; }

    [Column("squad_id")]
    public int? SquadId { get; set; }
    [Column("region_id")]
    public int? RegionId { get; set; }

    [Column("user_id")]
    public int? UserId { get; set; }
    [Column("dlc")]
    public DateTime? Dlc { get; set; }
    [Column("subreason_lost_drone_id")]
    public int? SubreasonLostDroneId { get; set; }
    [Column("subreason_tech_id")]
    public int? SubreasonTechId { get; set; }

    [NotMapped]
    public string? PilotName { get; set; }
    [NotMapped]
    public string? WeaponName { get; set; }
    [NotMapped]
    public string? WeaponSerialNumber { get; set; }
    [NotMapped]
    public string? WeaponTypeName { get; set; }
    [NotMapped]
    public string? WeaponFrequencyMhz { get; set; }
    [NotMapped]
    public string? WeaponVideoTypeName { get; set; }
    [NotMapped]
    public string? FlyingResultName { get; set; }
    [NotMapped]
    public string? ReasonName { get; set; }
    [NotMapped]
    public string? SubreasonLostDroneName { get; set; }
    [NotMapped]
    public string? SubreasonTechName { get; set; }
}

