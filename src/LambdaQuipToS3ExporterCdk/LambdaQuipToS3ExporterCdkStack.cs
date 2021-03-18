using Amazon.CDK;
using Amazon.CDK.AWS.Events;
using Amazon.CDK.AWS.Events.Targets;
using Amazon.CDK.AWS.Lambda;

namespace LambdaQuipToS3ExporterCdk
{
    public class LambdaQuipToS3ExporterCdkStack : Stack
    {
        internal LambdaQuipToS3ExporterCdkStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            var function = new Function(this, "quip-to-s3-exporter", new FunctionProps
            {
                Runtime = Runtime.DOTNET_CORE_3_1,
                Code = Code.FromAsset("src/LambdaQuipToS3Exporter/bin/Release/netcoreapp3.1/publish"),
                Handler = "LambdaQuipToS3Exporter::LambdaQuipToS3Exporter.Function::FunctionHandler",
            });

            var scheduleRule = new Rule(this, "quip-to-s3-exporter-schedule", new RuleProps
            {
                Schedule = Schedule.Cron(new CronOptions { Minute = "2" }),
                Targets = new[] { new LambdaFunction(function) },
                Enabled = true
            });
        }
    }
}
