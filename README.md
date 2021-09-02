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
2. cdk bootstrap [--profile &lt;name&gt;]
    * Only have to run this once per account/region.
    * "--profile" relates to the AWS CLI profile (https://docs.aws.amazon.com/cdk/latest/guide/environments.html).  Leave blank to use the default profile.
3. cdk deploy [--context &lt;app-name=UNIQUE_APP_NAME&gt;] [--profile &lt;name&gt;]
    * Use same command for updating the stack.
    * Specifying "app-name" allows for multiple instances to be deployed to the same account/region.
