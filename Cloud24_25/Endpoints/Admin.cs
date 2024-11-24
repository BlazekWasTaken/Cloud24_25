using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Cloud24_25.Infrastructure.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace Cloud24_25.Endpoints;

public static class Admin
{
    public static void MapAdminEndpoints(this RouteGroupBuilder group, WebApplicationBuilder builder)
    {
        group.MapPost("/register", async (UserRegistrationDto registration,
                UserManager<IdentityUser> userManager) =>
            {
                if (registration.Password != "admin") return Results.BadRequest(new { Message = "Wrong admin password." });
                var user = new IdentityUser { UserName = registration.Username };
                var result = await userManager.CreateAsync(user, registration.Password);

                return result.Succeeded ? 
                    Results.Ok(new { Message = "Admin registered successfully" }) : 
                    Results.BadRequest(new { result.Errors });
            })
            .WithName("AdminRegister")
            .WithOpenApi();

        group.MapPost("/login", async (LoginDto login, UserManager<IdentityUser> userManager,
                IConfiguration config) =>
            {
                if (login.Password != "admin") return Results.BadRequest(new { Message = "Wrong admin password." });

                var user = await userManager.FindByNameAsync(login.Username);
                if (user == null || !await userManager.CheckPasswordAsync(user, login.Password))
                    return Results.Unauthorized();

                var claims = new[]
                {
                    new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                    new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                    new Claim("aud", config["Jwt:AdminAudience"])
                };

                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]));
                var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

                var token = new JwtSecurityToken(
                    config["Jwt:Issuer"],
                    config["Jwt:AdminAudience"],
                    claims,
                    expires: DateTime.Now.AddDays(1),
                    signingCredentials: creds);

                return Results.Ok(new
                {
                    token = new JwtSecurityTokenHandler().WriteToken(token)
                });
            })
            .WithName("AdminLogin")
            .WithOpenApi();
        
        group.MapGet("/authorized-hello-world", [Authorize](HttpContext context) =>
            {
                var user = context.User;

                var audienceClaim = user.FindFirstValue("aud");
                return audienceClaim == builder.Configuration["Jwt:AdminAudience"] ? 
                    Results.Ok("Hello World! You are authorized as admin!") : Results.Forbid();
            })
            .WithName("AdminAuthorizedHelloWorld")
            .WithOpenApi();
        
        group.MapGet("/get-file/{username}&{filename}", [Authorize] () => 
            {
                // return file
            })
            .WithName("AdminGetFile");
    }
}