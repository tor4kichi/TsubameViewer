using System;

namespace TsubameViewer.Core.UseCases.Migrate
{
    public interface IMigrater 
    {
        Version? TargetVersion { get; }
        void Migrate();
    }
}
