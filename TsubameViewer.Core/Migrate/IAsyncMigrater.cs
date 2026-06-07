using System;
using System.Threading.Tasks;
#nullable enable
namespace TsubameViewer.Core.Models.Migrate;

public interface IAsyncMigrater
{
    Version? TargetVersion { get; }
    ValueTask MigrateAsync();
}
