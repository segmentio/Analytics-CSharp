using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Moq;
using Segment.Analytics;
using Segment.Analytics.Utilities;
using Segment.Serialization;
using Segment.Sovran;
using Xunit;

namespace Tests.Utilities
{
    public class StorageProviderTest
    {
        private Mock<Analytics> _analytics;

        public StorageProviderTest()
        {
            var config = new Configuration(
                writeKey: "123",
                persistentDataPath: "tests",
                autoAddSegmentDestination: false,
                userSynchronizeDispatcher: true
            );
            _analytics = new Mock<Analytics>(config);
        }

        [Fact]
        public void TestDefaultStorageProvider()
        {   
            var storage = new DefaultStorageProvider().CreateStorage(_analytics.Object);
            var converted = Assert.IsType<Storage>(storage);
            Assert.IsType<FileEventStream>(converted._eventStream);
            Assert.IsType<UserPrefs>(converted._userPrefs);
        }

        [Fact]
        public void TestInMemoryStorageProvider()
        {   
            var storage = new InMemoryStorageProvider().CreateStorage(_analytics.Object);
            var converted = Assert.IsType<Storage>(storage);
            Assert.IsType<InMemoryEventStream>(converted._eventStream);
            Assert.IsType<InMemoryPrefs>(converted._userPrefs);
        }
    }
    
    public class StorageTest
    {
        private Storage _storage;
        
        private IPreferences _prefs;
        
        private Mock<IEventStream> _stream;

        private string _payload;

        public StorageTest()
        {
            _payload = JsonUtility.ToJson(new TrackEvent("clicked", new JsonObject
            {
                ["foo"] = "bar"
            }));
            _prefs = new InMemoryPrefs();
            _stream = new Mock<IEventStream>();

            _storage = new Storage(_prefs, _stream.Object, new Store(true), "test");
        }

        [Fact]
        public async Task WriteNewFileTest()
        {
            var newFile = true;
            _stream.Setup(o => o.OpenOrCreate(It.IsAny<string>(), out newFile));

            await _storage.Write(StorageConstants.Events, _payload);
            _stream.Verify(o => o.Write(_storage.Begin), Times.Exactly(1));
            _stream.Verify(o => o.Write(_payload), Times.Exactly(1));
        }

        [Fact]
        public async Task RolloverToNewFileTest()
        {
            var newFile = new[]{ false, true };
            _stream.Setup(o => o.OpenOrCreate(It.IsAny<string>(), out newFile[0]))
                .Callback(() => _stream.Setup(o => o.OpenOrCreate(It.IsAny<string>(), out newFile[1])));
            _stream.Setup(o => o.Length).Returns(Storage.MaxFileSize + 1);
            _stream.Setup(o => o.IsOpened).Returns(true);

            await _storage.Write(StorageConstants.Events, _payload);
            
            // verify the old file gets rolled over
            _stream.Verify(o => o.FinishAndClose(It.IsAny<string>()), Times.Exactly(1));
            Assert.Equal(1, _prefs.GetInt(_storage._fileIndexKey, 0));
            // verify begin, payload, and end all get written exactly once
            _stream.Verify(o => o.Write(_storage.Begin), Times.Exactly(1));
            _stream.Verify(o => o.Write(_payload), Times.Exactly(1));
            // for end, we don't know the exact date, so can't verify the content
            // thus, we verify another write with any string happened exactly once
            _stream.Verify(o => o.Write(It.IsAny<string>()), Times.Exactly(3));
        }

        [Fact]
        public async Task LargePayloadCauseExceptionTest()
        {
            var letters = "abcdefghijklmnopqrstuvwxyz1234567890";
            var largePayload = new StringBuilder();
            for (var i = 0; i < 1000; i++)
            {
                largePayload.Append(letters);
            }

            await Assert.ThrowsAsync<Exception>(async () =>
                await _storage.Write(StorageConstants.Events, largePayload.ToString())
            );
        }

        [Fact]
        public async Task WritePrefsAsyncTest()
        {
            var expected = "userid";
            Assert.Null(_storage.Read(StorageConstants.UserId));
            await _storage.Write(StorageConstants.UserId, expected);
            Assert.Equal(expected, _storage.Read(StorageConstants.UserId));
        }

        [Fact]
        public void WritePrefsTest()
        {
            var expected = "userid";
            Assert.Null(_storage.Read(StorageConstants.UserId));
            _storage.WritePrefs(StorageConstants.UserId, expected);
            Assert.Equal(expected, _storage.Read(StorageConstants.UserId));
        }

        [Fact]
        public async Task RolloverTest()
        {   
            _stream.Setup(o => o.IsOpened).Returns(true);

            await _storage.Rollover();
            
            _stream.Verify(o => o.Write(It.IsAny<string>()), Times.Exactly(1));
            _stream.Verify(o => o.FinishAndClose(It.IsAny<string>()), Times.Exactly(1));
            Assert.Equal(1, _prefs.GetInt(_storage._fileIndexKey, 0));
        }

        [Fact]
        public void ReadTest()
        {
            var files = new[] {"test1", "test2.json", "test3.tmp", "test4.json"}.ToList();
            _stream.Setup(o => o.Read()).Returns(files);
            _prefs.Put(StorageConstants.UserId, "userId");
            
            var actual = _storage.Read(StorageConstants.Events);
            Assert.Equal(files[1] + ',' + files[3], actual);
            Assert.Equal("userId", _storage.Read(StorageConstants.UserId));
        }

        [Fact]
        public void RemoveTest()
        {
            _prefs.Put(StorageConstants.UserId, "userId");
            _storage.Remove(StorageConstants.UserId);
            
            Assert.True(_storage.Remove(StorageConstants.Events));
            Assert.Null(_storage.Read(StorageConstants.UserId));
        }

        [Fact]
        public void RemoveFileTest()
        {
            _storage.RemoveFile("file");
            _stream.Verify(o => o.Remove("file"), Times.Exactly(1));

            _stream.Setup(o => o.Remove(It.IsAny<string>())).Throws<Exception>();
            Assert.False(_storage.RemoveFile("file"));
        }

        [Fact]
        public void ReadAsBytesTest()
        {
            _storage.ReadAsBytes("file");
            _stream.Verify(o => o.ReadAsBytes(It.IsAny<string>()), Times.Exactly(1));
        }
    }

    public class StorageIntegrationTest : IDisposable
    {
        private Storage _storage;

        private string _payload;

        private string dir;

        private string writeKey;

        private string parent;

        public StorageIntegrationTest()
        {
            _payload = JsonUtility.ToJson(new TrackEvent("clicked", new JsonObject
            {
                ["foo"] = "bar"
            }));
            parent = Guid.NewGuid().ToString();
            dir = parent + Path.DirectorySeparatorChar + "tmp";
            writeKey = "123";
            _storage = new Storage(
                new UserPrefs(parent + Path.DirectorySeparatorChar + writeKey + ".prefs"), 
                new FileEventStream(dir), 
                new Store(true), 
                writeKey);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(parent, true);
            }
            catch
            {
                // ignored
            }
        }

        [Fact]
        public async Task TestStoreEvent()
        {
            await _storage.Write(StorageConstants.Events, _payload);
            await _storage.Rollover();

            var path = dir + Path.DirectorySeparatorChar + writeKey + "-0.json";
            var actual = File.ReadAllText(path);
            var exception = Record.Exception(() =>
            {
                JsonUtility.FromJson<JsonObject>(actual);
            });
            
            Assert.True(File.Exists(path));
            Assert.Contains(_payload, actual);
            Assert.Null(exception);
        }

        [Fact]
        public async Task TestRead()
        {   
            await _storage.Write(StorageConstants.Events, _payload);
            await _storage.Rollover();

            var actual = _storage.Read(StorageConstants.Events).Split(',');
            
            Assert.Single(actual);
            Assert.EndsWith(dir + Path.DirectorySeparatorChar + writeKey + "-0.json", actual[0]);
        }

        [Fact]
        public async Task TestRemove()
        {
            await _storage.Write(StorageConstants.Events, _payload);
            await _storage.Rollover();

            var actual = _storage.Read(StorageConstants.Events).Split(',');

            foreach (var file in actual)
            {
                _storage.RemoveFile(file);
            }

            foreach (var file in actual)
            {
                Assert.False(File.Exists(file));
            }
        }

        [Fact]
        public async Task TestRollover()
        {
            await _storage.Write(StorageConstants.Events, _payload);
            await _storage.Rollover();

            var files = Directory.GetFiles(dir);
            var hasCompletedFile = false;
            var hasTempFile = false;

            foreach (var file in files)
            {
                if (file.EndsWith("-0"))
                {
                    hasTempFile = true;
                }

                if (file.EndsWith(".json"))
                {
                    hasCompletedFile = true;
                }
            }
            
            Assert.Single(files);
            Assert.True(hasCompletedFile);
            Assert.False(hasTempFile);
        }

        [Fact]
        public async Task TestRolloverNoFileCreated()
        {
            // rollover w/o writing content to the file
            // should not create a file at all
            for (var i = 0; i < 5; i++)
            {
                await _storage.Rollover();
            }
            
            var files = Directory.GetFiles(dir);
            Assert.Empty(files);
        }

        [Fact]
        public async Task TestRolloverFileCreated()
        {
            // rollover w/ writing content
            // should create a file every time
            var expected = 5;
            for (var i = 0; i < expected; i++)
            {
                await _storage.Write(StorageConstants.Events, _payload);
                await _storage.Rollover();
            }
            
            var files = Directory.GetFiles(dir);
            Assert.Equal(expected, files.Length);
        }
    }
}