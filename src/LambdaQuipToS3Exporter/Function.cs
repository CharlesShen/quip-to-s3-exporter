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
        static IServiceProvider services;
        static Function()
        {
            services = ConfigureServices();
        }

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

            serviceCollection.AddHttpClient("quip", c =>
            {
                c.BaseAddress = new Uri($"https://platform.{settings.QuipDomain}/");
                c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.QuipApiToken);
            });

            return serviceCollection.BuildServiceProvider();
        };

        private Regex blobRegex = new Regex(@"='/(blob/[A-Za-z_0-9]+/([A-Za-z_0-9]+))'", RegexOptions.Compiled);
        private const string ChangeDetectionMetadataKey = "x-amz-meta-updatedtimestamp";

        public void FunctionHandler(ILambdaContext context)
        {
            LambdaLogger.Log("ENVIRONMENT VARIABLES: " + JsonSerializer.Serialize(Environment.GetEnvironmentVariables()));
            LambdaLogger.Log("CONTEXT: " + JsonSerializer.Serialize(context));

            var settings = services.GetRequiredService<AppSettings>();
            var httpClientFactory = services.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient("quip");

            if (((settings.DocumentIds?.Count() ?? 0) == 0) ||
                (settings.DocumentOutputPaths?.Count() ?? 0) == 0)
            {
                LambdaLogger.Log("Nothing defined for DocumentIds or DocumentOutputPaths. Quitting.");
                return;
            }

            if (settings.DocumentIds.Count() != settings.DocumentOutputPaths.Count())
            {
                throw new Exception("Mismatch in number of DocumentIds and DocumentOutputPaths defined.");
            }

            for (var i = 0; i < settings.DocumentIds.Count(); i++)
            {
                var documentId = settings.DocumentIds.ElementAt(i);
                var documentOutputPath = settings.DocumentOutputPaths.ElementAt(i);

                LambdaLogger.Log($"Processing DocumentId: {documentId}");

                var quipClient = new QuipApi.Client(httpClient);
                var document = quipClient.GetThread(documentId).Result;
                var staleObject = IsObjectStale(settings.S3BucketOutput, documentOutputPath, document.thread.updated_usec);

                if (staleObject)
                {
                    LambdaLogger.Log($"DocumentId {documentId} has been modified, syncing changes.");

                    switch (document.thread.type)
                    {
                        case "document":
                            {
                                var editLinkHtml = settings.OutputQuipEditLink ? @$"<div class=""edit-quip""><a target=""_blank"" href=""{document.thread.link}"">Edit Page (requires permissions)</a></div>" : null;
                                var html = $"{settings.PrependHtml}{document.html}{editLinkHtml}{settings.AppendHtml}";
                                PutObject(settings.S3BucketOutput, documentOutputPath, Encoding.UTF8.GetBytes(html), "text/html", document.thread.updated_usec).Wait();

                                var blobMatches = blobRegex.Matches(document.html);
                                if (blobMatches.Any())
                                {
                                    foreach (Match match in blobMatches)
                                    {
                                        var s3BlobKey = match.Groups[1].Value;
                                        var blobId = match.Groups[2].Value;

                                        var blob = quipClient.GetThreadBlob(documentId, blobId).Result;
                                        PutObject(settings.S3BucketOutput, s3BlobKey, blob.Data, blob.MediaType, document.thread.updated_usec).Wait();
                                    }
                                }

                                break;
                            }
                        case "spreadsheet":
                            {
                                var json = quipClient.ExportSpreadsheetToJson(document).Result;
                                PutObject(settings.S3BucketOutput, documentOutputPath, Encoding.UTF8.GetBytes(json), "application/json", document.thread.updated_usec).Wait();

                                break;
                            }
                        default:
                            {
                                LambdaLogger.Log($@"Skipping... Unknown document type ""{document.thread.type}"".");
                                continue;
                            }
                    }

                    LambdaLogger.Log($"DocumentId {documentId} successfully synced.");
                }
                else
                {
                    LambdaLogger.Log($"Document has not been modified since last sync... skipping.");
                }
            }
        }

        public bool IsObjectStale(string s3Bucket, string s3Key, long lastUpdatedTimestamp)
        {
            var s3Client = new AmazonS3Client();
            var staleObject = true;

            try
            {
                var currentObject = s3Client.GetObjectMetadataAsync(s3Bucket, s3Key).Result;
                var currentObjectTimestamp = Convert.ToInt64(currentObject.Metadata[ChangeDetectionMetadataKey]);

                staleObject = currentObjectTimestamp < lastUpdatedTimestamp;
            }
            catch (Exception) { /*metadata might not exist because this is a new file*/ }

            return staleObject;
        }

        public async Task PutObject(string s3Bucket, string s3Key, byte[] data, string contentType, long lastUpdatedTimestamp)
        {
            var s3Client = new AmazonS3Client();

            using (var stream = new MemoryStream(data))
            {
                var putQuipHtmlRequest = new PutObjectRequest()
                {
                    BucketName = s3Bucket,
                    Key = s3Key,
                    InputStream = stream,
                    ContentType = contentType
                };
                putQuipHtmlRequest.Metadata.Add(ChangeDetectionMetadataKey, lastUpdatedTimestamp.ToString());

                var putObjectResponse = await s3Client.PutObjectAsync(putQuipHtmlRequest);

                if (putObjectResponse.HttpStatusCode != System.Net.HttpStatusCode.OK)
                {
                    LambdaLogger.Log($"S3 Client PutObjectAsync Error: {JsonSerializer.Serialize(putObjectResponse)}");
                }
            }
        }
    }
}
