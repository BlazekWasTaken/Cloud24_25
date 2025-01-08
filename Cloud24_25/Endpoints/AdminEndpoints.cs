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
        group.MapPost("/register", LoginService.AdminRegistration)
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
        group.MapPost("/login", LoginService.AdminLogin)
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
        group.MapGet("/get-logs", LogService.ListAllLogs)
            .WithName("AdminGetLogs")
            .WithTags("Admin")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .WithOpenApi(operation =>
            {
                operation.Summary = "Get All System Logs";
                operation.Description = "Retrieves all system logs. Requires Admin role.";
                operation.Responses["200"].Description = "Successfully retrieved system logs.";
                operation.Responses["401"].Description = "Unauthorized access.";
                operation.Responses["403"].Description = "Forbidden access.";
                return operation;
            });
    }
}