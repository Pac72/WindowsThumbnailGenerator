using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Thumbnail_Generator_Library
{
    public class ProcessHandler
    {
        private static readonly HashSet<string> supportedFilesSet = new HashSet<string>() {
            ".mp4", ".mov", ".wmv", ".avi", ".mkv",
            ".mpeg", ".mpg", ".flv", ".webm", ".wmv", ".asf", ".m4v",
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".svg"
        };

        private static volatile int progressCount;
        private static volatile float progressPercentage;

        public static async Task<long> GenerateThumbnailsForFolder(
            IProgress<InitProgressInfo> initializationProgress,
            IProgress<float> generationProgress,
            string rootFolder,
            int maxThumbCount,
            int maxThreads,
            bool recurse,
            bool clearCache,
            bool skipExisting,
            bool shortCover,
            bool stacked,
            CancellationToken cancellationToken
        )
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            try
            {
                progressCount = 0;
                progressPercentage = 0;

                DirNode rootNode;
                SortedDictionary<int, List<DirNode>> dict;

                if (recurse)
                {
                    rootNode = PathHandler.GetDirTree(rootFolder, "*", initializationProgress, cancellationToken);
                    dict = rootNode.DepthFirstNodes(cancellationToken);
                } else
                {
                    rootNode = new DirNode(rootFolder, 0);
                    List<DirNode> list = new List<DirNode>() { rootNode };
                    dict = new SortedDictionary<int, List<DirNode>>();
                    dict[rootNode.Level] = list;
                }

                int treeSize = rootNode.Size();

                foreach (KeyValuePair<int, List<DirNode>> entry in dict.Reverse())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    List<DirNode> dirNodes = entry.Value;

                    await Task.Run(() =>
                    {
                        _ = Parallel.ForEach(
                        dirNodes,
                        new ParallelOptions { MaxDegreeOfParallelism = maxThreads },
                        node =>
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                return;
                            }
                            progressCount++;
                            progressPercentage = (float)progressCount / treeSize * 100;
                            generationProgress.Report(progressPercentage);
                            string directory = node.Path;

                            string iconLocation = Path.Combine(directory, "thumb.ico");
                            string iniLocation = Path.Combine(directory, "desktop.ini");

                            if (skipExisting && File.Exists(iniLocation))
                            {
                                return;
                            }

                            FSHandler.UnsetSystem(iconLocation);

                            List<string> paths = new();

                            IEnumerable<string> dirEnum = Directory
                            .EnumerateDirectories(directory, "*.*")
                            .Where(path => DirectoryContainsFilesOrFolders(path))
                            .Take(maxThumbCount)
                            ;

                            paths.AddRange(dirEnum);

                            if (paths.Count < maxThumbCount)
                            {
                                IEnumerable<string> fileEnum = Directory
                                .EnumerateFiles(directory, "*.*")
                                .Where(path => supportedFilesSet.Contains(Path.GetExtension(path).ToLower()))
                                .Take(maxThumbCount - paths.Count)
                                ;
                                paths.AddRange(fileEnum);
                            }

                            string[] pathsArray = paths.ToArray();

                            if (pathsArray.Length <= 0)
                            {
                                //Debug.WriteLine(directory + ": no supported files");
                                return;
                            }
                            Debug.WriteLine(directory + ": " + string.Join(", ", pathsArray));

                            if (stacked)
                            {
                                ImageHandler.GenerateThumbnail(pathsArray, iconLocation, shortCover);
                            }
                            else
                            {
                                ImageHandler.GenerateThumbnail4(pathsArray, iconLocation);
                            }

                            FSHandler.SetSystem(iconLocation);
                            FSHandler.ApplyFolderIcon(directory, @".\thumb.ico");
                        });
                    }, cancellationToken);
                }

                if (clearCache) {
                    await Task.Run(() => {
                        FSHandler.ClearCache();
                    }, cancellationToken);
                }

            }
            finally
            {
                stopwatch.Stop();
            }

            return stopwatch.ElapsedMilliseconds;
        }

        public static bool DirectoryContainsFilesOrFolders(string path)
        {
            try {
                return Directory.EnumerateFileSystemEntries(path).Any();
            } catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) {
                return false;
            }
        }
    }
}
