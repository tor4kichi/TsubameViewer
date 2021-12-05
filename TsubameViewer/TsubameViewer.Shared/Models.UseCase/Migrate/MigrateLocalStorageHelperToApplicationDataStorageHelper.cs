using Microsoft.Toolkit.Uwp.Helpers;
using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Models.Infrastructure;
using Windows.ApplicationModel;

namespace TsubameViewer.Models.UseCase.Migrate
{
    internal interface IMigrater 
    {
        bool IsRequireMigrate { get; }
        void Migrate();
    }
    internal sealed class MigrateLocalStorageHelperToApplicationDataStorageHelper : IMigrater
    {
        // SystemInformation.Instance.TrackAppUse を仕掛け始めたのが v1.2.5.0 なので
        // ApplicationDataStorageHelperへの移行判定は IsFirstRun で行っている
        bool IMigrater.IsRequireMigrate =>
            SystemInformation.Instance.IsFirstRun
            ;
        void IMigrater.Migrate()
        {
            var strorageHelper = ApplicationDataStorageHelper.GetCurrent(objectSerializer: new JsonObjectSerializer());
            strorageHelper.Clear();
        }
    }
}
