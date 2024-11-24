using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Cloud24_25.Infrastructure.Dtos;
using Cloud24_25.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace Cloud24_25.Endpoints;

public static class User
{
    public static void MapUserEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/register", async (UserRegistrationDto registration,
                UserManager<IdentityUser> userManager) =>
            {
                var user = new IdentityUser { UserName = registration.Username };
                var result = await userManager.CreateAsync(user, registration.Password);

                return result.Succeeded ? 
                    Results.Ok(new { Message = "User registered successfully" }) : 
                    Results.BadRequest(new { result.Errors });
            })
            .WithName("UserRegister")
            .WithOpenApi();

        group.MapPost("/login", async (LoginDto login, UserManager<IdentityUser> userManager,
                IConfiguration config) =>
            {
                var user = await userManager.FindByNameAsync(login.Username);
                if (user == null || !await userManager.CheckPasswordAsync(user, login.Password))
                    return Results.Unauthorized();

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
        
        group.MapPost("/upload-file", [Authorize] async (HttpContext context, IFormFile myFile) =>
            {
                var user = context.User;
                var userId = user.FindFirstValue(JwtRegisteredClaimNames.Sub);
                if (userId == null) return Results.Unauthorized();
                var userGuid = Guid.Parse(userId);
                
                
                
                return Results.Ok();
            })
            .WithName("UploadFile")
            .DisableAntiforgery();
        
        group.MapGet("/get-file/{filename}", [Authorize] (HttpContext context) => 
            {
                // return file
            })
            .WithName("UserGetFile");
    }
}