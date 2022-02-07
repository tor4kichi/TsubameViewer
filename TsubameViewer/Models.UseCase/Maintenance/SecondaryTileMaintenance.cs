using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TsubameViewer.Presentation.Services.UWP;

namespace TsubameViewer.Models.UseCase.Maintenance
{
    public sealed class SecondaryTileMaintenance : ILaunchTimeMaintenanceAsync
    {
        private readonly SecondaryTileManager _secondaryTileManager;

        public SecondaryTileMaintenance(SecondaryTileManager secondaryTileManager)
        {
            _secondaryTileManager = secondaryTileManager;
        }

        public Task MaintenanceAsync()
        {
            return _secondaryTileManager.InitializeAsync();
        }
    }
}
