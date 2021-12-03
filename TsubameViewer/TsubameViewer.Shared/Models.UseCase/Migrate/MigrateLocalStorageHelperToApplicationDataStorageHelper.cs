using Microsoft.Toolkit.Uwp.Helpers;
using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Models.Infrastructure;
using Windows.ApplicationModel;

namespace TsubameViewer.Models.UseCase.Migrate
{
    internal interface IMigarater
    {
        bool IsRequireMigrate { get; }
        void Migrate();
    }
    internal sealed class MigrateLocalStorageHelperToApplicationDataStorageHelper : IMigarater
    {
        // SystemInformation.Instance.TrackAppUse を仕掛け始めたのが v1.2.5.0 なので
        // ApplicationDataStorageHelperへの移行判定は IsFirstRun で行っている
        bool IMigarater.IsRequireMigrate =>
            SystemInformation.Instance.IsFirstRun
            ;
        void IMigarater.Migrate()
        {
            var strorageHelper = ApplicationDataStorageHelper.GetCurrent(objectSerializer: new JsonObjectSerializer());
            strorageHelper.Clear();
        }
    }
}
