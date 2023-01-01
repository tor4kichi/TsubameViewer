using System;

namespace TsubameViewer.Core.Contracts.Services;

public interface IFolderLastIntractItemService
{
    string GetLastIntractItemName(Guid albamId);
    string GetLastIntractItemName(string path);
    void Remove(Guid albamId);
    void Remove(string path);
    void RemoveAllUnderPath(string path);
    void SetLastIntractItemName(Guid albamId, string itemPath);
    void SetLastIntractItemName(string path, string itemName);
}