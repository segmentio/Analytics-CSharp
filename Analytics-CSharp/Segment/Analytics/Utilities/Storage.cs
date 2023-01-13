using System;
using System.IO;
using System.Threading.Tasks;
using Segment.Concurrent;
using Segment.Serialization;
using Segment.Sovran;

namespace Segment.Analytics.Utilities
{
    
    #region Storage Constants

    public readonly struct Constants
    {
        public string value { get; }

        private Constants(string value)
        {
            this.value = value;
        }

        public override string ToString()
        {
            return value;
        }

        public static implicit operator string(Constants constant) => constant.value;

        // backing fields that holds the actual string representation
        // needed for switch statement, has to be compile time available
        public const string _UserId = "segment.userId";
        public const string _Traits = "segment.traits";
        public const string _AnonymousId = "segment.anonymousId";
        public const string _Settings = "segment.settings";
        public const string _Events = "segment.events";
            
        // enum alternatives
        public static readonly Constants UserId = new Constants(_UserId);
        public static readonly Constants Traits = new Constants(_Traits);
        public static readonly Constants AnonymousId = new Constants(_AnonymousId);
        public static readonly Constants Settings = new Constants(_Settings);
        public static readonly Constants Events = new Constants(_Events);
    }

    #endregion
    
    public interface IStorage
    {
        Task SubscribeToStore();

        Task Write(Constants key, string value);

        void WritePrefs(Constants key, string value);

        Task Rollover();

        string Read(Constants key);

        bool Remove(Constants key);

        bool RemoveFile(string filePath);

        byte[] ReadAsBytes(string source);
    }

    public interface IStorageProvider
    {
        IStorage CreateStorage(params object[] parameters);
    }

    public class DefaultStorageProvider : IStorageProvider
    {
        public IStorage CreateStorage(params object[] parameters)
        {
            if (!(parameters.Length == 1 && parameters[0] is Analytics))
            {
                throw new ArgumentException(
                    "Invalid parameters for DefaultStorageProvider. DefaultStorageProvider only accepts 1 parameter and it has to be an instance of Analytics");
            }

            var analytics = (Analytics)parameters[0];
            var config = analytics.configuration;
            return new Storage(analytics.store, config.writeKey, config.persistentDataPath, analytics.fileIODispatcher, config.exceptionHandler);   
        }
    }
    
    public class InMemoryStorageProvider : IStorageProvider
    {
        public IStorage CreateStorage(params object[] parameters)
        {
            if (!(parameters.Length == 1 && parameters[0] is Analytics))
            {
                throw new ArgumentException(
                    "Invalid parameters for InMemoryStorageProvider. InMemoryStorageProvider only accepts 1 parameter and it has to be an instance of Analytics");
            }

            var analytics = (Analytics)parameters[0];
            return new InMemoryStorage(analytics.configuration.writeKey);   
        }
    }

    internal class Storage : IStorage, ISubscriber
    {
        private readonly Store _store;
        
        private string _writeKey;
        
        private readonly string _storageDirectory;

        private readonly UserPrefs _userPrefs;

        private readonly EventsFileManager _eventsFile;

        private readonly IDispatcher _ioDispatcher;

        public const long MaxPayloadSize = 32_000;

        public const long MaxBatchSize = 475_000;

        public Storage(Store store, string writeKey, string rootDir, IDispatcher ioDispatcher = default, ICoroutineExceptionHandler exceptionHandler = default)
        {
            _store = store;
            _writeKey = writeKey;
            _userPrefs = new UserPrefs(rootDir + Path.DirectorySeparatorChar + 
                                       "segment.prefs" + Path.DirectorySeparatorChar + writeKey, exceptionHandler);
            _storageDirectory = rootDir + Path.DirectorySeparatorChar +
                                    "segment.data" + Path.DirectorySeparatorChar +
                                    writeKey + Path.DirectorySeparatorChar +
                                    "events";
            _eventsFile = new EventsFileManager(_storageDirectory, writeKey, _userPrefs);
            _ioDispatcher = ioDispatcher;
        }

        public async Task SubscribeToStore()
        {
            await _store.Subscribe<UserInfo>(this, UserInfoUpdate, true, _ioDispatcher);
            await _store.Subscribe<System>(this, SystemUpdate, true, _ioDispatcher);
        }
        
        /// <summary>
        /// Write an event or a pref value async
        /// </summary>
        /// <para>
        /// Write a value to storage in an asynchronous way.
        /// If you want to write a non-event type value in a synchronous way,
        /// please use <see cref="WritePrefs"/> instead.
        /// </para>
        /// <param name="key">the type of value being written</param>
        /// <param name="value">the value being written</param>
        /// <exception cref="Exception">exception that captures the failure of writing an event to disk</exception>
        public virtual async Task Write(Constants key, string value)
        {
            switch (key)
            {
                case Constants._Events:
                    if (value.Length < MaxPayloadSize)
                    {
                        await _eventsFile.StoreEvent(value);
                    }
                    else
                    {
                        throw new Exception("enqueued payload is too large");
                    }
                    break;
                default:
                    WritePrefs(key, value);
                    break;
            }
        }

        /// <summary>
        /// Write a pref value synchronously
        /// </summary>
        /// <para>
        /// Write a value to UserPrefs in a synchronous way.
        /// If you want to write an event type value or write pref value asynchronously,
        /// please use <see cref="Write"/> instead.
        /// </para>
        /// <param name="key">the type of value being written</param>
        /// <param name="value">the value being written</param>
        public void WritePrefs(Constants key, string value)
        {
            var editor = _userPrefs.Edit();
            editor.PutString(key, value);
            editor.Apply();
        }
        
        /// <summary>
        /// Direct writes to a new file, and close the current file.
        /// This function is useful in cases such as `flush`, that
        /// we want to finish writing the current file, and have it
        /// flushed to server.
        /// </summary>
        public virtual async Task Rollover()
        {
            await _eventsFile.Rollover();
        }
        
        public virtual string Read(Constants key)
        {
            switch (key)
            {
                case Constants._Events:
                    return string.Join(",", _eventsFile.Read());
                default:
                    return _userPrefs.GetString(key, null);
            }
        }

        public bool Remove(Constants key)
        {
            switch (key)
            {
                case Constants._Events:
                    return true;
                default:
                    var editor = _userPrefs.Edit();
                    editor.Remove(key);
                    editor.Apply();
                    return true;
            }
        }

        public virtual bool RemoveFile(string filePath)
        {
            return _eventsFile.Remove(filePath);
        }

        public byte[] ReadAsBytes(string source)
        {   
            var file = new FileInfo(source);
            return file.Exists ? File.ReadAllBytes(source) : null;
        }

        #region State Subscriptions

        public void UserInfoUpdate(IState state)
        {   
            var userInfo = (UserInfo) state;
            WritePrefs(Constants.AnonymousId, userInfo.anonymousId);
            
            if (userInfo.userId != null)
            {
                WritePrefs(Constants.UserId, userInfo.userId);
            }

            if (userInfo.traits != null)
            {
                WritePrefs(Constants.Traits, JsonUtility.ToJson(userInfo.traits));
            }
        }

        public void SystemUpdate(IState state)
        {
            var system = (System) state;
            WritePrefs(Constants.Settings, JsonUtility.ToJson(system.settings));
        }

        #endregion
    }
    
    internal class InMemoryStorage : IStorage
    {
        public InMemoryStorage(string writeKey)
        {
        }

        public Task SubscribeToStore()
        {
            throw new NotImplementedException();
        }

        public Task Write(Constants key, string value)
        {
            throw new NotImplementedException();
        }

        public void WritePrefs(Constants key, string value)
        {
            throw new NotImplementedException();
        }

        public Task Rollover()
        {
            throw new NotImplementedException();
        }

        public string Read(Constants key)
        {
            throw new NotImplementedException();
        }

        public bool Remove(Constants key)
        {
            throw new NotImplementedException();
        }

        public bool RemoveFile(string filePath)
        {
            throw new NotImplementedException();
        }

        public byte[] ReadAsBytes(string source)
        {
            throw new NotImplementedException();
        }
    }
}