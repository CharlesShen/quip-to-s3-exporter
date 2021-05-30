# quip-to-s3-exporter

Lambda function to automatically sync/export your Quip documents to an S3 bucket

## prerequisites

* .NET Core SDK
* Node.js (Current or LTS)
* AWS CDK Tookkit

## getting started

1. dotnet publish -c Release src\LambdaQuipToS3Exporter
2. cdk bootstrap [--profile &lt;name&gt;] (only once per region)
3. cdk deploy [--profile &lt;name&gt;]
