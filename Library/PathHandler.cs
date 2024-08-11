using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Thumbnail_Generator_Library
{
    public class DirNode : IComparable<DirNode>
    {
        public List<DirNode> SubDirs { get; }
        public string Path { get; }
        public int Level { get; }


        internal DirNode(string pth, int lvl)
        {
            Path = pth;
            Level = lvl;
            SubDirs = new();
        }

        internal void Add(DirNode node)
        {
            SubDirs.Add(node);
        }

        public int Size()
        {
            int result = 1;
            foreach(DirNode node in SubDirs)
            {
                result += node.Size();
            }
            return result;
        }

        public int CompareTo(DirNode other)
        {
            int deltaLevel = other.Level - Level;
            if (deltaLevel != 0) {
                return deltaLevel;
            }

            return Path.CompareTo(other.Path);
        }

        public SortedDictionary<int, List<DirNode>> DepthFirstNodes(CancellationToken cancellationToken)
        {
            SortedDictionary<int, List<DirNode>> dict = new();
            Visit(ref dict, cancellationToken);
            return dict;
        }

        private void Visit(ref SortedDictionary<int, List<DirNode>> dict, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            List<DirNode> nodes;
            if (!dict.TryGetValue(Level, out nodes))
            {
                nodes = new List<DirNode>();
                dict[Level] = nodes;
            }
            nodes.Add(this);

            foreach (DirNode node in SubDirs)
            {
                node.Visit(ref dict, cancellationToken);
            }
        }

        override public string ToString()
        {
            return Level + ": " + Path;
        }
    }

    public struct InitProgressInfo
    {
        public int MaxLevel { get; internal set; }
        public int DirCount { get; internal set; }
    }

    public class PathHandler
    {
        public static InitProgressInfo ProgressInfo;

        public static IEnumerable<string> GetAllDirectoriesOld(string rootDirectory, string searchPattern)
        {
            Stack<string> searchList = new();
            Stack<string> returnList = new();

            searchList.Push(rootDirectory);
            returnList.Push(rootDirectory);

            while (searchList.Count != 0)
            {
                string searchPath = searchList.Pop();
                try
                {
                    IEnumerable<string> subDirsEnumerable = Directory.EnumerateDirectories(searchPath, searchPattern);
                    List<string> subDir = new(subDirsEnumerable);
                    foreach (string directory in subDir) {
                        searchList.Push(directory);
                        if (IsDirectoryWritable(directory)) returnList.Push(directory);
                    }
                }
                catch { }
            }

            return returnList;
        }

        private static readonly EnumerationOptions RECURSE_OPTIONS = new()
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = true,
            ReturnSpecialDirectories = false
        };

        public static IEnumerable<string> GetAllDirectories(string rootDirectory, string searchPattern)
        {
            DirectoryInfo di = new DirectoryInfo(rootDirectory);

            IEnumerable<string> directories = di.EnumerateDirectories(searchPattern, RECURSE_OPTIONS)
                .Where(di => KeepDirectory(di))
                .Select(di => di.FullName);

            return directories;
        }

        private static readonly EnumerationOptions NO_RECURSE_OPTIONS = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = false,
            ReturnSpecialDirectories = false
        };

        public static DirNode GetDirTree(string rootDirectory, string searchPattern, IProgress<InitProgressInfo> initializationProgress, CancellationToken cancellationToken)
        {
            ProgressInfo.MaxLevel = 0;
            ProgressInfo.DirCount = 0;
            DirNode rootNode = new DirNode(rootDirectory, 0);

            BuildDirTree(ref rootNode, searchPattern, initializationProgress, cancellationToken);

            return rootNode;
        }

        private static void BuildDirTree(ref DirNode parent, string searchPattern, IProgress<InitProgressInfo> initializationProgress, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (parent.Level > ProgressInfo.MaxLevel)
            {
                ProgressInfo.MaxLevel = parent.Level;
            }

            DirectoryInfo di = new DirectoryInfo(parent.Path);

            IEnumerable<string> directories = di.EnumerateDirectories(searchPattern, NO_RECURSE_OPTIONS)
                .Where(di => KeepDirectory(di))
                .Select(di => di.FullName);

            ProgressInfo.DirCount += directories.Count();

            initializationProgress.Report(ProgressInfo);

            foreach(string dir in directories)
            {
                DirNode node = new DirNode(dir, parent.Level + 1);
                parent.Add(node);
                BuildDirTree(ref node, searchPattern, initializationProgress, cancellationToken);
            }
        }

        private static bool KeepDirectory(DirectoryInfo di)
        {
            if (IsReparsed(di))
            {
                return false;
            }

            return true;
        }

        private static bool IsReparsed(DirectoryInfo di)
        {
            return (di.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint;
        }

        public static bool IsDirectoryWritable(string dirPath)
        {
            try
            {
                string randomFileName = Path.Combine(dirPath, Path.GetRandomFileName());
                using (FileStream fs = File.Create(randomFileName, 1, FileOptions.DeleteOnClose)) { }
                return true;
            }
            catch
            {
                 return false;
            }
        }
    }
}
