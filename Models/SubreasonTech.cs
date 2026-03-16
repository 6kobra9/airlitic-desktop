using System.ComponentModel.DataAnnotations.Schema;

namespace AirLiticApp.Models;

[Table("subreason_tech")]
public class SubreasonTech
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

