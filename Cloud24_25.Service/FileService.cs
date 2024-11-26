using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Cloud24_25.Infrastructure;
using Cloud24_25.Infrastructure.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Oci.Common;
using Oci.Common.Auth;
using Oci.Common.Retry;
using Oci.ObjectstorageService;
using Oci.ObjectstorageService.Requests;
using Oci.ObjectstorageService.Responses;
using File = Cloud24_25.Infrastructure.Model.File;

namespace Cloud24_25.Service;

public static class FileService
{
    private const string BucketName = "bucket-20241022-1249";
    private const string NamespaceName = "frgrfeumviow";
    private const int NumberOfRevisions = 5;

    private static readonly ObjectStorageClient Client = new(
        new ConfigFileAuthenticationDetailsProvider("DEFAULT"),
        new ClientConfiguration
        {
            ClientUserAgent = "Cloud24_25",
            RetryConfiguration = new RetryConfiguration
            {
                // maximum number of attempts to retry the same request
                MaxAttempts = 5,
                // retries the request if the response status code is in the range [400-499] or [500-599]
                RetryableStatusCodeFamilies = [4, 5]
            }
        });

    public static async Task<IResult> UploadFileAsync(
        HttpContext context, 
        IFormFile myFile,
        MyDbContext db)
    {
        var endpointUser = context.User;
        var username = endpointUser.Identity?.Name;
        if (username == null) return Results.Unauthorized();

        File fileObject;
        if (!db.Users.Any() || !db.Users.Any(x => x.UserName == username)) return Results.NotFound();
        var user = db.Users
            .Include(x => x.Files)
            .ThenInclude(x => x.Revisions)
            .First(x => x.UserName == username);
        var userFiles = user.Files;
        
        if (userFiles.Any(x => x.Name == myFile.FileName))
        {
            fileObject = userFiles.First(x => x.Name == myFile.FileName);
        }
        else
        { 
            fileObject = new File
            { 
                Id = Guid.NewGuid(),
                Name = myFile.FileName, 
                ContentType = myFile.ContentType,
                Created = DateTime.UtcNow,
                Modified = DateTime.UtcNow,
                Revisions = []
            };
        }

        int revisionNumber;
        if (fileObject.Revisions.Count == 0)
        {
            revisionNumber = 1;
        }
        else
        {
            revisionNumber = int.Parse(fileObject.Revisions.OrderBy(x => x.Created).Last().ObjectName.Split('@').Last()) + 1;
        }
        
        FileRevision fileRevisionObject = new()
        {
            Id = Guid.NewGuid(),
            Created = DateTime.UtcNow,
            ObjectName = $"{user.UserName}@{fileObject.Name}@{revisionNumber}"
        };
        fileObject.Revisions.Add(fileRevisionObject);
        
        if (userFiles.All(x => x.Name != myFile.FileName))
        {
            await db.Files.AddAsync(fileObject);
        }
        else
        {
            db.Files.Update(fileObject);
        }
        
        await db.FileRevisions.AddAsync(fileRevisionObject);
        user.Files.Add(fileObject);
        db.Users.Update(user);

        try
        {
            await PutObject(fileRevisionObject.ObjectName, myFile.OpenReadStream());
            if (fileObject.Revisions.Count > NumberOfRevisions)
            {
                var toDelete = fileObject.Revisions.OrderBy(x => x.Created).First();
                fileObject.Revisions.Remove(toDelete);
                db.FileRevisions.Remove(toDelete);
                await DeleteObject(toDelete.ObjectName);
            }

            await db.SaveChangesAsync();
        }
        catch (Exception e)
        {
            throw;
        }
        
        return Results.Ok();
    }

    public static IResult ListFiles(
        HttpContext context, 
        MyDbContext db)
    {
        var endpointUser = context.User;
        var username = endpointUser.Identity?.Name;
        if (username == null) return Results.Unauthorized();
        if (!db.Users.Any() || !db.Users.Any(x => x.UserName == username)) return Results.NotFound();
        
        var user = db.Users
            .Include(x => x.Files)
            .ThenInclude(x => x.Revisions)
            .First(x => x.UserName == username);
        var userFiles = user.Files;
        
        return Results.Ok(userFiles);
    }

    public static async Task<IResult> DeleteFile(
        HttpContext context,
        MyDbContext db,
        Guid id) 
    {
        var endpointUser = context.User;
        var username = endpointUser.Identity?.Name;
        if (username == null) return Results.Unauthorized();
        if (!db.Users.Any() || !db.Users.Any(x => x.UserName == username)) return Results.NotFound();
        
        var user = db.Users
            .Include(x => x.Files)
            .ThenInclude(x => x.Revisions)
            .First(x => x.UserName == username);
        var userFiles = user.Files;

        var fileToDelete = userFiles.First(x => x.Id == id);
        var revisionsToDelete = fileToDelete.Revisions;

        try
        {
            foreach (var revision in revisionsToDelete)
            {
                await DeleteObject(revision.ObjectName);
                db.FileRevisions.Remove(revision);
            }
            
            db.Files.Remove(fileToDelete);
            user.Files.Remove(fileToDelete);
            db.Users.Update(user);
            
            await db.SaveChangesAsync();
        }
        catch (Exception e)
        {
            await db.SaveChangesAsync();
            throw;
        }

        return Results.Ok();
    }
    
    public static async Task<IResult> DownloadFile(
        HttpContext context,
        MyDbContext db,
        Guid id)
    {
        var endpointUser = context.User;
        var username = endpointUser.Identity?.Name;
        if (username == null) return Results.Unauthorized();
        if (!db.Users.Any() || !db.Users.Any(x => x.UserName == username)) return Results.NotFound();
        
        var user = db.Users
            .Include(x => x.Files)
            .ThenInclude(x => x.Revisions)
            .First(x => x.UserName == username);
        var userFiles = user.Files;

        var fileToDownload = userFiles.First(x => x.Id == id);
        var revisionToDownload = fileToDownload.Revisions.OrderBy(x => x.Created).Last();
        
        var objectStream = await GetObject(revisionToDownload.ObjectName);
        if (objectStream.InputStream is null) return Results.NotFound();
        
        return Results.File(objectStream.InputStream, fileToDownload.ContentType, fileToDownload.Name);
    }
    
    private static async Task<PutObjectResponse> PutObject(string objectName, Stream file)
    { 
        var putObjectRequest = new PutObjectRequest
        {
            BucketName = BucketName,
            NamespaceName = NamespaceName,
            ObjectName = objectName,
            PutObjectBody = file
        };
        
        return await Client.PutObject(putObjectRequest);
    }

    private static async Task<GetObjectResponse> GetObject(string objectName)
    {
        var getObjectRequest = new GetObjectRequest
        {
            BucketName = BucketName,
            NamespaceName = NamespaceName,
            ObjectName = objectName
        };

        return await Client.GetObject(getObjectRequest);
    }

    private static async Task<DeleteObjectResponse> DeleteObject(string objectName)
    {
        var deleteObjectRequest = new DeleteObjectRequest
        {
            BucketName = BucketName,
            NamespaceName = NamespaceName,
            ObjectName = objectName
        };

        return await Client.DeleteObject(deleteObjectRequest);
    }
}