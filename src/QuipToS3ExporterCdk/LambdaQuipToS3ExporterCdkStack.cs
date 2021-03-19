using Amazon.CDK;
using Amazon.CDK.AWS.Events;
using Amazon.CDK.AWS.Events.Targets;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.CloudFront;
using Amazon.CDK.AWS.CloudFront.Origins;

namespace LambdaQuipToS3ExporterCdk
{
    public class LambdaQuipToS3ExporterCdkStack : Stack
    {
        internal LambdaQuipToS3ExporterCdkStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            var s3Bucket = new Bucket(this, "bucket", new BucketProps
            {
                RemovalPolicy = RemovalPolicy.DESTROY,
                BlockPublicAccess = BlockPublicAccess.BLOCK_ALL
            });

            var cloudFrontDistribution = new Distribution(this, "cloudfront", new DistributionProps()
            {
                DefaultBehavior = new BehaviorOptions()
                {
                    Origin = new S3Origin(s3Bucket),
                    CachePolicy = CachePolicy.CACHING_DISABLED
                },
                DefaultRootObject = "index.html"
            });

            var function = new Function(this, "function", new FunctionProps
            {
                Runtime = Runtime.DOTNET_CORE_3_1,
                Code = Code.FromAsset("src/LambdaQuipToS3Exporter/bin/Release/netcoreapp3.1/publish"),
                Handler = "LambdaQuipToS3Exporter::LambdaQuipToS3Exporter.Function::FunctionHandler",
                MemorySize = 256,
                RetryAttempts = 0,
                ReservedConcurrentExecutions = 1,
                Timeout = Duration.Seconds(60)
            });

            function.AddEnvironment("S3BucketOutput", s3Bucket.BucketName);

            s3Bucket.GrantReadWrite(function);
            s3Bucket.GrantPut(function);
            s3Bucket.GrantDelete(function);

            var scheduleRule = new Rule(this, "schedule-rule", new RuleProps
            {
                Schedule = Schedule.Rate(Duration.Minutes(2)),
                Targets = new[] { new LambdaFunction(function) }
            });
        }
    }
}
