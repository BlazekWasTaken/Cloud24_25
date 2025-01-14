using Cloud24_25.Endpoints;
using Cloud24_25.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using Microsoft.AspNetCore.Http.Features;
using User = Cloud24_25.Infrastructure.Model.User;
using Resend;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions();

// CORS
const string corsPolicyName = "corsPolicy";
builder.Services.AddCors(options =>
{
    options.AddPolicy(corsPolicyName, corsPolicyBuilder => corsPolicyBuilder
        .WithOrigins("http://localhost:5173", "https://cloud.bdymek.com")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

// Resend
builder.Services.AddHttpClient<ResendClient>();
var token = Environment.GetEnvironmentVariable( "RESEND_APITOKEN" )!;
builder.Services.Configure<ResendClientOptions>(o => { o.ApiToken = token; });
builder.Services.AddTransient<IResend, ResendClient>();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Cloud24_25 API",
        Version = "v1",
        Description = "API for file management and user authorization",
        Contact = new OpenApiContact
        {
            Name = "API Support",
            Email = "support@bdymek.com"
        }
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme."
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

// DbContext
builder.Services.AddDbContext<MyDbContext>();

// Identity
builder.Services.AddIdentityCore<User>(options =>
{
    // Change after testing
    options.Password.RequiredLength = 3;
    options.Password.RequireDigit = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
    
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = true;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<MyDbContext>()
.AddSignInManager()
.AddDefaultTokenProviders();

// Managers
builder.Services.AddScoped<UserManager<User>>();
builder.Services.AddScoped<RoleManager<IdentityRole>>();

// JWT Auth
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudiences = [
            builder.Configuration["Jwt:Audience"],
            builder.Configuration["Jwt:AdminAudience"]
        ],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
    };
});

// Authorization
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"))
    .AddPolicy("UserOrAdmin", policy => policy.RequireRole("Admin", "User"));

// Kestrel config
builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = long.MaxValue;
});

builder.Services.Configure<FormOptions>(options =>
{
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartBodyLengthLimit = long.MaxValue;
    options.MultipartHeadersLengthLimit = int.MaxValue;
});

var app = builder.Build();

// Swagger/UI
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// CORS
app.UseCors(corsPolicyName);

app.UseAuthentication();
app.UseAuthorization();

// Endpoints
app.MapGroup("user").MapUserEndpoints();
app.MapGroup("admin").MapAdminEndpoints();
app.MapGroup("files").MapFileEndpoints();
app.MapGroup("revisions").MapRevisionEndpoints();

app.Run();