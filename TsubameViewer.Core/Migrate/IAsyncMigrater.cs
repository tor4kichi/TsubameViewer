using System;
using System.Threading.Tasks;

namespace TsubameViewer.Core.UseCases.Migrate
{
    public interface IAsyncMigrater
    {
        Version? TargetVersion { get; }
        ValueTask MigrateAsync();
    }
}
