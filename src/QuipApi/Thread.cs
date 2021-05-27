using System.Collections.Generic;

namespace QuipApi
{
    public class Thread
    {
        public IList<string> expanded_user_ids { get; set; }
        public IList<string> invited_user_emails { get; set; }
        public IList<string> shared_folder_ids { get; set; }
        public IList<string> user_ids { get; set; }
        public string html { get; set; }
        public ThreadMetadata thread { get; set; }
    }
}