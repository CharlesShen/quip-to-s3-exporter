using Amazon.Lambda.Core;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace LambdaQuipToS3Exporter
{
    public class Function
    {
        public static Func<IServiceProvider> ConfigureServices = () =>
        {
            var serviceCollection = new ServiceCollection();

            //building configuration
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            var settings = configuration.Get<AppSettings>();
            serviceCollection.AddSingleton(settings);

            return serviceCollection.BuildServiceProvider();
        };

        static IServiceProvider services;

        static Function()
        {
            services = ConfigureServices();
        }

        private Regex blobRegex = new Regex(@"='/(blob/[A-Za-z_0-9]+/([A-Za-z_0-9]+))'", RegexOptions.Compiled);
        private const string ChangeDetectionMetadataKey = "x-amz-meta-updatedtimestamp";

        public void FunctionHandler(ILambdaContext context)
        {
            LambdaLogger.Log("ENVIRONMENT VARIABLES: " + JsonSerializer.Serialize(Environment.GetEnvironmentVariables()));
            LambdaLogger.Log("CONTEXT: " + JsonSerializer.Serialize(context));

            var settings = services.GetRequiredService<AppSettings>();

            if (((settings.DocumentIds?.Count() ?? 0) == 0) ||
                (settings.DocumentOutputPaths?.Count() ?? 0) == 0)
            {
                LambdaLogger.Log("Nothing defined for DocumentIds or DocumentOutputPaths. Quitting.");
            }

            if (settings.DocumentIds.Count() != settings.DocumentOutputPaths.Count())
            {
                throw new Exception("Mismatch in number of DocumentIds and DocumentOutputPaths defined.");
            }

            for (var i = 0; i < settings.DocumentIds.Count(); i++)
            {
                var documentId = settings.DocumentIds.ElementAt(i);
                var documentOutputPath = settings.DocumentOutputPaths.ElementAt(i);

                var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.QuipApiToken);

                var quipResponse = httpClient.GetStringAsync($"https://platform.quip-amazon.com/1/threads/{documentId}").Result;

                var quip = JsonSerializer.Deserialize<JsonElement>(quipResponse);
                var quipHtml = quip.GetProperty("html").GetString();
                var quipUpdatedTimestamp = quip.GetProperty("thread").GetProperty("updated_usec").GetUInt64();

                var appendHtml = settings.OutputQuipEditLink ? @$"<div class=""edit-quip""><a target=""_blank"" href=""https://quip-amazon.com/{documentId}"">Edit Page (requires permissions)</a></div>" : null;

                var dirtyDocument = false;
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes($"{settings.PrependText}{quipHtml}{settings.AppendText}")))
                {
                    dirtyDocument = PutObject(settings.S3BucketOutput, documentOutputPath, stream, "text/html", quipUpdatedTimestamp).Result;
                }

                // if quip document has been modified, also process the associated blobs (embedded images, etc)
                if (dirtyDocument)
                {
                    var blobMatches = blobRegex.Matches(quipHtml);
                    if (blobMatches.Any())
                    {
                        foreach (Match match in blobMatches)
                        {
                            var s3BlobKey = match.Groups[1].Value;
                            var blobId = match.Groups[2].Value;

                            using (var blobResponse = httpClient.GetAsync($"https://platform.quip-amazon.com/1/blob/{documentId}/{blobId}").Result)
                            using (var stream = blobResponse.Content.ReadAsStreamAsync().Result)
                            {
                                PutObject(settings.S3BucketOutput, s3BlobKey, stream, blobResponse.Content.Headers.ContentType.MediaType, quipUpdatedTimestamp).Wait();
                            }
                        }
                    }
                }
            }
        }

        public async Task<bool> PutObject(string s3Bucket, string s3Key, Stream data, string contentType, ulong lastUpdatedTimestamp)
        {
            var s3Client = new AmazonS3Client();

            var performUpdate = true;

            try
            {
                var currentObject = s3Client.GetObjectMetadataAsync(s3Bucket, s3Key).Result;
                var currentObjectTimestamp = Convert.ToUInt64(currentObject.Metadata[ChangeDetectionMetadataKey]);

                performUpdate = currentObjectTimestamp < lastUpdatedTimestamp;
            }
            catch (Exception) { }

            if (performUpdate)
            {
                var putQuipHtmlRequest = new PutObjectRequest()
                {
                    BucketName = s3Bucket,
                    Key = s3Key,
                    InputStream = data,
                    ContentType = contentType
                };
                putQuipHtmlRequest.Metadata.Add(ChangeDetectionMetadataKey, lastUpdatedTimestamp.ToString());

                await s3Client.PutObjectAsync(putQuipHtmlRequest);
            }

            return performUpdate;
        }
    }
}
