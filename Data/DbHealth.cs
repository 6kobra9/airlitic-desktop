using System;

namespace AirLiticApp.Data;

public static class DbHealth
{
    public static bool IsDatabaseAvailable()
    {
        try
        {
            using var db = new AppDbContext();
            return db.Database.CanConnect();
        }
        catch
        {
            return false;
        }
    }

    public static string GetUnavailableMessage() => "БД відсутня або недоступна.";
}

