using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace QuipApi
{
    public class Client
    {
        private readonly HttpClient _httpClient;

        static Client()
        {
            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };
        }

        public Client(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<Thread> GetThread(string threadId)
        {
            var uri = ($"1/threads/{threadId}");

            return await _httpClient.GetFromJsonAsync<Thread>(uri);
        }

        public async Task<Blob> GetThreadBlob(string threadId, string blobId)
        {
            var uri = ($"1/blob/{threadId}/{blobId}");

            using (var blobResponse = await _httpClient.GetAsync(uri))
            {
                return new Blob()
                {
                    MediaType = blobResponse.Content.Headers.ContentType.MediaType,
                    Data = await blobResponse.Content.ReadAsByteArrayAsync()
                };
            }
        }

        public async Task<string> ExportSpreadsheetToJson(Thread document, string spreadsheetIgnoreRegex = null)
        {
            if (document.thread.type != "spreadsheet")
            {
                throw new ArgumentException("Thread is not a spreadsheet type.", nameof(document.thread.type));
            }

            var uri = ($"1/threads/{document.thread.id}/export/xlsx");

            using (var stream = await _httpClient.GetStreamAsync(uri))
            {
                IList<string> ignoreRegexes = null;
                if (!string.IsNullOrWhiteSpace(spreadsheetIgnoreRegex))
                {
                    ignoreRegexes = new List<string>() { spreadsheetIgnoreRegex };
                }

                var exporter = new ExcelToJsonExporter.ExcelToJsonExporter(stream, ignoreRegexes, ignoreRegexes);
                var jsonData = exporter.ExportWorkbookToJson(ExcelToJsonExporter.OutputFormat.ObjectWithSheetNameAsKey);

                var jsonMetadata = new JObject();
                jsonMetadata.Add("title", document.thread.title);
                jsonMetadata.Add("link", document.thread.link);
                jsonMetadata.Add("timestamp", document.thread.updated_usec);

                var json = new JObject();
                json.Add("metadata", jsonMetadata);
                json.Add("data", jsonData);

                return json.ToString();
            }
        }
    }
}
