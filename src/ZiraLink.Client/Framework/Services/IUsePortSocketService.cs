using ZiraLink.Client.Models;

namespace ZiraLink.Client.Framework.Services
{
    public interface IUsePortSocketService
    {
        Task InitializeAsync(string username, List<AppProjectDto> appProjects, CancellationToken cancellationToken);
    }
}
