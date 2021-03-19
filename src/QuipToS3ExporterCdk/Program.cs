using Amazon.CDK;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LambdaQuipToS3ExporterCdk
{
    sealed class Program
    {
        public static void Main(string[] args)
        {
            var app = new App();
            new LambdaQuipToS3ExporterCdkStack(app, "LambdaQuipToS3ExporterCdkStack");
            app.Synth();
        }
    }
}
