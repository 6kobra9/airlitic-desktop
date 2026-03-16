using System.ComponentModel.DataAnnotations.Schema;

namespace AirLiticApp.Models;

[Table("flying_result")]
public class FlyingResult
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

