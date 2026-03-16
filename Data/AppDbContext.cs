using AirLiticApp.Models;
using Microsoft.EntityFrameworkCore;

namespace AirLiticApp.Data;

public class AppDbContext : DbContext
{
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
        // Подключение к локальному SQL Server Express, БД airlitics
        optionsBuilder.UseSqlServer(
            "Server=localhost\\SQLEXPRESS;Database=airlitics;Trusted_Connection=True;TrustServerCertificate=True;");
    }
}

