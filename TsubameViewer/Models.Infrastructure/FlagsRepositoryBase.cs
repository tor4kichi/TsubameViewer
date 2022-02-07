using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Uwp.Helpers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace TsubameViewer.Models.Infrastructure
{
    /*
    public class JsonObjectSerializer : Microsoft.Toolkit.Helpers.IObjectSerializer
    {
        public string Serialize<T>(T value) => System.Text.Json.JsonSerializer.Serialize(value);

        public T Deserialize<T>(string value) => string.IsNullOrEmpty(value) || value == "null" ? default(T) : System.Text.Json.JsonSerializer.Deserialize<T>(value);
    }
    */

    public class BinaryJsonObjectSerializer : IBytesObjectSerializer
    {
        public byte[] Serialize<T>(T value) => System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(value);

        public T Deserialize<T>(byte[] value) => value == null || value.Length == 0 ? default(T) : System.Text.Json.JsonSerializer.Deserialize<T>(value);
    }

    /// <remarks>
    /// 注意：BinaryJsonObjectSerializer は Nullale[T] をシリアライズできない
    /// </remarks>
    public abstract class FlagsRepositoryBase : ObservableObject
    {
        private readonly static BytesApplicationDataStorageHelper _LocalStorageHelper = BytesApplicationDataStorageHelper.GetCurrent(objectSerializer: new BinaryJsonObjectSerializer());
        private static readonly Models.Infrastructure.AsyncLock _fileUpdateLock = new ();
        public FlagsRepositoryBase()
        {            
        }

        protected T Read<T>(T @default = default, [CallerMemberName] string propertyName = null)
        {
            return _LocalStorageHelper.Read<T>(propertyName, @default);
        }

        protected async Task<T> ReadFileAsync<T>(T value, [CallerMemberName] string propertyName = null)
        {
            using (await _fileUpdateLock.LockAsync(default))
            {
                return await _LocalStorageHelper.ReadFileAsync(propertyName, value);
            }
        }

        protected void Save<T>(T value, [CallerMemberName] string propertyName = null)
        {
            _LocalStorageHelper.Save(propertyName, value);
        }

        protected async Task SaveFileAsync<T>(T value, [CallerMemberName] string propertyName = null)
        {
            using (await _fileUpdateLock.LockAsync(default))
            {
                await _LocalStorageHelper.CreateFileAsync(propertyName, value);
            }
        }

        protected void Save<T>(T? value, [CallerMemberName] string propertyName = null)
            where T : struct
        {
            _LocalStorageHelper.Save(propertyName, value);
        }

        protected new bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (base.SetProperty(ref storage, value, propertyName))
            {
                Save<T>(value, propertyName);
                return true;
            }
            else
            {
                return true;
            }
        }

        protected bool SetProperty<T>(ref T? storage, T? value, [CallerMemberName] string propertyName = null)
            where T : struct
        {
            if (base.SetProperty(ref storage, value, propertyName))
            {
                Save<T>(value, propertyName);
                return true;
            }
            else
            {
                return true;
            }
        }

    }    
}
