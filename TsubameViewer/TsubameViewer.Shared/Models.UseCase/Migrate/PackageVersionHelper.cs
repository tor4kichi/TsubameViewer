using System;
using System.Collections.Generic;
using System.Text;
using Windows.ApplicationModel;

namespace TsubameViewer.Models.UseCase.Migrate
{
    public static class PackageVersionHelper
    {
        public static bool IsSmallerThen(this PackageVersion left, PackageVersion right)
        {
            if (left.Major < right.Major)
            {
                return true;
            }
            if (left.Major == right.Major
                && left.Minor < right.Minor)
            {
                return true;
            }
            if (left.Major == right.Major
                && left.Minor == right.Minor
                && left.Build <= right.Build)
            {
                return true;
            }

            return false;

        }
    }    
}
