using System.ComponentModel.DataAnnotations.Schema;

namespace AirLiticApp.Models;

[Table("weapon")]
public class Weapon
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

