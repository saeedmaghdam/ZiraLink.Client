using ZiraLink.Client.Models;

namespace ZiraLink.Client.Framework.Services
{
    public interface ISharePortSocketService
    {
        void Initialize(string username, List<AppProjectDto> appProjects, CancellationToken cancellationToken);
    }
}
