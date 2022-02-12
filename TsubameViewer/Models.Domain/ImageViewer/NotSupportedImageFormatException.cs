using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TsubameViewer.Models.Domain.ImageViewer
{
    internal class NotSupportedImageFormatException : NotSupportedException
    {
        public NotSupportedImageFormatException(string fileType)
        {
            FileType = fileType;
        }

        public NotSupportedImageFormatException(string fileType, string message) : base(message)
        {
            FileType = fileType;
        }

        public NotSupportedImageFormatException(string fileType, string message, Exception innerException) : base(message, innerException)
        {
            FileType = fileType;
        }

        public string FileType { get; }
    }
}
