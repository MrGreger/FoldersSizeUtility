using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

namespace FoldersSizeUtility
{
    class Program
    {
        static void Main(string[] args)
        {
            args = new string[] { "C:\\" };

            if (args.Length == 0)
            {
                Console.WriteLine("pass initial folder as arg or pass '.' to scan in current folder.");
                return;
            }

            var path = args[0];

            if (args[0].Trim() == ".")
            {
                path = Directory.GetCurrentDirectory();
            }

            var sw = new Stopwatch();
            sw.Start();

            var scanner = new FolderScanner();
            scanner.ProgressChanged += HandleFolderScanCompleted;

            var result = scanner.GetSizeAsync(path).GetAwaiter().GetResult();

            foreach (var item in result.OrderByDescending(x => x.FolderSize).ToList())
            {
                Console.WriteLine($"{item.FolderName} -- {SizeSuffixHelper.SizeSuffix(item.FolderSize)} -- {item.GetNestedFoldersCount()} nested dirs ");
                foreach (var ext in item.GetFileExtensionsCount())
                {
                    Console.WriteLine($"{ext.Key} -- {ext.Value.Count()} -- {SizeSuffixHelper.SizeSuffix(ext.Value.Select(x => x.FolderFileSize).Sum())}");
                }
                Console.WriteLine(new string('-', 10));
            }

            sw.Stop();

            Console.WriteLine();
            Console.WriteLine($"Time elapsed: {sw.ElapsedMilliseconds / 1000}s");
            Console.WriteLine();

            Console.ReadLine();
        }

        private static object sync = new object();

        private static void HandleFolderScanCompleted(int totalFolders, int completedFolders)
        {
            lock (sync)
            {

                Console.CursorVisible = false;
                var cursorTop = Console.CursorTop;
                for (int i = Console.BufferWidth - 1; i >= 0; i--)
                {
                    Console.SetCursorPosition(i, cursorTop);
                    Console.Write(" ");
                }

                Console.Write($"{((float)completedFolders / totalFolders) * 100}%");
                Console.CursorVisible = true;
            }
        }
    }
}
