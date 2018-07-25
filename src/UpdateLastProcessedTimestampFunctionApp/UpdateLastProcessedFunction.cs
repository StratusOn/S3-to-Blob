//#r "Newtonsoft.Json"
//#r "Microsoft.WindowsAzure.Storage"
//#load "AzureTableUtil.cs"

using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Host;
using System.Threading.Tasks;

namespace UpdateLastProcessedTimestampFunctionApp
{
    public static class UpdateLastProcessedFunction
    {
        [FunctionName("UpdateLastProcessed")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequest req, TraceWriter log)
        {
            log.Info("UpdateLastProcessed function processed a request.");
            IActionResult response = await AzureTableUtil.UpdateLastProcessedTimestampInTable(req, log);
            return response;
        }
    }
}
