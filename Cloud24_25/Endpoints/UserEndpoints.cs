using Cloud24_25.Infrastructure;
using Cloud24_25.Infrastructure.Dtos;
using Cloud24_25.Infrastructure.Model;
using Cloud24_25.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using HttpContext = Microsoft.AspNetCore.Http.HttpContext;

namespace Cloud24_25.Endpoints;

public static class UserEndpoints
{
    private const int MaxUsers = 10;
    
    public static void MapUserEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/register", async (
                UserRegistrationDto registration,
                UserManager<User> userManager,
                MyDbContext db) =>
            {
                if (db.Users.Count() >= MaxUsers)
                {
                    await LogService.Log(LogType.RegisterAttempt, $"User {registration.Username}'s attempt to register failed, max user count exceeded", db, null);
                    return Results.BadRequest(new { Message = "Max user count exceeded" });
                }
                var user = new User { UserName = registration.Username, Files = [], Logs = [] };
                var result = await userManager.CreateAsync(user, registration.Password);

                if (result.Succeeded)
                {
                    await LogService.Log(LogType.Register,$"User {registration.Username} successfully registered", db, user);
                    return Results.Ok(new { Message = "User registered successfully" });
                }
                else
                {
                    await LogService.Log(LogType.RegisterAttempt, $"User {registration.Username}'s attempt to register failed", db, null);
                    return Results.BadRequest(new { result.Errors });
                }
            })
            .WithName("UserRegister")
            .WithTags("User")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .WithOpenApi(operation =>
            {
                operation.Summary = "Register a new user";
                operation.Description = "Creates a new regular user account.";
                operation.Responses["200"].Description = "User registered successfully.";
                operation.Responses["400"].Description = "Registration failed.";
                return operation;
            });

        group.MapPost("/login", async (LoginDto login, UserManager<User> userManager,
            IConfiguration config, MyDbContext db) =>
            {
                var user = await userManager.FindByNameAsync(login.Username);
                if (user == null || !await userManager.CheckPasswordAsync(user, login.Password))
                {
                    await LogService.Log(LogType.LoginAttempt, $"There was an attempt to login as {login.Username}, but password was incorrect.", db, user);
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

                await LogService.Log(LogType.Login, $"User {login.Username} logged in.", db, user);
                return Results.Ok(new
                {
                    token = new JwtSecurityTokenHandler().WriteToken(token)
                });
            })
            .WithName("UserLogin")
            .WithTags("User")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .WithOpenApi(operation =>
            {
                operation.Summary = "User login";
                operation.Description = "Authenticate as a regular user and receive a JWT token.";
                operation.Responses["200"].Description = "Login successful.";
                operation.Responses["401"].Description = "Invalid credentials.";
                return operation;
            });

        group.MapGet("/authorized-hello-world", [Authorize] () => "Hello World! You are authorized!")
            .WithName("UserAuthorizedHelloWorld")
            .WithTags("Authorization")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .WithOpenApi(operation =>
            {
                operation.Summary = "Authorized Hello World for Users";
                operation.Description = "Returns a greeting message for authorized users.";
                operation.Responses["200"].Description = "Successfully retrieved greeting message.";
                operation.Responses["401"].Description = "Unauthorized access.";
                return operation;
            });

        group.MapPost("/upload-file",
                async (HttpContext context, IFormFile myFile, [FromForm] StringValues fileHashes, MyDbContext db) =>
                    await FileService.UploadFileAsync(context, myFile, fileHashes.ToList(), db))
            .WithName("UploadFile")
            .WithTags("Files")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .WithOpenApi(operation =>
            {
                operation.Summary = "Upload a File";
                operation.Description = "Allows users to upload a file to the system.";
                operation.Responses["200"].Description = "File uploaded successfully.";
                operation.Responses["400"].Description = "File upload failed.";
                return operation;
            })
            .DisableAntiforgery();

        group.MapGet("/get-files", (HttpContext context, MyDbContext db) => FileService.ListFiles(context, db))
            .WithName("UserGetFiles")
            .WithTags("Files")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .WithOpenApi(operation =>
            {
                operation.Summary = "List User Files";
                operation.Description = "Retrieves a list of all files uploaded by the user.";
                operation.Responses["200"].Description = "Successfully retrieved user files.";
                operation.Responses["401"].Description = "Unauthorized access.";
                return operation;
            });
        
        group.MapPost("/get-file-hash", (IFormFile myFile) => FileService.GetHash(myFile.OpenReadStream()))
            .WithName("UserGetFileHash")
            .WithTags("Files")
            .Produces(StatusCodes.Status200OK)
            .WithOpenApi(operation =>
            {
                operation.Summary = "Calculate File Hash";
                operation.Description = "Calculates the hash of a file.";
                operation.Responses["200"].Description = "Successfully calculated hash.";
                return operation;
            })
            .DisableAntiforgery();

        group.MapDelete("/delete-file/{fileId}", async (HttpContext context, MyDbContext db, Guid fileId) =>
            await FileService.DeleteFile(context, db, fileId))
            .WithName("UserDeleteFile")
            .WithTags("Files")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .WithOpenApi(operation =>
            {
                operation.Summary = "Delete a File";
                operation.Description = "Deletes a specific file by its ID.";

                var fileIdParam = operation.Parameters.FirstOrDefault(p => p.Name.Equals("fileId", StringComparison.OrdinalIgnoreCase) && p.In == ParameterLocation.Path);

                if (fileIdParam != null)
                {
                    fileIdParam.Description = "The unique identifier of the file to delete.";
                }

                operation.Responses["200"].Description = "File deleted successfully.";
                operation.Responses["400"].Description = "Invalid file ID.";
                operation.Responses["401"].Description = "Unauthorized access.";
                operation.Responses["404"].Description = "File not found.";
                return operation;
            });

        group.MapGet("/download-file/{fileId}", async (HttpContext context, MyDbContext db, Guid fileId) =>
                await FileService.DownloadFile(context, db, fileId))
            .WithName("UserDownloadFile")
            .WithTags("Files")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .WithOpenApi(operation =>
            {
                operation.Summary = "Download a File";
                operation.Description = "Downloads a specific file by its ID.";

                var fileIdParam = operation.Parameters.FirstOrDefault(p => p.Name.Equals("fileId", StringComparison.OrdinalIgnoreCase) && p.In == ParameterLocation.Path);

                if (fileIdParam != null)
                {
                    fileIdParam.Description = "The unique identifier of the file to download.";
                }

                operation.Responses["200"].Description = "File downloaded successfully.";
                operation.Responses["401"].Description = "Unauthorized access.";
                operation.Responses["404"].Description = "File not found.";
                return operation;
            });

        group.MapGet("/download-files", async (HttpContext context, MyDbContext db, [FromQuery] Guid[] ids) =>
            await FileService.DownloadMultipleFiles(context, db, ids.ToList()))
            .WithName("UserDownloadFiles")
            .WithTags("Files")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .WithOpenApi(operation =>
            {
                operation.Summary = "Download files";
                operation.Description = "Downloads multiple files by their IDs.";

                operation.Responses["200"].Description = "File downloaded successfully.";
                operation.Responses["401"].Description = "Unauthorized access.";
                operation.Responses["404"].Description = "File not found.";
                return operation;
            });
        group.MapGet("/get-logs", (HttpContext context, MyDbContext db) => LogService.ListUserLogs(context, db))
            .WithName("UserGetLogs")
            .WithTags("Logs")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .WithOpenApi(operation =>
            {
                operation.Summary = "List User Logs";
                operation.Description = "Retrieves a list of all logs for the user.";
                operation.Responses["200"].Description = "Successfully retrieved user logs.";
                operation.Responses["401"].Description = "Unauthorized access.";
                return operation;
            });
        group.MapGet("/get-free-space", (HttpContext context, MyDbContext db) => FileService.GetFreeSpaceForUser(context, db))
            .WithName("UserGetFreeSpace")
            .WithTags("Files")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .WithOpenApi(operation =>
            {
                operation.Summary = "Get User's Free Space";
                operation.Description = "Returns the amount of free space left for the user in bytes.";
                operation.Responses["200"].Description = "Successfully retrieved user's free space.";
                operation.Responses["401"].Description = "Unauthorized access.";
                return operation;
            });
    }
}