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

namespace FoldersSizes
{
    class Program
    {
        private static object sync = new object();

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

            var result = GetSizeAsync(path).GetAwaiter().GetResult();

            sw.Stop();

            Console.WriteLine();
            Console.WriteLine($"Time elapsed: {sw.ElapsedMilliseconds / 1000}s");
            Console.WriteLine();

            foreach (var item in result.OrderByDescending(x => x.FolderSize).ToList())
            {
                Console.WriteLine($"{item.FolderName} -- {SizeSuffix(item.FolderSize)} -- {item.GetNestedFoldersCount()} nested dirs ");
                foreach (var ext in item.GetFileExtensionsCount())
                {
                    Console.WriteLine($"{ext.Key} -- {ext.Value.Count()} -- {SizeSuffix(ext.Value.Select(x => x.FolderFileSize).Sum())}");
                }
                Console.WriteLine(new string('-', 10));
            }

            Console.ReadLine();
        }


        private static int completedFolders = 0;
        private static void HandleFolderScanCompleted(int totalFolders)
        {
            lock (sync)
            {
                completedFolders++;

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

        private static async Task<IEnumerable<FolderInfo>> GetSizeAsync(string path)
        {
            var directories = new DirectoryInfo(path).GetDirectories();

            var totalFolders = directories.Length;

            var tasks = new List<Task<FolderInfo>>();

            foreach (DirectoryInfo dir in directories)
            {
                var t = Task.Run(async () =>
                {
                    var res = await GetDirectoryInfo(dir, totalFolders);

                    HandleFolderScanCompleted(totalFolders);

                    return res;
                });

                tasks.Add(t);
            }

            return await Task.WhenAll(tasks);
        }

        private static async Task<FolderInfo> GetDirectoryInfo(DirectoryInfo directory, int totalFolders)
        {
            var folderInfo = await ScanDirectory(directory);
            return folderInfo;
        }

        private static async Task<FolderInfo> ScanDirectory(DirectoryInfo directory)
        {
            try
            {
                long dirSize = 0;
                List<FolderInfo> nestedInfos = new List<FolderInfo>();
                List<FolderFileInfo> files = new List<FolderFileInfo>();

                foreach (var file in directory.GetFiles())
                {
                    dirSize += file.Length;
                    files.Add(new FolderFileInfo(file.Name, file.Extension, file.Length));
                }

                foreach (var dir in directory.GetDirectories())
                {
                    var nestedDirInfo = ScanDirectory(dir);

                    dirSize += (await nestedDirInfo).FolderSize;
                    nestedInfos.Add(await nestedDirInfo);
                }

                return new FolderInfo(directory.Name, dirSize, nestedInfos, files);
            }
            catch (UnauthorizedAccessException)
            {
                return new FolderInfo(directory.Name, 0, null, null);
            }
        }

        private static readonly string[] SizeSuffixes =
                   { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };
        private static string SizeSuffix(long value, int decimalPlaces = 1)
        {
            if (decimalPlaces < 0) { throw new ArgumentOutOfRangeException("decimalPlaces"); }
            if (value < 0) { return "-" + SizeSuffix(-value); }
            if (value == 0) { return string.Format("{0:n" + decimalPlaces + "} bytes", 0); }

            // mag is 0 for bytes, 1 for KB, 2, for MB, etc.
            int mag = (int)Math.Log(value, 1024);

            // 1L << (mag * 10) == 2 ^ (10 * mag) 
            // [i.e. the number of bytes in the unit corresponding to mag]
            decimal adjustedSize = (decimal)value / (1L << (mag * 10));

            // make adjustment when the value is large enough that
            // it would round up to 1000 or more
            if (Math.Round(adjustedSize, decimalPlaces) >= 1000)
            {
                mag += 1;
                adjustedSize /= 1024;
            }

            return string.Format("{0:n" + decimalPlaces + "} {1}",
                adjustedSize,
                SizeSuffixes[mag]);
        }

        class FolderInfo
        {
            public string FolderName { get; }
            public long FolderSize { get; }
            public IEnumerable<FolderInfo> NestedFolders { get; }
            public IEnumerable<FolderFileInfo> Files { get; }

            public FolderInfo(string folderName, long size, IEnumerable<FolderInfo> nestedFolderInfos, IEnumerable<FolderFileInfo> files)
            {
                FolderName = folderName;
                FolderSize = size;
                NestedFolders = nestedFolderInfos;
                Files = files;
            }

            public long GetNestedFoldersCount()
            {
                if (NestedFolders == null)
                {
                    return 0;
                }

                long result = NestedFolders.LongCount();

                foreach (var item in NestedFolders)
                {
                    result += item.GetNestedFoldersCount();
                }

                return result;
            }

            public long GetFilesCount()
            {
                if (Files == null)
                {
                    return 0;
                }

                long result = Files.LongCount();

                if (NestedFolders == null)
                {
                    return result;
                }

                foreach (var item in NestedFolders)
                {
                    result += item.GetFilesCount();
                }

                return result;
            }

            public string GetBiggestFileExt()
            {
                return GetFlattenFilesTree(this).OrderBy(x => x.FolderFileSize).FirstOrDefault()?.FolderFileExtension;
            }

            public Dictionary<string, IEnumerable<FolderFileInfo>> GetFileExtensionsCount()
            {
                return new Dictionary<string, IEnumerable<FolderFileInfo>>(GetFlattenFilesTree(this).GroupBy(x => x.FolderFileExtension)
                                                                           .Select(x => new KeyValuePair<string, IEnumerable<FolderFileInfo>>(x.Key, x)));
            }

            public IEnumerable<FolderFileInfo> GetFlattenFilesTree(FolderInfo folder)
            {
                List<FolderFileInfo> result = folder.Files?.ToList() ?? new List<FolderFileInfo>();

                if (folder.NestedFolders == null)
                {
                    return result;
                }

                foreach (var item in folder.NestedFolders)
                {
                    result.AddRange(item.GetFlattenFilesTree(item));
                }

                return result;
            }
        }

        class FolderFileInfo
        {
            public string FolderFileExtension { get; }
            public string FolderFileName { get; }
            public long FolderFileSize { get; }
            public FolderFileInfo(string folderFileName, string folderFileExtension, long folderFileSize)
            {
                FolderFileName = folderFileName;
                FolderFileSize = folderFileSize;
                FolderFileExtension = folderFileExtension;
            }
        }
    }
}
