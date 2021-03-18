# lambda-quip-to-s3-exporter
Lambda function to automatically sync/export your Quip documents to an S3 bucket

Steps:
1. dotnet publish -c Release src\LambdaQuipToS3Exporter
2. cdk deploy