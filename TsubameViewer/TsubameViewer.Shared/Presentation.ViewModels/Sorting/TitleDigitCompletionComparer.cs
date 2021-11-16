using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TsubameViewer.Models.Domain.ImageViewer;
using TsubameViewer.Presentation.ViewModels.PageNavigation;

namespace TsubameViewer.Presentation.ViewModels.Sorting
{
    public sealed class TitleDigitCompletionComparer : IComparer<string>, IComparer
    {
        public static readonly TitleDigitCompletionComparer Default = new TitleDigitCompletionComparer();
        private TitleDigitCompletionComparer() { }

        public static int ComparePath(string x, string y)
        {
            var xDictPath = Path.GetDirectoryName(x);
            var yDictPath = Path.GetDirectoryName(y);

            if (xDictPath != yDictPath)
            {
                return String.CompareOrdinal(x, y);
            }

            static bool TryGetPageNumber(string name, out int pageNumber)
            {
                int keta = 1;
                int number = 0;
                foreach (var i in name.Reverse().SkipWhile(c => !char.IsDigit(c)).TakeWhile(c => char.IsDigit(c)))
                {
                    number += i * keta;
                    keta *= 10;
                }

                pageNumber = number;
                return number > 0;
            }

            var xName = Path.GetFileNameWithoutExtension(x);
            if (!TryGetPageNumber(xName, out int xPageNumber)) { return String.CompareOrdinal(x, y); }

            var yName = Path.GetFileNameWithoutExtension(y);
            if (!TryGetPageNumber(yName, out int yPageNumber)) { return String.CompareOrdinal(x, y); }

            return xPageNumber - yPageNumber;
        }


        public int Compare(string x, string y)
        {
            return TitleDigitCompletionComparer.ComparePath(x, y);
        }

        public int Compare(object x, object y)
        {
            return TitleDigitCompletionComparer.ComparePath(x as string, y as string);
        }

    }

    public sealed class ImageSourceTitleDigitCompletionComparer : IComparer<IImageSource>
    {
        public static readonly ImageSourceTitleDigitCompletionComparer Default = new ImageSourceTitleDigitCompletionComparer();
        private ImageSourceTitleDigitCompletionComparer() { }
        public int Compare(IImageSource x, IImageSource y)
        {
            return TitleDigitCompletionComparer.ComparePath(x.Path, y.Path);
        }
    }
}
