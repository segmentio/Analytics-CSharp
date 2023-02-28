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

        public long Length => this._file?.Length ?? 0;

        public bool IsOpened => this._file != null;

        public void OpenOrCreate(string file, out bool newFile)
        {
            if (this._file != null && !this._file.Name.Equals(file))
            {
                // the given file is different than the current one
                // close the current one first
                this.Close();
            }

            newFile = false;
            if (this._file == null)
            {
                newFile = !this._directory.ContainsKey(file);
                this._file = newFile ? new InMemoryFile(file) : this._directory[file];
            }

            this._directory[file] = this._file;
        }

        public Task Write(string content)
        {
            this._file?.Write(content);
            return Task.CompletedTask;
        }

        public IEnumerable<string> Read() => this._directory.Keys;

        public void Remove(string file) => this._directory.Remove(file);

        public void Close() => this._file = null;


        /// <summary>
        /// This method closes and adds an extension to the opening file.
        /// The file will no longer be available to modified once this method is called.
        /// </summary>
        /// <param name="extension">extension without dot</param>
        public void FinishAndClose(string extension = default)
        {
            if (this._file == null)
            {
                return;
            }

            if (extension != null)
            {
                var nameWithExtension = this._file.Name + '.' + extension;
                _ = this._directory.Remove(this._file.Name);
                this._directory[nameWithExtension] = this._file;
            }

            this._file = null;
        }

        public byte[] ReadAsBytes(string source) => this._directory.ContainsKey(source) ? this._directory[source].ToBytes() : null;

        public class InMemoryFile
        {
            public StringBuilder FileStream { get; }

            public string Name { get; }

            public int Length => this.FileStream.Length;

            public InMemoryFile(string name)
            {
                this.Name = name;
                this.FileStream = new StringBuilder();
            }

            public void Write(string content) => this.FileStream.Append(content);

            public byte[] ToBytes() => this.FileStream.ToString().GetBytes();
        }
    }

    public class FileEventStream : IEventStream
    {

        private readonly DirectoryInfo _directory;

        private FileStream _fs;

        private FileInfo _file;

        public long Length => this._file?.Length ?? 0;

        public bool IsOpened => this._file != null && this._fs != null;

        public FileEventStream(string directory) => this._directory = Directory.CreateDirectory(directory);

        /// <summary>
        /// This method appends a `.tmp` extension to the give file, which
        /// means we only operate on an unfinished file. Once a file is
        /// finished, it is no longer available to modified.
        /// </summary>
        /// <param name="file">filename</param>
        /// <param name="newFile">result that indicate whether the file is a new file</param>
        public void OpenOrCreate(string file, out bool newFile)
        {
            if (this._file != null && !this._file.FullName.EndsWith(file))
            {
                // the given file is different than the current one
                // close the current one first
                this.Close();
            }

            if (this._file == null)
            {
                this._file = new FileInfo(this._directory.FullName + Path.DirectorySeparatorChar + file);
            }
            newFile = !this._file.Exists;

            if (this._fs == null)
            {
                this._fs = this._file.Open(FileMode.OpenOrCreate);
                _ = this._fs.Seek(0, SeekOrigin.End);
            }
            this._file.Refresh();
        }

        public async Task Write(string content)
        {
            if (this._fs == null || this._file == null)
            {
                return;
            }

            await this._fs.WriteAsync(content.GetBytes(), 0, content.Length);
            await this._fs.FlushAsync();
            this._file.Refresh();
        }

        public IEnumerable<string> Read() => this._directory.GetFiles().Select(f => f.FullName);

        public void Remove(string file) => File.Delete(file);

        public void Close()
        {
            this._fs?.Close();
            this._fs = null;
            this._file = null;
        }

        /// <summary>
        /// This method closes and adds an extension to the opening file.
        /// The file will no longer be available to modified once this method is called.
        /// </summary>
        /// <param name="extension">extension without dot</param>
        public void FinishAndClose(string extension = default)
        {
            this._fs?.Close();
            this._fs = null;


            if (this._file != null && extension != null)
            {
                var nameWithExtension = this._file.FullName + '.' + extension;
                this._file.MoveTo(nameWithExtension);
            }

            this._file = null;
        }

        public byte[] ReadAsBytes(string source)
        {
            var file = new FileInfo(source);
            return file.Exists ? File.ReadAllBytes(source) : null;
        }
    }
}
