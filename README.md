# S3 to Blob

* A simple solution for monitoring an AWS S3 bucket and keep track of its contents. Upon detecting a new object being uploaded into the S3 bucket, it will be copied into an Azure Storage account (blob-only or general purpose, v1 or v2).
* This assumes that you have access to an S3 account (via a key and a secret) but cannot make midifications to the AWS account like creating an AWS Lambda function to spy on an S3 bucket and trigger the copying to the Azure Storage account.
