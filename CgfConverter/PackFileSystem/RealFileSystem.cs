using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Extensions;

namespace CgfConverter.PackFileSystem;

public class RealFileSystem : IPackFileSystem
{
    // Object dir
    private readonly string _rootPath;

    public RealFileSystem(string rootPath)
    {
        rootPath = Path.GetFullPath(rootPath);
        if (!Directory.Exists(rootPath))
            throw new FileNotFoundException();

        _rootPath = FileHandlingExtensions.CombineAndNormalizePath(rootPath) + "/";
    }

    public Stream GetStream(string path)
    {
        try
        {
            return new FileStream(
                FileHandlingExtensions.CombineAndNormalizePath(_rootPath, path),
                FileMode.Open,
                FileAccess.Read);
        }
        catch (IOException ioe) when (ioe.HResult == unchecked((int) 0x8007007B)) // Path name is invalid
        {
            throw new FileNotFoundException(path);
        }
    }

    // TODO: Rework this.  
    public bool Exists(string path) =>
        File.Exists(FileHandlingExtensions.CombineAndNormalizePath(_rootPath, path));

    public string[] Glob(string pattern)
    {
        // remainingPattern always contains fully qualified path, but in lowercase.
        var remainingPatterns = new List<string>
        {
            _rootPath + FileHandlingExtensions.CombineAndNormalizePath(pattern)
        };

        var testedPatterns = new HashSet<string>();
        var foundPaths = new HashSet<string>();

        while (remainingPatterns.Any())
        {
            pattern = remainingPatterns[^1];
            remainingPatterns.RemoveAt(remainingPatterns.Count - 1);
            if (testedPatterns.Contains(pattern))
                continue;
            testedPatterns.Add(pattern);

            if (!pattern.StartsWith(_rootPath, StringComparison.InvariantCultureIgnoreCase))
                continue;

            if (File.Exists(pattern))
                foundPaths.Add(pattern[_rootPath.Length..]);

            for (var i = 0; i < pattern.Length;)
            {
                var next = pattern.IndexOfAny(new[] { '\\', '/' }, i + 1);
                if (next == -1)
                    next = pattern.Length;

                int pos;
                if (-1 != (pos = pattern.IndexOf("**", i, next - i, StringComparison.Ordinal)))
                {
                    var searchBase = pattern[..i];
                    var prefix = pattern[(i + 1)..pos];
                    var suffix = pattern[pos..next].TrimStart('*');

                    var remainingPattern = next == pattern.Length ? string.Empty : pattern[(next + 1)..];
                    if (remainingPattern.IndexOfAny(new[] { '\\', '/' }) < 0)
                    {
                        try
                        {
                            remainingPatterns.AddRange(
                                Directory.GetFiles(searchBase, $"{prefix}*{suffix}{remainingPattern}"));
                        }
                        catch (Exception)
                        {
                            // pass
                        }
                    }

                    remainingPattern = $"**{pattern[(pos + 1)..]}";
                    try
                    {
                        remainingPatterns.AddRange(
                            Directory.GetDirectories(searchBase, $"{prefix}*")
                                .Select(x => Path.Combine(searchBase, x, remainingPattern)));
                    }
                    catch (Exception)
                    {
                        // pass
                    } 
                    break;
                }

                var wildcardIndex = pattern.IndexOfAny(new[] { '?', '*' }, i, next - i);

                if (-1 != wildcardIndex)
                {
                    var searchBase = pattern[..i];
                    var remainingPattern = pattern[next..];

                    try {
                        remainingPatterns.AddRange(
                            Directory.GetFileSystemEntries(searchBase, pattern[wildcardIndex..next])
                            .Select(x => Path.Combine(searchBase, x) + remainingPattern));
                    }
                    catch (Exception)
                    {
                        // pass
                    }
                    break;
                }

                i = next;
            }
        }

        return foundPaths.ToArray();
    }

    public byte[] ReadAllBytes(string path)
    {
        try
        {
            return File.ReadAllBytes(Path.Combine(_rootPath, path));
        }
        catch (FileNotFoundException)
        {
            throw new FileNotFoundException(path);
        }
        catch (DirectoryNotFoundException)
        {
            throw new FileNotFoundException(path);
        }
        catch (IOException ioe) when (ioe.HResult == unchecked((int) 0x8007007B)) // Path name is invalid
        {
            throw new FileNotFoundException(path);
        }
    }
}
