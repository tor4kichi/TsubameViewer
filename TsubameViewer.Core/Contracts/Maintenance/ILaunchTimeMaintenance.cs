using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace TsubameViewer.Core.Contracts.Maintenance;

public interface ILaunchTimeMaintenance
{
    public void Maintenance();
}

public interface ILaunchTimeMaintenanceAsync
{
    public Task MaintenanceAsync();
}
