namespace QuipApi
{
    public class ThreadMetadata
	{
		public string id { get; set; }
		public string link { get; set; }
		public string title { get; set; }
		public long created_usec { get; set; }
		public long updated_usec { get; set; }
		public string author_id { get; set; }
		public Sharing sharing { get; set; }
		public string type { get; set; }
		public string thread_class { get; set; }
	}
}