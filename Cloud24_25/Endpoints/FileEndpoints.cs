using Cloud24_25.Service;
using Microsoft.OpenApi.Models;

namespace Cloud24_25.Endpoints;

public static class FileEndpoints
{
    public static void MapFileEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/list", FileService.ListFiles)
            .WithName("GetList")
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
        group.MapGet("/", FileService.DownloadMultipleFiles)
            .WithName("DownloadZip")
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
        group.MapGet("/{fileId}", FileService.DownloadFile)
            .WithName("DownloadFile")
            .WithTags("Files")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .WithOpenApi(operation =>
            {
                operation.Summary = "Download a File";
                operation.Description = "Downloads a specific file by its ID.";

                var fileIdParam = operation.Parameters.FirstOrDefault(p => 
                    p.Name.Equals("fileId", StringComparison.OrdinalIgnoreCase) && p.In == ParameterLocation.Path);
                if (fileIdParam != null) fileIdParam.Description = "The unique identifier of the file to download.";

                operation.Responses["200"].Description = "File downloaded successfully.";
                operation.Responses["401"].Description = "Unauthorized access.";
                operation.Responses["404"].Description = "File not found.";
                return operation;
            });
        
        group.MapPost("/upload", FileService.UploadFileAsync)
            .WithName("UploadFile")
            .WithTags("Files")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .WithOpenApi(operation =>
            {
                operation.Summary = "Upload a File";
                operation.Description = "Allows users to upload a file to the system. " +
                                        "Requires file hash(es) to be provided. " + 
                                        "Hashes are calculated using SHA-512 and formatted as Base64 strings." +
                                        "The proper way to do this in linux is: openssl sha512 -binary [file] | base64 -";
                
                operation.Responses["200"].Description = "File uploaded successfully.";
                operation.Responses["400"].Description = "File upload failed.";
                return operation;
            })
            .DisableAntiforgery();
        
        group.MapPost("/hash", (IFormFile myFile) => FileService.GetHash(myFile.OpenReadStream()))
            .WithName("GetFileHash")
            .WithTags("Files")
            .Produces(StatusCodes.Status200OK)
            .WithOpenApi(operation =>
            {
                operation.Summary = "Calculate File Hash";
                operation.Description = "Calculates the hash of a file using SHA-512. " +
                                        "Returns the hash as a Base64 string.";
                operation.Responses["200"].Description = "Successfully calculated hash.";
                return operation;
            })
            .DisableAntiforgery();
        
        group.MapDelete("/{fileId}", FileService.DeleteFile)
            .WithName("DeleteFile")
            .WithTags("Files")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .WithOpenApi(operation =>
            {
                operation.Summary = "Delete a File";
                operation.Description = "Deletes a specific file by its ID.";

                var fileIdParam = operation.Parameters.FirstOrDefault(p => 
                    p.Name.Equals("fileId", StringComparison.OrdinalIgnoreCase) && p.In == ParameterLocation.Path);
                if (fileIdParam != null) fileIdParam.Description = "The unique identifier of the file to delete.";

                operation.Responses["200"].Description = "File deleted successfully.";
                operation.Responses["400"].Description = "Invalid file ID.";
                operation.Responses["401"].Description = "Unauthorized access.";
                operation.Responses["404"].Description = "File not found.";
                return operation;
            });
        
    }
}