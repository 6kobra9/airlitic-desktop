using System.ComponentModel.DataAnnotations.Schema;

namespace AirLiticApp.Models;

[Table("pilot")]
public class Pilot
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

