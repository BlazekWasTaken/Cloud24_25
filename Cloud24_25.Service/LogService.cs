using Cloud24_25.Infrastructure;
using Cloud24_25.Infrastructure.Model;
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
    public static async Task<IResult> ListUserLogs(HttpContext httpContext, 
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
    public static async Task<IResult> ListAllLogs(HttpContext httpContext, 
        UserManager<User> userManager,
        MyDbContext db)
    {
        var endpointUser = httpContext.User;
        var username = endpointUser.Identity?.Name;
        if (username == null) return Results.Unauthorized();
        
        if (!db.Users.Any() || !db.Users.Any(x => x.UserName == username))
        {
            await Log(LogType.Failure, $"Admin or user {username} was not found.", db, null);
            return Results.NotFound();
        }
        var user = db.Users
            .Include(x => x.Logs)
            .First(x => x.UserName == username);

        if (!(await userManager.GetRolesAsync(user)).Contains("Admin"))
        {
            await Log(LogType.Failure, $"User {username} attempted to view all application logs, but is not an admin.", db, user);
            return Results.Forbid();
        }
        var logs = db.Logs.OrderByDescending(x => x.Date);
        return Results.Ok(logs);
    }
}