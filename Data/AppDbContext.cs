using AirLiticApp.Models;
using Microsoft.EntityFrameworkCore;

namespace AirLiticApp.Data;

public class AppDbContext : DbContext
{
    private const string WindowsAuthConnectionString =
        "Server=localhost\\SQLEXPRESS;Database=airlitics;Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=3;";

    public DbSet<User> Users => Set<User>();
    public DbSet<Record> Records => Set<Record>();
    public DbSet<Pilot> Pilots => Set<Pilot>();
    public DbSet<Weapon> Weapons => Set<Weapon>();
    public DbSet<FlyingResult> FlyingResults => Set<FlyingResult>();
    public DbSet<Reason> Reasons => Set<Reason>();
    public DbSet<SubreasonLostDrone> SubreasonLostDrones => Set<SubreasonLostDrone>();
    public DbSet<SubreasonTech> SubreasonTeches => Set<SubreasonTech>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlServer(WindowsAuthConnectionString);
    }
}

