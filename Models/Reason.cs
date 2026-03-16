using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace AirLiticApp.Models;

[Table("reason")]
public class Reason
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

