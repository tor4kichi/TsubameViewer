using System.Threading.Tasks;

namespace TsubameViewer.Contracts.Services;

public interface IAlbamDialogService
{
    Task<(bool isEdited, string Rename)> EditAlbamAsync(string albamName);
    Task<(bool IsSuccess, string Name)> GetNewAlbamNameAsync();
}