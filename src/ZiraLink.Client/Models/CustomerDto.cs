namespace ZiraLink.Client.Models
{
    public class CustomerDto
    {
        public long Id { get; set; }
        public Guid ViewId { get; set; }
        public string ExternalId { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string Name { get; set; }
        public string Family { get; set; }
    }
}
