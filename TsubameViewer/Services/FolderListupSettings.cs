using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TsubameViewer.Core.Infrastructure;
#nullable enable
namespace TsubameViewer.Services;
public sealed class FolderListupSettings : FlagsRepositoryBase
{
    public bool ShowWithIndexedFolderItemAccess
    {
        get => Read(false);
        set => Save(value);
    }
}
