using ZiraLink.Client.Models;

namespace ZiraLink.Client.Framework.Services
{
    public interface IUsePortSocketService
    {
        void Initialize(string username, List<AppProjectDto> appProjects, CancellationToken cancellationToken);
    }
}
