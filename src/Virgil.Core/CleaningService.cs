using System;
using System.IO;
using System.Linq;

namespace Virgil.Core
{
    /// <summary>
    /// Provides methods to scan and clean temporary files on the system. This
    /// implementation focuses on the user's temporary directory but can be
    /// extended to include additional locations.
    /// </summary>
    public class CleaningService
    {
        private readonly string _tempPath;

        public CleaningService()
        {
            _tempPath = Path.GetTempPath();
        }

        /// <summary>
        /// Calculates the total size, in bytes, of all files found within the
        /// configured temporary directory and its subdirectories. If an
        /// exception occurs during enumeration, the method returns zero.
        /// </summary>
        /// <returns>The cumulative size of files in bytes.</returns>
        public long GetTempFilesSize()
        {
            try
            {
                return Directory.EnumerateFiles(_tempPath, "*", SearchOption.AllDirectories)
                    .Sum(file =>
                    {
                        try
                        {
                            return new FileInfo(file).Length;
                        }
                        catch
                        {
                            return 0L;
                        }
                    });
            }
            catch
            {
                return 0L;
            }
        }

        /// <summary>
        /// Attempts to delete all files within the configured temporary
        /// directory. Exceptions during deletion are swallowed to ensure the
        /// process continues for remaining files.
        /// </summary>
        public void CleanTempFiles()
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(_tempPath, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch
                    {
                        // Ignore and continue on individual file deletion failures
                    }
                }
            }
            catch
            {
                // Ignore enumeration errors entirely
            }
        }
    }
}