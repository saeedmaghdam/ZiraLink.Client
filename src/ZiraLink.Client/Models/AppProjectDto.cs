namespace ZiraLink.Client.Models
{
    public class AppProjectDto
    {
        public long Id { get; set; }
        public Guid ViewId { get; set; }
        public Guid? AppProjectViewId { get; set; }
        public long CustomerId { get; set; }
        public string Title { get; set; }
        public AppProjectType AppProjectType { get; set; }
        public int InternalPort { get; set; }
        public ProjectState State { get; set; }

        public DateTime DateCreated { get; set; }
        public DateTime DateUpdated { get; set; }

        public CustomerDto Customer { get; set; }
    }
}
