using Oci.Common;
using Oci.Common.Auth;
using Oci.Common.Retry;
using Oci.ObjectstorageService;
using Oci.ObjectstorageService.Requests;
using Oci.ObjectstorageService.Responses;

namespace Cloud24_25.Service;

public static class FileService
{
    private const string BucketName = "bucket-20241022-1249";
    private const string NamespaceName = "frgrfeumviow";

    private static readonly ObjectStorageClient Client = new(
        new ConfigFileAuthenticationDetailsProvider("DEFAULT"),
        new ClientConfiguration
        {
            ClientUserAgent = "DotNet-SDK-Example",
            RetryConfiguration = new RetryConfiguration
            {
                // maximum number of attempts to retry the same request
                MaxAttempts = 5,
                // retries the request if the response status code is in the range [400-499] or [500-599]
                RetryableStatusCodeFamilies = [4, 5]
            }
        });
    
    public static async Task UploadFileAsync(string fileName, Stream file)
    {
        try
        {
            var bb = await PutObject(Client, fileName, file);
            var aa = await GetObject(Client, fileName);
        }
        catch (Exception e)
        {
            // ignored
        }
        finally
        {
            Client.Dispose();
        }
    }
    
    private static async Task<PutObjectResponse> PutObject(this ObjectStorageClient client, string objectName, Stream file)
    { 
        var putObjectRequest = new PutObjectRequest
        {
            BucketName = BucketName,
            NamespaceName = NamespaceName,
            ObjectName = objectName,
            PutObjectBody = file
        };
        
        return await client.PutObject(putObjectRequest);
    }

    private static async Task<GetObjectResponse> GetObject(ObjectStorageClient osClient, string objectName)
    {
        var getObjectObjectRequest = new GetObjectRequest()
        {
            BucketName = BucketName,
            NamespaceName = NamespaceName,
            ObjectName = objectName
        };

        return await osClient.GetObject(getObjectObjectRequest);
    }
}