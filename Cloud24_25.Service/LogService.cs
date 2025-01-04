using Cloud24_25.Infrastructure;
using Cloud24_25.Infrastructure.Model;

namespace Cloud24_25.Service;

public static class LogService
{
    public static async Task Log(string message, MyDbContext db)
    {
        var log = new Log()
        {
            Id = Guid.NewGuid(),
            Date = DateTime.UtcNow,
            LogType = LogType.Register,
            Message = message
        };
        db.Logs.Add(log);
        await db.SaveChangesAsync();
    }
}