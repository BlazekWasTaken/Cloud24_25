using Cloud24_25.Service;
using Microsoft.OpenApi.Models;

namespace Cloud24_25.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/register", LoginService.UserRegistration)
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
        group.MapPost("/confirm-email", LoginService.UserConfirmation)
            .WithName("UserConfirmEmail")
            .WithTags("User")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .WithOpenApi(operation =>
            {
                operation.Summary = "Confirm new user email";
                operation.Description = "Confirms new user's email.";
                operation.Responses["200"].Description = "User's email confirmed successfully.";
                operation.Responses["400"].Description = "Email Confirmation failed.";
                return operation;
            })
            .DisableAntiforgery();
        group.MapPost("/login", LoginService.UserLogin)
            .WithName("UserLogin")
            .WithTags("User")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .WithOpenApi(operation =>
            {
                operation.Summary = "User login";
                operation.Description = "Authenticate as a regular user and receive a JWT token.";
                operation.Responses["200"].Description = "Login successful.";
                operation.Responses["401"].Description = "Invalid credentials.";
                operation.Responses["403"].Description = "User's email not confirmed.";
                return operation;
            });
        
        group.MapGet("/logs", LogService.ListUserLogs)
            .WithName("UserGetLogs")
            .WithTags("User")
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
        group.MapGet("/free-disk-space", FileService.GetFreeSpaceForUser)
            .WithName("UserGetFreeSpace")
            .WithTags("User")
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