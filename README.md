# quip-to-s3-exporter

Lambda function to automatically sync/export your Quip documents to an S3 bucket

## prerequisites

* .NET Core SDK
* Node.js (Current or LTS)
* AWS CDK Tookkit

## getting started

1. dotnet publish -c Release src\LambdaQuipToS3Exporter
2. cdk bootstrap [--profile &lt;name&gt;] (run once per region)
3. cdk deploy [--context &lt;app-name=UNIQUE_APP_NAME&gt;] [--profile &lt;name&gt;] (same command also used for updating stack. specifying "app-name" allows for multiple instances to be deployed.)
