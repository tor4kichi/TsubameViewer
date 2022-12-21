using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TsubameViewer.Core.Contracts.Maintenance;
using TsubameViewer.Core.Contracts.Services;

namespace TsubameViewer.Core.UseCases.Maintenance
{
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
