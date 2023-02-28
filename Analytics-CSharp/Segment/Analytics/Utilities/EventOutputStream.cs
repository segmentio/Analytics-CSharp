namespace Segment.Analytics.Utilities
{
    using global::System.Collections.Concurrent;
    using global::System.Collections.Generic;
    using global::System.IO;
    using global::System.Linq;
    using global::System.Text;
    using global::System.Threading.Tasks;

    public interface IEventStream
    {
        long Length { get; }

        bool IsOpened { get; }

        void OpenOrCreate(string file, out bool newFile);

        Task Write(string content);

        IEnumerable<string> Read();

        void Remove(string file);

        void Close();

        void FinishAndClose(string extension = default);

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
                var nameWithExtension = _file.Name + '.' + extension;
                _ = _directory.Remove(_file.Name);
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
                _fs = _file.Open(FileMode.OpenOrCreate);
                _ = _fs.Seek(0, SeekOrigin.End);
            }
            _file.Refresh();
        }

        public async Task Write(string content)
        {
            if (_fs == null || _file == null)
            {
                return;
            }

            await _fs.WriteAsync(content.GetBytes(), 0, content.Length);
            await _fs.FlushAsync();
            _file.Refresh();
        }

        public IEnumerable<string> Read() => _directory.GetFiles().Select(f => f.FullName);

        public void Remove(string file) => File.Delete(file);

        public void Close()
        {
            _fs?.Close();
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
            _fs?.Close();
            _fs = null;


            if (_file != null && extension != null)
            {
                var nameWithExtension = _file.FullName + '.' + extension;
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
