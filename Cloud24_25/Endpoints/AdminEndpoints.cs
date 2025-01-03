using Cloud24_25.Infrastructure;
using Cloud24_25.Infrastructure.Dtos;
using Cloud24_25.Infrastructure.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Cloud24_25.Endpoints;

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/register", async (UserRegistrationDto registration,
            UserManager<User> userManager,
            RoleManager<IdentityRole> roleManager) =>
        {
            var user = new User { UserName = registration.Username, Files = [], Logs = [] };
            var result = await userManager.CreateAsync(user, registration.Password);

            if (result.Succeeded)
            {
                if (!await roleManager.RoleExistsAsync("Admin"))
                {
                    await roleManager.CreateAsync(new IdentityRole("Admin"));
                }
                await userManager.AddToRoleAsync(user, "Admin");
                return Results.Ok(new { Message = "Admin registered successfully" });
            }
            return Results.BadRequest(new { result.Errors });
        })
        .WithName("AdminRegister")
        .WithOpenApi();

        group.MapPost("/login", async (LoginDto login, UserManager<User> userManager, IConfiguration config) =>
        {
            var user = await userManager.FindByNameAsync(login.Username);
            if (user == null || !await userManager.CheckPasswordAsync(user, login.Password))
                return Results.Unauthorized();

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

            return Results.Ok(new
            {
                token = new JwtSecurityTokenHandler().WriteToken(token)
            });
        })
        .WithName("AdminLogin")
        .WithOpenApi();

        group.MapGet("/authorized-hello-world", [Authorize(Roles = "Admin")] () =>
            "Hello World! You are authorized as admin!")
            .WithName("AdminAuthorizedHelloWorld")
            .WithOpenApi();

        group.MapGet("/get-logs", [Authorize(Roles = "Admin")] (MyDbContext db) =>
        {
            var logs = db.Logs.ToList();
            return Results.Ok(logs);
        })
        .WithName("AdminGetLogs")
        .WithOpenApi();
    }
}