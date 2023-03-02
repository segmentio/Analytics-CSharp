using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Segment.Analytics.Utilities;
using Xunit;

namespace Tests.Utilities
{
    public class InMemoryEventStreamTest
    {
        private readonly IEventStream _eventStream;

        public InMemoryEventStreamTest()
        {
            _eventStream = new InMemoryEventStream();
        }

        [Fact]
        public void LengthTest()
        {
            string str1 = "abc";
            string str2 = "defgh";
            Assert.Equal(0, _eventStream.Length);

            _eventStream.OpenOrCreate("test", out _);
            _eventStream.Write(str1);

            Assert.Equal(str1.Length, _eventStream.Length);

            _eventStream.Write(str2);
            Assert.Equal(str1.Length + str2.Length, _eventStream.Length);
        }

        [Fact]
        public void IsOpenTest()
        {
            Assert.False(_eventStream.IsOpened);

            _eventStream.OpenOrCreate("test", out _);
            Assert.True(_eventStream.IsOpened);

            _eventStream.Close();
            Assert.False(_eventStream.IsOpened);
        }

        [Fact]
        public void OpenOrCreateTest()
        {
            _eventStream.OpenOrCreate("test", out bool actual);
            Assert.True(actual);

            _eventStream.OpenOrCreate("test", out actual);
            Assert.False(actual);
        }

        [Fact]
        public void WriteAndReadBytesTest()
        {
            string file = "test";
            _eventStream.OpenOrCreate(file, out _);
            string str1 = "abc";
            string str2 = "defgh";

            Assert.Equal(0, _eventStream.Length);

            _eventStream.Write(str1);
            Assert.Equal(str1.GetBytes(), _eventStream.ReadAsBytes(file));
            _eventStream.Write(str2);
            Assert.Equal((str1 + str2).GetBytes(), _eventStream.ReadAsBytes(file));
        }

        [Fact]
        public void ReadTest()
        {
            string[] files = new[] { "test1", "test2.json", "test3" };

            _eventStream.OpenOrCreate("test1", out _);

            // open test2 without finish test1
            _eventStream.OpenOrCreate("test2", out _);
            _eventStream.FinishAndClose("json");

            // open test3 after finish test2
            _eventStream.OpenOrCreate("test3", out _);
            // open test3 again
            _eventStream.OpenOrCreate("test3", out _);

            var actual = new HashSet<string>(_eventStream.Read());
            Assert.Equal(files.Length, actual.Count);
            Assert.Contains(files[0], actual);
            Assert.Contains(files[1], actual);
            Assert.Contains(files[2], actual);
        }

        [Fact]
        public void RemoveTest()
        {
            _eventStream.OpenOrCreate("test", out _);
            _eventStream.FinishAndClose("json");
            _eventStream.Remove("test.json");
            _eventStream.OpenOrCreate("test", out bool newFile);

            Assert.True(newFile);
        }

        [Fact]
        public void CloseTest()
        {
            _eventStream.OpenOrCreate("test", out _);
            Assert.True(_eventStream.IsOpened);

            _eventStream.Close();
            Assert.False(_eventStream.IsOpened);
        }

        [Fact]
        public void FinishAndCloseTest()
        {
            _eventStream.OpenOrCreate("test", out _);
            _eventStream.FinishAndClose("random");

            var files = _eventStream.Read().ToList();
            Assert.Single(files);
            Assert.Equal("test.random", files[0]);
            Assert.False(_eventStream.IsOpened);
        }
    }

    public class FileEventStreamTest : IDisposable
    {
        private readonly IEventStream _eventStream;

        private readonly string dir;

        public FileEventStreamTest()
        {
            dir = Guid.NewGuid().ToString();
            _eventStream = new FileEventStream(dir);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(dir, true);
            }
            catch
            {
                // ignored
            }
        }

        [Fact]
        public async Task LengthTest()
        {
            string str1 = "abc";
            string str2 = "defgh";
            Assert.Equal(0, _eventStream.Length);

            _eventStream.OpenOrCreate("test", out _);
            await _eventStream.Write(str1);

            Assert.Equal(str1.Length, _eventStream.Length);

            await _eventStream.Write(str2);
            Assert.Equal(str1.Length + str2.Length, _eventStream.Length);
        }

        [Fact]
        public void IsOpenTest()
        {
            Assert.False(_eventStream.IsOpened);

            _eventStream.OpenOrCreate("test", out _);
            Assert.True(_eventStream.IsOpened);

            _eventStream.Close();
            Assert.False(_eventStream.IsOpened);
        }

        [Fact]
        public void OpenOrCreateTest()
        {
            _eventStream.OpenOrCreate("test", out bool actual);
            Assert.True(actual);

            _eventStream.OpenOrCreate("test", out actual);
            Assert.False(actual);
        }

        [Fact]
        public async Task WriteAndReadBytesTest()
        {
            string str1 = "abc";
            string str2 = "defgh";

            _eventStream.OpenOrCreate("test", out _);
            Assert.Equal(0, _eventStream.Length);
            var files = _eventStream.Read().ToList();
            Assert.Single(files);
            await _eventStream.Write(str1);
            _eventStream.Close();
            Assert.Equal(str1.GetBytes(), _eventStream.ReadAsBytes(files[0]));

            _eventStream.OpenOrCreate("test", out _);
            Assert.Equal(str1.Length, _eventStream.Length);
            files = _eventStream.Read().ToList();
            Assert.Single(files);
            await _eventStream.Write(str2);
            _eventStream.Close();
            Assert.Equal((str1 + str2).GetBytes(), _eventStream.ReadAsBytes(files[0]));
        }

        [Fact]
        public void ReadTest()
        {
            var files = new[] { "test1", "test2.json", "test3" }.ToList();
            files.Sort();

            _eventStream.OpenOrCreate("test1", out _);

            // open test2 without finish test1
            _eventStream.OpenOrCreate("test2", out _);
            _eventStream.FinishAndClose("json");

            // open test3 after finish test2
            _eventStream.OpenOrCreate("test3", out _);
            // open test3 again
            _eventStream.OpenOrCreate("test3", out _);

            var actual = _eventStream.Read().ToList();
            actual.Sort();

            Assert.Equal(files.Count(), actual.Count);
            Assert.EndsWith(files[0], actual[0]);
            Assert.EndsWith(files[1], actual[1]);
            Assert.EndsWith(files[2], actual[2]);
        }

        [Fact]
        public void RemoveTest()
        {
            _eventStream.OpenOrCreate("test", out _);
            _eventStream.FinishAndClose("json");
            _eventStream.Remove("test.json");
            _eventStream.OpenOrCreate("test", out bool newFile);

            Assert.True(newFile);
        }

        [Fact]
        public void CloseTest()
        {
            _eventStream.OpenOrCreate("test", out _);
            Assert.True(_eventStream.IsOpened);

            _eventStream.Close();
            Assert.False(_eventStream.IsOpened);
        }

        [Fact]
        public void FinishAndCloseTest()
        {
            _eventStream.OpenOrCreate("test", out _);
            _eventStream.FinishAndClose("random");

            var files = _eventStream.Read().ToList();
            Assert.Single(files);
            Assert.EndsWith("test.random", files[0]);
            Assert.False(_eventStream.IsOpened);
        }
    }
}
