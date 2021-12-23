using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace TsubameViewer.Models.UseCase.Maintenance
{
    public interface ILaunchTimeMaintenance
    {
        public void Maintenance();
    }

    public interface ILaunchTimeMaintenanceAsync
    {
        public Task MaintenanceAsync();
    }
}
