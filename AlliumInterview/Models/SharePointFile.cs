namespace AlliumInterview.Models
{
    public class SharePointFile
    {
        public required string Name { get; set; }

        public DateTime TimeCreated { get; set; }

        public DateTime TimeLastModified { get; set; }
    }
}
