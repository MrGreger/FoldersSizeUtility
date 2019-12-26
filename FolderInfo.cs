using System.Collections.Generic;
using System.Linq;

namespace FoldersSizeUtility
{
    public class FolderInfo
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
}
