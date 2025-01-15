using Cloud24_25.Service;
using Microsoft.OpenApi.Models;

namespace Cloud24_25.Endpoints;

public static class RevisionEndpoints
{
    public static void MapRevisionEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("{revId}", FileService.DownloadRevision)
            .WithName("DownloadRevision")
            .WithTags("Revisions")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .WithOpenApi(operation =>
            {
                operation.Summary = "Download a Revision";
                operation.Description = "Downloads a specific file by its ID.";

                var fileIdParam = operation.Parameters.FirstOrDefault(p => 
                    p.Name.Equals("revId", StringComparison.OrdinalIgnoreCase) && p.In == ParameterLocation.Path);
                if (fileIdParam != null) fileIdParam.Description = "The unique identifier of the revision to download.";

                operation.Responses["200"].Description = "Revision downloaded successfully.";
                operation.Responses["401"].Description = "Unauthorized access.";
                operation.Responses["404"].Description = "Revision not found.";
                return operation;
            });
        
        group.MapDelete("{revId}", FileService.DeleteRevision)
            .WithName("DeleteRevision")
            .WithTags("Revisions")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .WithOpenApi(operation =>
            {
                operation.Summary = "Delete a Revision";
                operation.Description = "Deletes a specific revision by its ID.";

                var fileIdParam = operation.Parameters.FirstOrDefault(p => 
                    p.Name.Equals("revId", StringComparison.OrdinalIgnoreCase) && p.In == ParameterLocation.Path);
                if (fileIdParam != null) fileIdParam.Description = "The unique identifier of the revision to delete.";

                operation.Responses["200"].Description = "Revision deleted successfully.";
                operation.Responses["400"].Description = "Invalid revision ID.";
                operation.Responses["401"].Description = "Unauthorized access.";
                operation.Responses["404"].Description = "Revision not found.";
                return operation;
            });
    }
}