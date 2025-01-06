using Cloud24_25.Infrastructure;
using Cloud24_25.Infrastructure.Dtos;
using Cloud24_25.Infrastructure.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Cloud24_25.Service;

namespace Cloud24_25.Endpoints;

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/register", async (UserRegistrationDto registration,
            UserManager<User> userManager,
            MyDbContext db,
            RoleManager<IdentityRole> roleManager) =>
        {
            var user = new User { UserName = registration.Username, Email = registration.Email, EmailConfirmed = false, ConfirmationCode = RandomService.RandomString(10), Files = [], Logs = [] };
            var result = await userManager.CreateAsync(user, registration.Password);

            if (!result.Succeeded) return Results.BadRequest(new { result.Errors });
            if (!await roleManager.RoleExistsAsync("Admin"))
            {
                await roleManager.CreateAsync(new IdentityRole("Admin"));
            }
            await userManager.AddToRoleAsync(user, "Admin");
            await LogService.Log(LogType.Register, $"Admin {registration.Username} successfully registered", db,
                user);
            return Results.Ok(new { Message = "Admin registered successfully" });
        })
            .WithName("AdminRegister")
            .WithTags("Admin")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .WithOpenApi(operation =>
            {
                operation.Summary = "Register a New Admin";
                operation.Description = "Creates a new admin user with elevated privileges.";
                operation.Responses["200"].Description = "Admin registered successfully.";
                operation.Responses["400"].Description = "Registration failed.";
                return operation;
            });

        group.MapPost("/login", async (LoginDto login, UserManager<User> userManager, IConfiguration config, MyDbContext db) =>
        {
            var user = await userManager.FindByNameAsync(login.Username);
            if (user == null || !await userManager.CheckPasswordAsync(user, login.Password))
            {
                await LogService.Log(LogType.LoginAttempt,
                    $"There was an attempt to login as {login.Username}, but password was incorrect.", db, user);
                return Results.Unauthorized();
            }

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var roles = await userManager.GetRolesAsync(user);
            claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                issuer: config["Jwt:Issuer"],
                audience: config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddDays(1),
                signingCredentials: creds);

            await LogService.Log(LogType.Login, $"User {login.Username} logged in", db,
                user);
            return Results.Ok(new
            {
                token = new JwtSecurityTokenHandler().WriteToken(token)
            });
        })
            .WithName("AdminLogin")
            .WithTags("Admin")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .WithOpenApi(operation =>
            {
                operation.Summary = "Admin Login";
                operation.Description = "Authenticate as an admin user and receive a JWT token.";
                operation.Responses["200"].Description = "Login successful.";
                operation.Responses["401"].Description = "Invalid credentials.";
                return operation;
            });

        group.MapGet("/authorized-hello-world", [Authorize(Roles = "Admin")] () =>
            "Hello World! You are authorized as admin!")
            .WithName("AdminAuthorizedHelloWorld")
            .WithTags("Authorization")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .WithOpenApi(operation =>
            {
                operation.Summary = "Authorized Hello World for Admins";
                operation.Description = "Returns a greeting message for authorized admins.";
                operation.Responses["200"].Description = "Successfully retrieved greeting message.";
                operation.Responses["401"].Description = "Unauthorized access.";
                return operation;
            });

        group.MapGet("/get-logs", [Authorize(Roles = "Admin")] (MyDbContext db) =>
        {
            var logs = db.Logs.ToList().OrderByDescending(x => x.Date);
            return Results.Ok(logs);
        })
            .WithName("AdminGetLogs")
            .WithTags("Admin")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .WithOpenApi(operation =>
            {
                operation.Summary = "Get All System Logs";
                operation.Description = "Retrieves all system logs. Requires Admin role.";
                operation.Responses["200"].Description = "Successfully retrieved system logs.";
                operation.Responses["401"].Description = "Unauthorized access.";
                return operation;
            });
    }
}