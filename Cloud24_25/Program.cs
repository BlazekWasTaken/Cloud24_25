using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Cloud24_25.Infrastructure;
using Cloud24_25.Infrastructure.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var connectionString = "CONNECTION_STRING"; // TODO: use secret

var serverVersion = new MySqlServerVersion(new Version(9, 0, 2));

builder.Services.AddDbContext<MyDbContext>(
    dbContextOptions => dbContextOptions
        .UseMySql(connectionString, serverVersion)
        .LogTo(Console.WriteLine, LogLevel.Information)
        .EnableSensitiveDataLogging()
        .EnableDetailedErrors()
);

builder.Services.AddIdentityCore<IdentityUser>(options =>
    {
        options.Password.RequireDigit = false;
        options.Password.RequiredLength = 3;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<MyDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddScoped<UserManager<IdentityUser>>();
builder.Services.AddScoped<RoleManager<IdentityRole>>();

builder.Services.AddAuthentication()
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudiences =
            [
                builder.Configuration["Jwt:Audience"],
                builder.Configuration["Jwt:AdminAudience"]
            ],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();


app.MapPost("/register", async (UserRegistrationDto registration,
        UserManager<IdentityUser> userManager) =>
    {
        var user = new IdentityUser { UserName = registration.Username };
        var result = await userManager.CreateAsync(user, registration.Password);

        return result.Succeeded ? 
            Results.Ok(new { Message = "User registered successfully" }) : 
            Results.BadRequest(new { result.Errors });
    })
    .WithName("Register")
    .WithOpenApi();

app.MapPost("/login", async (LoginDto login, UserManager<IdentityUser> userManager,
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
    .WithName("Login")
    .WithOpenApi();

app.MapPost("/registerAdmin", async (UserRegistrationDto registration,
        UserManager<IdentityUser> userManager) =>
    {
        if (registration.Password != "admin") return Results.BadRequest(new { Message = "Wrong admin password." });
        var user = new IdentityUser { UserName = registration.Username };
        var result = await userManager.CreateAsync(user, registration.Password);

        return result.Succeeded ? 
            Results.Ok(new { Message = "Admin registered successfully" }) : 
            Results.BadRequest(new { result.Errors });
    })
    .WithName("RegisterAdmin")
    .WithOpenApi();

app.MapPost("/loginAdmin", async (LoginDto login, UserManager<IdentityUser> userManager,
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
    .WithName("LoginAdmin")
    .WithOpenApi();

app.MapGet("/helloworld", () => "Hello World!")
    .WithName("HelloWorld")
    .WithOpenApi();

app.MapGet("/authorizedHelloworld", [Authorize]() => "Hello World! You are authorized!")
    .WithName("AuthorizedHelloWorld")
    .WithOpenApi();

app.MapGet("/authorizedHelloworldAdmin", [Authorize](HttpContext context) =>
    {
        var user = context.User;

        var audienceClaim = user.FindFirstValue("aud");
        return audienceClaim == builder.Configuration["Jwt:AdminAudience"] ? 
            Results.Ok("Hello World! You are authorized as admin!") : Results.Forbid();
    })
    .WithName("AuthorizedHelloWorldAdmin")
    .WithOpenApi();

app.MapPost("/handle-file", async (IFormFile myFile) =>
    {
        // do something with file
    })
    .WithName("HandleFile")
    .DisableAntiforgery();

app.Run();