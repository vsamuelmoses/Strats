using System;
using System.IO;

namespace Carvers.Infra.Extensions
{
    public static class StringExtensions
    {
        public static string[] AsCsv(this string line)
            => line.Split(',');
        
        public static string[] AsLines(this string lines)
            => lines.Split(new[] {"\n"}, StringSplitOptions.None);

        public static FileInfo AsFileInfo(this string val) 
            => new FileInfo(val);
    }
}