using System.ComponentModel.DataAnnotations.Schema;

namespace AirLiticApp.Models;

[Table("subreason_lost_drone")]
public class SubreasonLostDrone
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

