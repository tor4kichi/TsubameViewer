using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TsubameViewer.Contracts.Navigation;

public interface IViewLocator
{
    Type ResolveView(string viewName);
}
