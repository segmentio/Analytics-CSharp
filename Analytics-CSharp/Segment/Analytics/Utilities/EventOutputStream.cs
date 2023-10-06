using global::System.Collections.Concurrent;
using global::System.Collections.Generic;
using global::System.IO;
using global::System.Linq;
using global::System.Text;
using global::System.Threading.Tasks;

namespace Segment.Analytics.Utilities
{
    /// <summary>
    /// The protocol of how events are read and stored.
    /// Implement this interface if you wanna your events
    /// to be read and stored in a the way you want (for
    /// example: from/to remote server, from/to local database).
    /// By default, we have implemented read and store events
    /// from/to memory and file storage.
    /// </summary>
    public interface IEventStream
    {
        /// <summary>
        /// Length of current stream/batch
        /// </summary>
        long Length { get; }

        /// <summary>
        /// Check if a batch/connection is opened
        /// </summary>
        bool IsOpened { get; }

        /// <summary>
        /// Open the batch with the given name. Creates a new one if not already exists.
        /// </summary>
        /// <param name="file">Name of the batch</param>
        /// <param name="newFile">Outputs the result whether the file is newly created</param>
        void OpenOrCreate(string file, out bool newFile);

        /// <summary>
        /// Append content to the opening batch
        /// </summary>
        /// <param name="content">Content to append</param>
        /// <returns>Awaitable task</returns>
        Task Write(string content);

        /// <summary>
        /// Read the name of existing finished batches.
        /// Unfinished batch should not be returned.
        /// </summary>
        /// <returns>The name of existing batches</returns>
        IEnumerable<string> Read();

        /// <summary>
        /// Remove the batch with the given name
        /// </summary>
        /// <param name="file">name of the batch</param>
        void Remove(string file);

        /// <summary>
        /// Close the current opening batch without finish it,
        /// so that the batch can be opened for future appends.
        /// </summary>
        void Close();

        /// <summary>
        /// Close and finish the current opening batch.
        /// </summary>
        /// <param name="extension">The extension to the batch name, so that we can differentiates finished batches.</param>
        void FinishAndClose(string extension = default);

        /// <summary>
        /// Read the batch with the given name as bytes.
        /// The HTTPClient use this method to upload data.
        /// </summary>
        /// <param name="source">The fullname/identifier of a batch</param>
        /// <returns></returns>
        byte[] ReadAsBytes(string source);
    }

    public class InMemoryEventStream : IEventStream
    {
        private readonly IDictionary<string, InMemoryFile> _directory =
            new ConcurrentDictionary<string, InMemoryFile>();

        private InMemoryFile _file;

        public long Length => _file?.Length ?? 0;

        public bool IsOpened => _file != null;

        public void OpenOrCreate(string file, out bool newFile)
        {
            if (_file != null && !_file.Name.Equals(file))
            {
                // the given file is different than the current one
                // close the current one first
                Close();
            }

            newFile = false;
            if (_file == null)
            {
                newFile = !_directory.ContainsKey(file);
                _file = newFile ? new InMemoryFile(file) : _directory[file];
            }

            _directory[file] = _file;
        }

        public Task Write(string content)
        {
            _file?.Write(content);
            return Task.CompletedTask;
        }

        public IEnumerable<string> Read() => _directory.Keys;

        public void Remove(string file) => _directory.Remove(file);

        public void Close() => _file = null;


        /// <summary>
        /// This method closes and adds an extension to the opening file.
        /// The file will no longer be available to modified once this method is called.
        /// </summary>
        /// <param name="extension">extension without dot</param>
        public void FinishAndClose(string extension = default)
        {
            if (_file == null)
            {
                return;
            }

            if (extension != null)
            {
                string nameWithExtension = _file.Name + '.' + extension;
                _directory.Remove(_file.Name);
                _directory[nameWithExtension] = _file;
            }

            _file = null;
        }

        public byte[] ReadAsBytes(string source) => _directory.ContainsKey(source) ? _directory[source].ToBytes() : null;

        public class InMemoryFile
        {
            public StringBuilder FileStream { get; }

            public string Name { get; }

            public int Length => FileStream.Length;

            public InMemoryFile(string name)
            {
                Name = name;
                FileStream = new StringBuilder();
            }

            public void Write(string content) => FileStream.Append(content);

            public byte[] ToBytes() => FileStream.ToString().GetBytes();
        }
    }

    public class FileEventStream : IEventStream
    {

        private readonly DirectoryInfo _directory;

        private FileStream _fs;

        private FileInfo _file;

        public long Length => _file?.Length ?? 0;

        public bool IsOpened => _file != null && _fs != null;

        public FileEventStream(string directory) => _directory = Directory.CreateDirectory(directory);

        /// <summary>
        /// This method appends a `.tmp` extension to the give file, which
        /// means we only operate on an unfinished file. Once a file is
        /// finished, it is no longer available to modified.
        /// </summary>
        /// <param name="file">filename</param>
        /// <param name="newFile">result that indicate whether the file is a new file</param>
        public void OpenOrCreate(string file, out bool newFile)
        {
            if (_file != null && !_file.FullName.EndsWith(file))
            {
                // the given file is different than the current one
                // close the current one first
                Close();
            }

            if (_file == null)
            {
                _file = new FileInfo(_directory.FullName + Path.DirectorySeparatorChar + file);
            }
            newFile = !_file.Exists;

            if (_fs == null)
            {
                _fs = _file.Open(FileMode.Append);
            }
            _file.Refresh();
        }

        public async Task Write(string content)
        {
            if (_fs == null || _file == null)
            {
                return;
            }

            byte[] bytes = content.GetBytes();
            await _fs.WriteAsync(bytes, 0, bytes.Length);
            await _fs.FlushAsync();
            _file.Refresh();
        }

        public IEnumerable<string> Read() => _directory.GetFiles().Select(f => f.FullName);

        public void Remove(string file) => File.Delete(file);

        public void Close()
        {
            _fs?.Dispose();
            _fs = null;
            _file = null;
        }

        /// <summary>
        /// This method closes and adds an extension to the opening file.
        /// The file will no longer be available to modified once this method is called.
        /// </summary>
        /// <param name="extension">extension without dot</param>
        public void FinishAndClose(string extension = default)
        {
            _fs?.Dispose();
            _fs = null;


            if (_file != null && extension != null)
            {
                string nameWithExtension = _file.FullName + '.' + extension;
                _file.MoveTo(nameWithExtension);
            }

            _file = null;
        }

        public byte[] ReadAsBytes(string source)
        {
            var file = new FileInfo(source);
            return file.Exists ? File.ReadAllBytes(source) : null;
        }
    }
}
