# quip-to-s3-exporter

Lambda function to automatically sync/export your Quip documents to an S3 bucket

## prerequisites

* .NET Core SDK (project based on current LTS 3.1)
    * https://dotnet.microsoft.com/download
* Node.js (Current or LTS)
    * https://nodejs.org/
* AWS CDK Toolkit
    * npm install -g aws-cdk
    * https://docs.aws.amazon.com/cdk/latest/guide/getting_started.html

## getting started

1. dotnet publish -c Release src\LambdaQuipToS3Exporter
2. cdk bootstrap [--profile &lt;name&gt;] (run once per region)
3. cdk deploy [--context &lt;app-name=UNIQUE_APP_NAME&gt;] [--profile &lt;name&gt;] (same command also used for updating stack. specifying "app-name" allows for multiple instances to be deployed.)
