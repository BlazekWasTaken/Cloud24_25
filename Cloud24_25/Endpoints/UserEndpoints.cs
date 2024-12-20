using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Cloud24_25.Infrastructure;
using Cloud24_25.Infrastructure.Dtos;
using Cloud24_25.Infrastructure.Model;
using Cloud24_25.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace Cloud24_25.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/register", async (
                UserRegistrationDto registration,
                UserManager<User> userManager,
                MyDbContext db) =>
            {
                var user = new User { UserName = registration.Username, Files = [], Logs = []};
                var result = await userManager.CreateAsync(user, registration.Password);

                if (result.Succeeded)
                {
                    // TODO: get user and add the log to their logs
                    var log = new Log()
                    {
                        Id = Guid.NewGuid(),
                        Date = DateTime.UtcNow,
                        LogType = LogType.Register,
                        Message = $"User {registration.Username} successfully registered"
                    };
                    db.Logs.Add(log);
                    await db.SaveChangesAsync();
                    return Results.Ok(new { Message = "User registered successfully" });
                }
                else
                {
                    var log = new Log()
                    {
                        Id = Guid.NewGuid(),
                        Date = DateTime.UtcNow,
                        LogType = LogType.RegisterAttempt,
                        Message = $"User {registration.Username}'s attempt to register failed"
                    };
                    db.Logs.Add(log);
                    await db.SaveChangesAsync();
                    return Results.BadRequest(new { result.Errors });
                }
            })
            .WithName("UserRegister")
            .WithOpenApi();

        group.MapPost("/login", async (LoginDto login, UserManager<User> userManager,
            IConfiguration config, MyDbContext db) =>
            {
                var user = await userManager.FindByNameAsync(login.Username);
                if (user == null || !await userManager.CheckPasswordAsync(user, login.Password))
                {
                    var log = new Log
                    {
                        Id = Guid.NewGuid(),
                        Date = DateTime.UtcNow,
                        LogType = LogType.LoginAttempt,
                        Message = $"There was an attempt to login as {login.Username}, but password is incorrect."
                    };
                    db.Logs.Add(log);
                    await db.SaveChangesAsync();
                    return Results.Unauthorized();
                }
                
                var claims = new[]
                {
                    new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                    new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
                };

                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]));
                var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

                var token = new JwtSecurityToken(
                    config["Jwt:Issuer"],
                    config["Jwt:Audience"],
                    claims,
                    expires: DateTime.Now.AddDays(1),
                    signingCredentials: creds);
                
                // TODO: get user and add the log to their logs
                var log2 = new Log
                {
                    Id = Guid.NewGuid(),
                    Date = DateTime.UtcNow,
                    LogType = LogType.Login,
                    Message = $"User {login.Username} logged in."
                };
                db.Logs.Add(log2);
                await db.SaveChangesAsync();
                return Results.Ok(new
                {
                    token = new JwtSecurityTokenHandler().WriteToken(token)
                });
            })
            .WithName("UserLogin")
            .WithOpenApi();
        
        group.MapGet("/authorized-hello-world", [Authorize] () => "Hello World! You are authorized!")
            .WithName("UserAuthorizedHelloWorld")
            .WithOpenApi();

        group.MapPost("/upload-file",
                async (HttpContext context, IFormFile myFile, MyDbContext db) =>
                    await FileService.UploadFileAsync(context, myFile, db))
            .WithName("UploadFile")
            .DisableAntiforgery();
        
        group.MapGet("/get-files", (HttpContext context, MyDbContext db) => FileService.ListFiles(context, db))
            .WithName("UserGetFiles")
            .WithOpenApi();
        
        group.MapDelete("/delete-file/{fileId}", async (HttpContext context, MyDbContext db, Guid fileId) => 
            await FileService.DeleteFile(context, db, fileId))
            .WithName("UserDeleteFile")
            .WithOpenApi();

        group.MapGet("/get-file/{fileId}", async (HttpContext context, MyDbContext db, Guid fileId) =>
                await FileService.DownloadFile(context, db, fileId))
            .WithName("UserGetFile")
            .WithOpenApi();
    }
}