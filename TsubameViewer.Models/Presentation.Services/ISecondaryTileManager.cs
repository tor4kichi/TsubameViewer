using System.Threading.Tasks;
using Windows.Storage;

namespace TsubameViewer.Presentation.Services
{
    public interface ISecondaryTileManager
    {
        Task<bool> AddSecondaryTile(ISecondaryTileArguments arguments, string displayName, IStorageItem storageItem);
        bool ExistTile(string path);
        Task InitializeAsync();
        Task<bool> RemoveSecondaryTile(string path);
    }

    public interface ISecondaryTileArguments
    {
        string PageName { get; set; }
        string Path { get; set; }
    }
}