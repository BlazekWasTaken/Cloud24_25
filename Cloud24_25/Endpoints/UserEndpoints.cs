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
        group.MapPost("/upload-file", FileService.UploadFileAsync)
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
        group.MapGet("/get-files", FileService.ListFiles)
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
        group.MapDelete("/delete-file/{fileId}", FileService.DeleteFile)
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
        group.MapGet("/download-file/{fileId}", FileService.DownloadFile)
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
        group.MapGet("/download-files", FileService.DownloadMultipleFiles)
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
        group.MapGet("/get-logs", LogService.ListUserLogs)
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
        group.MapGet("/get-free-space", FileService.GetFreeSpaceForUser)
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