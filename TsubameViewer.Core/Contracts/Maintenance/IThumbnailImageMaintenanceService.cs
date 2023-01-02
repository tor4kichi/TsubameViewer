using System.Threading.Tasks;

namespace TsubameViewer.Core.Contracts.Maintenance;

public interface IThumbnailImageMaintenanceService
{
    long ComputeUsingSize();
    Task DeleteAllThumbnailUnderPathAsync(string path);
    Task DeleteAllThumbnailsAsync();
    Task DeleteThumbnailFromPathAsync(string path);
    Task FolderChangedAsync(string oldPath, string newPath);
}

