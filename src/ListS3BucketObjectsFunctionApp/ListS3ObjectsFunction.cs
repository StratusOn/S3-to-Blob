//#r "Newtonsoft.Json"
//#load "S3Util.cs"

using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Host;
using System.Threading.Tasks;

namespace ListS3BucketObjectsFunctionApp
{
    public static class ListS3ObjectsFunction
    {
        [FunctionName("ListS3Objects")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequest req, TraceWriter log)
        {
            log.Info("ListS3Objects function processed a request.");
            IActionResult response = await S3Util.ListingObjectsAsync(req, log);
            return response;
        }
    }
}
