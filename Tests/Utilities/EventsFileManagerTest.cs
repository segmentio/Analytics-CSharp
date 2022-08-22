using System;
using System.IO;
using System.Threading.Tasks;
using Moq;
using Segment.Analytics;
using Segment.Analytics.Utilities;
using Segment.Serialization;
using Xunit;

namespace Tests.Utilities
{

    public class EventsFileManagerTest : IDisposable
    {
        private EventsFileManager _manager;

        private string _payload;

        private string dir;

        private string writeKey;

        private string parent;

        public EventsFileManagerTest()
        {
            _payload = JsonUtility.ToJson(new TrackEvent("clicked", new JsonObject
            {
                ["foo"] = "bar"
            }));
            parent = "tmp";
            dir = parent + Path.DirectorySeparatorChar + "tmp";
            writeKey = "123";
            _manager = new EventsFileManager(dir, writeKey, 
                new UserPrefs(parent + Path.DirectorySeparatorChar + writeKey + ".prefs"));
        }

        public void Dispose()
        {
            Directory.Delete(parent, true);
        }

        [Fact]
        public async Task TestStoreEvent()
        {
            await _manager.StoreEvent(_payload);
            await _manager.Rollover();

            var path = dir + Path.DirectorySeparatorChar + writeKey + "-0";
            var actual = File.ReadAllText(path);
            var exception = Record.Exception(() =>
            {
                JsonUtility.FromJson<JsonObject>(actual);
            });
            
            Assert.True(File.Exists(path));
            Assert.True(actual.Contains(_payload));
            Assert.Null(exception);
        }

        [Fact]
        public async Task TestRead()
        {   
            await _manager.StoreEvent(_payload);
            await _manager.Rollover();

            var actual = _manager.Read();
            
            Assert.Equal(1, actual.Count);
            Assert.True(actual[0].EndsWith(dir + Path.DirectorySeparatorChar + writeKey + "-0"));
        }

        [Fact]
        public async Task TestRemove()
        {
            await _manager.StoreEvent(_payload);
            await _manager.Rollover();

            var actual = _manager.Read();

            foreach (var file in actual)
            {
                _manager.Remove(file);
            }

            foreach (var file in actual)
            {
                Assert.False(File.Exists(file));
            }
        }

        [Fact]
        public async Task TestRollover()
        {
            await _manager.StoreEvent(_payload);
            await _manager.Rollover();

            var files = Directory.GetFiles(dir);
            var hasCompletedFile = false;
            var hasTempFile = false;

            foreach (var file in files)
            {
                if (file.EndsWith(".tmp"))
                {
                    hasTempFile = true;
                }

                if (file.EndsWith("-0"))
                {
                    hasCompletedFile = true;
                }
            }
            
            Assert.Equal(1, files.Length);
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
                await _manager.Rollover();
            }
            
            var files = Directory.GetFiles(dir);
            Assert.Equal(0, files.Length);
        }

        [Fact]
        public async Task TestRolloverFileCreated()
        {
            // rollover w/ writing content
            // should create a file every time
            var expected = 5;
            for (var i = 0; i < expected; i++)
            {
                await _manager.StoreEvent(_payload);
                await _manager.Rollover();
            }
            
            var files = Directory.GetFiles(dir);
            Assert.Equal(expected, files.Length);
        }
    }
}