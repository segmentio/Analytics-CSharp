using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Segment.Analytics.Utilities
{
    public interface IEventStream
    {   
        long Length { get; }

        bool IsOpened { get; }
        
        void OpenOrCreate(string file, out bool newFile);

        Task Write(string content);

        IEnumerable<string> Read();

        void Remove(string file);
        
        void Close();
        
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
            _file.Write(content);
            return Task.CompletedTask;
        }

        public IEnumerable<string> Read() => _directory.Keys;

        public void Remove(string file)
        {
            _directory.Remove(file);
        }

        public void Close()
        {
            var nameWithoutExtension = Path.GetFileNameWithoutExtension(_file.Name);
            _directory.Remove(_file.Name);
            _directory[nameWithoutExtension] = _file;

            _file = null;
        }

        public byte[] ReadAsBytes(string source)
        {
            return _directory.ContainsKey(source) ? _directory[source].ToBytes() : null;
        }
        
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

        public FileEventStream(string directory)
        {   
            _directory = Directory.CreateDirectory(directory);
        }
        
        public void OpenOrCreate(string file, out bool newFile)
        {
            if (_file == null)
            {
                _file = new FileInfo(_directory.FullName + Path.DirectorySeparatorChar + file);
            }

            newFile = !_file.Exists;
            _fs = _file.Open(FileMode.OpenOrCreate);
        }

        public bool IsOpened => _file != null && _file.Exists && _fs != null;

        public async Task Write(string content)
        {   
            await _fs.WriteAsync(content.GetBytes(), 0, content.Length);
            await _fs.FlushAsync();
            _file.Refresh();
        }

        public IEnumerable<string>  Read()
        {
            return _directory.GetFiles().Select(f => f.FullName);
        }

        public void Remove(string file)
        {
            File.Delete(file);
        }

        public void Close()
        {
            _fs.Close();

            var nameWithoutExtension = _file.FullName.Remove(_file.FullName.Length - _file.Extension.Length);
            _file.MoveTo(nameWithoutExtension);
            
            _fs = null;
            _file = null;
        }

        public byte[] ReadAsBytes(string source)
        {   
            var file = new FileInfo(source);
            return file.Exists ? File.ReadAllBytes(source) : null;
        }
    }
}