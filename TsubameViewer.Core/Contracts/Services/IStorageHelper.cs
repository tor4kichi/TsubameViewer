// this code is copy from 
// https://github.com/CommunityToolkit/WindowsCommunityToolkit/blob/main/Microsoft.Toolkit.Uwp/Helpers/ObjectStorage/ApplicationDataStorageHelper.cs
// and modified to byte array serialize/deserialize.


using System.Collections.Generic;
using System.Threading.Tasks;

namespace TsubameViewer.Core.Contracts.Services;

public interface IStorageHelper
{
    void Clear();
    Task CreateFileAsync<T>(string filePath, T value);
    Task CreateFolderAsync(string folderPath);
    bool KeyExists(string key);
    bool KeyExists(string compositeKey, string key);
    T? Read<T>(string key, T? @default = default);
    T? Read<T>(string compositeKey, string key, T? @default = default);
    Task<T?> ReadFileAsync<T>(string filePath, T? @default = default);
    Task<IEnumerable<(DirectoryItemType ItemType, string Name)>> ReadFolderAsync(string folderPath);
    void Save<T>(string compositeKey, IDictionary<string, T> values);
    void Save<T>(string key, T value);
    bool TryDelete(string key);
    bool TryDelete(string compositeKey, string key);
    Task<bool> TryDeleteItemAsync(string itemPath);
    bool TryRead<T>(string key, out T? value);
    bool TryRead<T>(string compositeKey, string key, out T? value);
    Task<bool> TryRenameItemAsync(string itemPath, string newName);
}

public enum DirectoryItemType
{
    None,
    File,
    Folder
}