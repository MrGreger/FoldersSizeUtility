namespace FoldersSizeUtility
{
    public class FolderFileInfo
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
