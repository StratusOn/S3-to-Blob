# S3 to Blob

A simple solution for monitoring an AWS S3 bucket and keep track of its contents. Upon detecting a new object being uploaded into the S3 bucket, it will be copied into an Azure Storage account (blob-only or general purpose, v1 or v2). This solution utilizes Azure Data Factory v2.

## Deployment
You can deploy this solution by clicking on the following button:

[![Deploy to Azure](http://azuredeploy.net/deploybutton.png)](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2FStratusOn%2FS3-to-Blob%2Fmaster%2Fsrc%2FDeployments%2Fazuredeploy.json)

* Once the deployment finishes (it takes about 2 minutes), go to the resource group, click on the Azure Data Factory resource that was created and then click on "Author & Monitor".
* Click on the "Author" button on the left navigation menu. This loads the editor.
* Expand the "Pipelines" list and click on the "MasterCopyFromAmazonS3ToAzureBlob" pipeline. Click on the "Debug" button to test it.
* Under triggers, located the "SyncObjectsTrigger" trigger and edit it and check the "Activated" checkbox (it's unchecked by default). This trigger is configured to run once every 1 hour.

## Assumptions
* This assumes that you have access to an S3 account (via a key and a secret) but cannot make midifications to the AWS account like creating an AWS Lambda function to spy on an S3 bucket and trigger the copying to the Azure Storage account.
