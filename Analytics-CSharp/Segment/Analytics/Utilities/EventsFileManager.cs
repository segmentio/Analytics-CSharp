using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Segment.Analytics.Utilities
{
    /// <summary>
    /// Responsible for storing events in a batch payload style.
    ///
    /// Contents format
    /// <code>
    /// {
    ///     "batch": [
    ///     ...
    ///     ],
    ///     "sentAt": "2021-04-30T22:06:11"
    /// }
    /// </code>
    /// 
    /// Each file stored is a batch of events. When uploading events the contents of the file can be
    /// sent as-is to the Segment batch upload endpoint.
    ///
    /// Some terms:
    /// <list type="bullet">
    ///     <item><description>Current open file: the most recent temporary batch file that is being used to store events</description></item>
    ///     <item><description>Closing the file: ending the batch payload, and renaming the temporary file to a permanent one</description></item>
    ///     <item><description>Stored file paths: list of file paths that are not temporary and match the write-key of the manager</description></item>
    /// </list>
    /// 
    /// How it works:
    /// storeEvent() will store the event in the current open file, ensuring that batch size
    /// does not go over the 475KB limit. It will close the current file and create new temp ones
    /// when appropriate
    ///
    /// When read() is called the current file is closed, and a list of stored file paths is returned
    ///
    /// remove() will delete the file path specified
    /// </summary>
    internal class EventsFileManager
    {
        private readonly DirectoryInfo _directory;

        private readonly string _writeKey;

        private readonly UserPrefs _userPrefs;

        private readonly string _fileIndexKey;

        private FileStream _fs;

        private FileInfo _curFile;

        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1);

        private const long MaxFileSize = 475_000;

        public EventsFileManager(string directory, string writeKey, UserPrefs userPrefs)
        {
            _directory = Directory.CreateDirectory(directory);
            _writeKey = writeKey;
            _userPrefs = userPrefs;
            _fileIndexKey = "segment.events.file.index." + writeKey;
            _fs = null;
        }

        /// <summary>
        /// closes existing file, if at capacity
        /// opens a new file, if current file is full or uncreated
        /// stores the event
        /// </summary>
        /// <param name="event">event to store</param>
        public async Task StoreEvent(string @event) => await WithLock(async () =>
            {
                var newFile = false;
                var file = CurrentFile();
                if (!file.Exists)
                {
                    await Start(file);
                    newFile = true;
                }

                // check if file is at capacity
                if (file.Length > MaxFileSize)
                {
                    await Finish();

                    // get the next file
                    file = CurrentFile();
                    await Start(file);
                    newFile = true;
                }

                var contents = "";
                if (!newFile)
                {
                    contents += ",";
                }

                contents += @event;
                await WriteToFile(contents.GetBytes(), file);
            });

        /// <summary>
        /// Returns a comma-separated list of file paths that are not yet uploaded
        /// </summary>
        /// <returns></returns>
        public List<string> Read()
        {
            return _directory.GetFiles()
                        .Where(f => !f.Name.EndsWith(".tmp"))
                        .Select(f => f.FullName)
                        .ToList();
        }

        /// <summary>
        /// deletes the file at filePath
        /// </summary>
        /// <param name="filePath">path to the file to delete</param>
        /// <returns>whether the operation succeeds</returns>
        public bool Remove(string filePath)
        {
            try
            {
                File.Delete(filePath);
                return true;
            }
            catch (Exception e)
            {
                Analytics.logger?.LogError(e, "Failed to remove file path.");
                return false;
            }
        }

        /// <summary>
        /// closes current file, and increase the index
        /// so next write go to a new file
        /// </summary>
        public async Task Rollover() => await WithLock(async () =>
            {
                await Finish();
            });

        private async Task Start(FileInfo file)
        {
            const string contents = "{\"batch\":[";
            await WriteToFile(contents.GetBytes(), file);
        }

        private async Task Finish()
        {
            var file = CurrentFile();
            if (!file.Exists) return;

            var contents = "],\"sentAt\":\"" + DateTime.UtcNow.ToString("o") + "\",\"writeKey\":\"" + _writeKey + "\"}";
            await WriteToFile(contents.GetBytes(), file);
            _fs.Close();

            var nameWithoutExtension = file.FullName.Remove(file.FullName.Length - file.Extension.Length);
            file.MoveTo(nameWithoutExtension);
            
            IncrementFileIndex();
            Reset();
        }

        private FileInfo CurrentFile()
        {
            // if no file is opened, open a file according to _fileIndexKey
            if (_curFile == null)
            {
                _curFile = new FileInfo(
                        _directory.FullName + Path.DirectorySeparatorChar +
                                _writeKey + "-" + _userPrefs.GetInt(_fileIndexKey, 0) +
                                ".tmp"
                            );
            }
            
            return _curFile;
        }

        private async Task WriteToFile(byte[] content, FileInfo file)
        {
            // if no fire stream is open, open the file stream of the given file in open or create mode
            if (_fs == null)
            {
                _fs = file.Open(FileMode.OpenOrCreate);
            }

            await _fs.WriteAsync(content, 0, content.Length);
            await _fs.FlushAsync();
            file.Refresh();
        }

        private void Reset()
        {
            _fs = null;
            _curFile = null;
        }

        private bool IncrementFileIndex()
        {
            var index = _userPrefs.GetInt(_fileIndexKey, 0) + 1;
            try
            {
                var editor = _userPrefs.Edit();
                editor.PutInt(_fileIndexKey, index);
                editor.Apply();
                return true;
            }
            catch (Exception e)
            {
                Analytics.logger?.LogError(e, "Error editing preference file.");
                return false;
            }
        }

        private async Task WithLock(Func<Task> block)
        {
            await _semaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                await block();
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
    
    #region String Extension Methods

    public static partial class ExtensionMethods
    {
        public static byte[] GetBytes(this string str)
        {
            return Encoding.UTF8.GetBytes(str);
        }
    }
    
    #endregion
}