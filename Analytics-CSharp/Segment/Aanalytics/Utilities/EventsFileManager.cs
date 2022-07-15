using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Segment.Analytics.Utilities
{
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

        public List<string> Read()
        {
            return _directory.GetFiles()
                        .Where(f => !f.Name.EndsWith(".tmp"))
                        .Select(f => f.FullName)
                        .ToList();
        }

        public bool Remove(string filePath)
        {
            try
            {
                File.Delete(filePath);
                return true;
            }
            catch (Exception e)
            {
                // TODO: log exception
                return false;
            }
        }

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

            var contents = "],\"sentAt\":\"" + DateTime.UtcNow + "\",\"writeKey\":\"" + _writeKey + "\"}";
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
            _curFile ??= new FileInfo(
                    _directory.FullName + Path.DirectorySeparatorChar +
                            _writeKey + "-" + _userPrefs.GetInt(_fileIndexKey, 0) +
                            ".tmp"
                        );
            
            return _curFile;
        }

        private async Task WriteToFile(byte[] content, FileInfo file)
        {
            // if no fire stream is open, open the file stream of the given file in open or create mode
            _fs ??= file.Open(FileMode.OpenOrCreate);
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
                // TODO: log exception
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