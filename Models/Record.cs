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
    [Column("weapon_id")]
    public int? WeaponId { get; set; }
    [Column("flying_result_id")]
    public int? FlyingResultId { get; set; }
    [Column("reason_id")]
    public int? ReasonId { get; set; }
    public string Description { get; set; } = string.Empty;

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
    public string? FlyingResultName { get; set; }
    [NotMapped]
    public string? ReasonName { get; set; }
    [NotMapped]
    public string? SubreasonLostDroneName { get; set; }
    [NotMapped]
    public string? SubreasonTechName { get; set; }
}

