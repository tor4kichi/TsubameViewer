using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TsubameViewer.Core.Contracts.Maintenance;
using TsubameViewer.Core.Contracts.Services;

namespace TsubameViewer.Core.UseCases.Maintenance
{
    /// <summary>
    /// v1.4.0 以前に 外部リンクをアプリにD&Dしたことがある場合、 <br />
    /// StorageItem.Path == string.Empty となるためアプリの挙動が壊れてしまっていた問題に対処する
    /// </summary>
    public sealed class SecondaryTileMaintenance : ILaunchTimeMaintenanceAsync
    {
        private readonly ISecondaryTileManager _secondaryTileManager;

        public SecondaryTileMaintenance(ISecondaryTileManager secondaryTileManager)
        {
            _secondaryTileManager = secondaryTileManager;
        }

        public Task MaintenanceAsync()
        {
            return _secondaryTileManager.InitializeAsync();
        }
    }
}
