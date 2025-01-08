using Cloud24_25.Infrastructure;
using Cloud24_25.Infrastructure.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Cloud24_25.Service;

public static class LogService
{
    public static async Task Log(LogType type, string message, MyDbContext db, User? user)
    {
        var log = new Log
        {
            Id = Guid.NewGuid(),
            Date = DateTime.UtcNow,
            LogType = type,
            Message = message,
        };
        if (user?.Logs != null) user.Logs.Add(log);
        else if (user != null)
        {
            user.Logs = [log];
        }
        db.Logs.Add(log);
        await db.SaveChangesAsync();
    }
    [Authorize(Policy = "UserOrAdmin")]
    public static async Task<IResult> ListUserLogs(HttpContext httpContext, 
        UserManager<User> userManager,
        MyDbContext db)
    {
        var endpointUser = httpContext.User;
        var username = endpointUser.Identity?.Name;
        if (username == null) return Results.Unauthorized();
        if (!db.Users.Any() || !db.Users.Any(x => x.UserName == username))
        {
            await Log(LogType.Failure, $"User {username} was not found.", db, null);
            return Results.NotFound();
        }
        var user = db.Users
            .Include(x => x.Logs)
            .First(x => x.UserName == username);
        var logs = user.Logs.OrderByDescending(x => x.Date);
        return Results.Ok(logs);
    }
    [Authorize(Policy = "AdminOnly")]
    public static async Task<IResult> ListAllLogs(MyDbContext db)
    {
        var logs = (await db.Logs.ToListAsync()).OrderByDescending(x => x.Date);
        return Results.Ok(logs);
    }
}