﻿using Microsoft.Toolkit.Uwp.Helpers;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Uno.Threading;
using Windows.Storage;

namespace TsubameViewer.Models.Infrastructure
{

    public class JsonObjectSerializer : Microsoft.Toolkit.Helpers.IObjectSerializer
    {
        public string Serialize<T>(T value) => System.Text.Json.JsonSerializer.Serialize(value);

        public T Deserialize<T>(string value) => System.Text.Json.JsonSerializer.Deserialize<T>(value as string);
    }

    public class FlagsRepositoryBase : BindableBase
    {
        private readonly ApplicationDataStorageHelper _LocalStorageHelper;
        FastAsyncLock _fileUpdateLock = new FastAsyncLock();
        public FlagsRepositoryBase()
        {            
            _LocalStorageHelper = ApplicationDataStorageHelper.GetCurrent(objectSerializer: new JsonObjectSerializer());
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

        protected override bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
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
