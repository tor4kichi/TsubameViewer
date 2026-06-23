using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TsubameViewer.Core.Helpers;

public static class HashHelper
{
    public static ulong CalculateFNV1a64(string filePath)
    {
        if (filePath == null) throw new ArgumentNullException(nameof(filePath));

        // パスの大文字小文字を区別しない場合は標準化する
        string normalizedPath = filePath.ToLowerInvariant();

        ulong hash = 14695981039346656037UL; // FNV offset basis
        const ulong prime = 1099511628211UL;   // FNV prime

        foreach (char c in normalizedPath)
        {
            // 文字列（UTF-16）の下位・上位バイトに分けて処理
            hash ^= (byte)(c & 0xFF);
            hash *= prime;
            hash ^= (byte)((c >> 8) & 0xFF);
            hash *= prime;
        }

        return hash;
    }
}
