using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Cloud24_25.Infrastructure;
using Cloud24_25.Infrastructure.Dtos;
using Cloud24_25.Infrastructure.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Resend;

namespace Cloud24_25.Service;

public static class LoginService
{
    private const int MaxUsers = 10;

    #region Actions
    private static async Task<IResult> Registration(UserRegistrationDto registration,
        UserManager<User> userManager,
        MyDbContext db,
        RoleManager<IdentityRole> roleManager,
        IResend resend,
        bool isAdmin)
    {
        var role = isAdmin ? "Admin" : "User";
        if (!isAdmin && (await userManager.GetUsersInRoleAsync("User")).Count >= MaxUsers)
        {
            await LogService.Log(LogType.RegisterAttempt, $"User {registration.Username}'s attempt to register failed, max user count exceeded", db, null);
            return Results.BadRequest(new { Message = "Max user count exceeded" });
        }
        var user = new User
        {
            UserName = registration.Username, 
            Email = registration.Email, 
            EmailConfirmed = false, 
            ConfirmationCode = RandomService.RandomString(10), 
            Files = [], 
            Logs = []
        };
        var userResult = await userManager.CreateAsync(user, registration.Password);
        if (!userResult.Succeeded)
        {
            await LogService.Log(LogType.RegisterAttempt, $"{role} {registration.Username}'s attempt to register failed", db, null);
            return Results.BadRequest(new { userResult.Errors });
        }
        if (!isAdmin)
        {
            var emailResult = await MailService.SendConfirmationEmail(resend, user);
            if (!emailResult.Success)
            {
                await LogService.Log(LogType.RegisterAttempt, $"{role} {registration.Username}'s attempt to register failed, email not sent", db, null);
                await userManager.DeleteAsync(user);
                return Results.BadRequest(new { Message = "Confirmation email could not be sent" });
            }
        }
        
        if (!await roleManager.RoleExistsAsync(role)) 
            await roleManager.CreateAsync(new IdentityRole(role));
        await userManager.AddToRoleAsync(user, role);
        
        await LogService.Log(LogType.Register, $"{role} {registration.Username} successfully registered", db,
            user);
        return Results.Ok(new
        {
            id = user.Id
        });
    }
    private static async Task<IResult> Login(LoginDto login,
        UserManager<User> userManager,
        IConfiguration config,
        MyDbContext db,
        bool isAdmin)
    {
        var user = await userManager.FindByNameAsync(login.Username);
        if (user is null || !await userManager.CheckPasswordAsync(user, login.Password) || user.UserName is null)
        {
            await LogService.Log(LogType.LoginAttempt,
                $"There was an attempt to login as {login.Username}, but password was incorrect.", db, user);
            return Results.Unauthorized();
        }
        if (!user.EmailConfirmed)
        {
            await LogService.Log(LogType.LoginAttempt, $"There was an attempt to login as {login.Username}, but email is not confirmed.", db, user);
            return Results.Forbid();
        }
        var claims = new List<Claim>
        {
            new (JwtRegisteredClaimNames.Sub, user.Id),
            new (JwtRegisteredClaimNames.UniqueName, user.UserName),
            new (JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        var roles = await userManager.GetRolesAsync(user);
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config[isAdmin ? "Jwt:AdminAudience" : "Jwt:Audience"],
            claims: claims,
            expires: DateTime.Now.AddDays(1),
            signingCredentials: creds);
        
        var role = isAdmin ? "Admin" : "User";
        await LogService.Log(LogType.Login, $"{role} {login.Username} logged in", db, user);
        
        return Results.Ok(new
        {
            token = new JwtSecurityTokenHandler().WriteToken(token)
        });
    }
    #endregion
    
    #region Admin
    public static async Task<IResult> AdminRegistration(UserRegistrationDto registration,
        UserManager<User> userManager,
        MyDbContext db,
        RoleManager<IdentityRole> roleManager,
        IResend resend)
    {
        return await Registration(registration, userManager, db, roleManager, resend, true);
    }
    public static async Task<IResult> AdminLogin(LoginDto login, 
        UserManager<User> userManager, 
        IConfiguration config, 
        MyDbContext db)
    {
        return await Login(login, userManager, config, db, true);
    }
    #endregion

    #region User
    public static async Task<IResult> UserRegistration(UserRegistrationDto registration,
        UserManager<User> userManager,
        MyDbContext db,
        RoleManager<IdentityRole> roleManager,
        IResend resend)
    {
        return await Registration(registration, userManager, db, roleManager, resend, false);
    }
    public static async Task<IResult> UserLogin(LoginDto login, 
        UserManager<User> userManager,
        IConfiguration config, 
        MyDbContext db)
    {
        return await Login(login, userManager, config, db, false);
    }
    public static async Task<IResult> UserConfirmation(MyDbContext db,
        UserManager<User> userManager,
        [FromForm] UserConfirmationDto confirmation)
    {
        var user = await userManager.FindByIdAsync(confirmation.Id.ToString());
        if (user is null) return Results.NotFound();
        if (user.EmailConfirmed || user.ConfirmationCode != confirmation.Code) return Results.BadRequest();
        user.EmailConfirmed = true;
        await LogService.Log(LogType.EmailConfirmation, $"{user.UserName} confirmed their email.", db, user);
        await db.SaveChangesAsync();
        return Results.Ok();
    }
    #endregion
}