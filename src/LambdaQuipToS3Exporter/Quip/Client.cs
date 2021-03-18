namespace LambdaQuipToS3Exporter.Quip
{
    public class Client
    {
        private readonly string _apiToken;

        public Client(string apiToken)
        {
            _apiToken = apiToken;
        }

        public void GetThread(string threadId)
        {
        }
    }
}
