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
        group.MapPost("/register", async (UserRegistrationDto registration,
                UserManager<User> userManager) =>
            {
                var user = new User { UserName = registration.Username, Files = [], Logs = []};
                var result = await userManager.CreateAsync(user, registration.Password);

                return result.Succeeded ? 
                    Results.Ok(new { Message = "User registered successfully" }) : 
                    Results.BadRequest(new { result.Errors });
            })
            .WithName("UserRegister")
            .WithOpenApi();

        group.MapPost("/login", async (LoginDto login, UserManager<User> userManager,
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
        
        group.MapPost("/upload-file", 
                async (HttpContext context, IFormFile myFile, MyDbContext db) => 
                    await FileService.UploadFileAsync(context, myFile, db))
            .WithName("UploadFile")
            .DisableAntiforgery();
        
        group.MapGet("/get-file/{filename}", (HttpContext context, MyDbContext db) => 
            {
                // return file
            })
            .WithName("UserGetFile");
    }
}