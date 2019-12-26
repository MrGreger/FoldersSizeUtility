using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace FoldersSizeUtility
{
    public class FolderScanner
    {
        public event Action<int, int> ProgressChanged;

        private object sync = new object();

        public async Task<IEnumerable<FolderInfo>> GetSizeAsync(string path)
        {
            var directories = new DirectoryInfo(path).GetDirectories();

            var totalFolders = directories.Length;
            var progress = 0;

            var tasks = new List<Task<FolderInfo>>();

            foreach (DirectoryInfo dir in directories)
            {
                var t = Task.Run(async () =>
                {
                    var res = await GetDirectoryInfo(dir, totalFolders);

                    OnProgressChanged(totalFolders, ref progress);

                    return res;
                });

                tasks.Add(t);
            }

            return await Task.WhenAll(tasks);
        }

        public async Task<FolderInfo> GetDirectoryInfo(DirectoryInfo directory, int totalFolders)
        {
            var folderInfo = await ScanDirectory(directory);
            return folderInfo;
        }

        public async Task<FolderInfo> ScanDirectory(DirectoryInfo directory)
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

        private void OnProgressChanged(int totalProgress, ref int curProgress)
        {
            lock (sync)
            {
                curProgress++;
                ProgressChanged?.Invoke(totalProgress, curProgress);
            }
        }
    }
}
