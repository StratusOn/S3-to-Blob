using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

public static class S3Util
{
    private static string awsSecretKey;

    public static async Task<IActionResult> ListingObjectsAsync(HttpRequest req, TraceWriter log)
    {
        // Reference: https://docs.aws.amazon.com/AmazonS3/latest/dev/ListingObjectKeysUsingNetSDK.html
        var bucketName = Environment.GetEnvironmentVariable("S3BucketName", EnvironmentVariableTarget.Process);
        var bucketRegion = RegionEndpoint.GetBySystemName(Environment.GetEnvironmentVariable("S3BucketRegion", EnvironmentVariableTarget.Process));

        if (awsSecretKey == null)
        {
            log.Info($"Fetching AWS secret key for the first time from KeyVault...");
            var azureServiceTokenProvider = new AzureServiceTokenProvider();
            var keyVaultClient = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(azureServiceTokenProvider.KeyVaultTokenCallback));
            var secretName = Environment.GetEnvironmentVariable("AmazonS3SecretAccessKeySecretName", EnvironmentVariableTarget.Process);
            var azureKeyVaultUrl = Environment.GetEnvironmentVariable("AzureKeyVaultUrl", EnvironmentVariableTarget.Process);
            var secret = await keyVaultClient.GetSecretAsync($"{azureKeyVaultUrl}secrets/{secretName}").ConfigureAwait(false);
            awsSecretKey = secret.Value;
            log.Info("[Setting]: Successfully fetched AWS secret key from KeyVault.");
        }

        var credentials = new Amazon.Runtime.BasicAWSCredentials(Environment.GetEnvironmentVariable("AwsAccessKey", EnvironmentVariableTarget.Process), awsSecretKey);

        string s3BucketLastProcessedDateTimeUtcAsString = req.Query["s3BucketLastProcessedDateTimeUtc"]; // GET
        string requestBody = new StreamReader(req.Body).ReadToEnd(); // POST
        dynamic data = JsonConvert.DeserializeObject(requestBody);
        s3BucketLastProcessedDateTimeUtcAsString = s3BucketLastProcessedDateTimeUtcAsString ?? data?.s3BucketLastProcessedDateTimeUtc;
        if (string.IsNullOrWhiteSpace(s3BucketLastProcessedDateTimeUtcAsString))
        {
            string errorMessage = "A 's3BucketLastProcessedDateTimeUtc' querystring parameter or a request body containing a JSON object with a 's3BucketLastProcessedDateTimeUtc' property was expected but not found.";
            log.Info(errorMessage);
            return new BadRequestObjectResult(errorMessage);
        }
        var s3BucketLastProcessedDateTimeUtc = DateTime.Parse(s3BucketLastProcessedDateTimeUtcAsString);

        log.Info($"Bucket Name: {bucketName}.");
        log.Info($"Bucket Region: {bucketRegion}.");
        log.Info($"S3 Bucket Last Processed DateTimeUtc: {s3BucketLastProcessedDateTimeUtcAsString}.");

        List<S3Object> filteredObjects = new List<S3Object>();
        int totalUnfilteredCount = 0;
        int currentUnfilteredCount = 0;
        DateTime newLastProcessedDateTimeUtc = DateTime.UtcNow;
        IAmazonS3 client = new AmazonS3Client(credentials, bucketRegion);
        try
        {
            ListObjectsV2Request request = new ListObjectsV2Request
            {
                BucketName = bucketName
            };

            ListObjectsV2Response response;
            do
            {
                response = await client.ListObjectsV2Async(request);
                currentUnfilteredCount = response.S3Objects.Count;
                totalUnfilteredCount += currentUnfilteredCount;
                log.Info($"Results Count (pre-filtering): {currentUnfilteredCount}.");
                var currentFilteredObjects = response.S3Objects.FindAll((s3Object) =>
                {
                    // Return objects updated after the last process date and that are not folder records (end with _$folder$ and have 0 size).
                    return DateTime.Compare(s3Object.LastModified.ToUniversalTime(), s3BucketLastProcessedDateTimeUtc) > 0
                        & !(s3Object.Key.EndsWith("_$folder$", StringComparison.InvariantCulture) && s3Object.Size == 0);
                });
                log.Info($"Results Count (post-filtering): {currentFilteredObjects.Count}.");
                filteredObjects.AddRange(currentFilteredObjects);

                log.Info($"Next Continuation Token: {response.NextContinuationToken}.");
                request.ContinuationToken = response.NextContinuationToken;
            } while (response.IsTruncated);

            log.Info($"Results Count (total-unfiltered): {totalUnfilteredCount}.");
            log.Info($"Results Count (total-filtered): {filteredObjects.Count}.");
            dynamic payload = new System.Dynamic.ExpandoObject();
            payload.s3Objects = filteredObjects;
            payload.newLastProcessedDateTimeUtc = newLastProcessedDateTimeUtc.ToString();
            return new OkObjectResult(JsonConvert.SerializeObject(payload, Formatting.Indented));
        }
        catch (AmazonS3Exception amazonS3Exception)
        {
            log.Info($"AmazonS3Exception [ListingObjectsAsync]: {amazonS3Exception.ToString()}.");
            return new BadRequestObjectResult("Operation failed (AmazonS3Exception). Check function's log for details.");
        }
        catch (Exception exception)
        {
            log.Info($"Exception [ListingObjectsAsync]: {exception.ToString()}.");
            return new BadRequestObjectResult("Operation failed. Check function's log for details.");
        }
    }
}
