using global::System;
using global::System.IO;
using global::System.Linq;
using global::System.Text;
using global::System.Threading;
using global::System.Threading.Tasks;
using Segment.Concurrent;
using Segment.Serialization;
using Segment.Sovran;

namespace Segment.Analytics.Utilities
{
    #region Storage Constants

    public readonly struct StorageConstants
    {
        public string Value { get; }

        private StorageConstants(string value) => Value = value;

        public override string ToString() => Value;

        public static implicit operator string(StorageConstants storageConstant) => storageConstant.Value;

        // backing fields that holds the actual string representation
        // needed for switch statement, has to be compile time available
        public const string _UserId = "segment.userId";
        public const string _Traits = "segment.traits";
        public const string _AnonymousId = "segment.anonymousId";
        public const string _Settings = "segment.settings";
        public const string _Events = "segment.events";
        // enum alternatives
        public static readonly StorageConstants UserId = new StorageConstants(_UserId);
        public static readonly StorageConstants Traits = new StorageConstants(_Traits);
        public static readonly StorageConstants AnonymousId = new StorageConstants(_AnonymousId);
        public static readonly StorageConstants Settings = new StorageConstants(_Settings);
        public static readonly StorageConstants Events = new StorageConstants(_Events);
    }

    #endregion

    /// <summary>
    /// The protocol of how events are read and stored.
    /// Implement this interface if you wanna your events
    /// to be read and stored in a the way you want (for
    /// example: from/to remote server, from/to local database).
    /// By default, we have implemented read and store events
    /// from/to memory and file storage.
    /// </summary>
    public interface IStorage
    {
        /// <summary>
        /// Initialization of the storage.
        /// All prerequisite setups should be done in this method.
        /// </summary>
        /// <returns>Awaitable task</returns>
        Task Initialize();

        /// <summary>
        /// Write the key/value pair to storage
        /// </summary>
        /// <param name="key">Key</param>
        /// <param name="value">Value</param>
        /// <returns>Awaitable task</returns>
        Task Write(StorageConstants key, string value);

        /// <summary>
        /// Write the key/value pair to IPreferences
        /// </summary>
        /// <param name="key">Key</param>
        /// <param name="value">Value</param>
        void WritePrefs(StorageConstants key, string value);

        /// <summary>
        /// Close and finish the current batch and start a new batch
        /// </summary>
        /// <returns>Awaitable task</returns>
        Task Rollover();

        /// <summary>
        /// Read the value of a given key
        /// </summary>
        /// <param name="key">Key</param>
        /// <returns>Value</returns>
        string Read(StorageConstants key);

        /// <summary>
        /// Remove the data of a given key
        /// </summary>
        /// <param name="key">Key</param>
        /// <returns>Result of the removal</returns>
        bool Remove(StorageConstants key);

        /// <summary>
        /// Remove a batch of given name
        /// </summary>
        /// <param name="filePath">Name of the batch</param>
        /// <returns>Result of the removal</returns>
        bool RemoveFile(string filePath);

        /// <summary>
        /// Read the given batch as bytes
        /// </summary>
        /// <param name="source">The fullname/identifier of a batch</param>
        /// <returns>Bytes of the content</returns>
        byte[] ReadAsBytes(string source);
    }

    /// <summary>
    /// A provider protocol that creates a concrete storage with the given parameters
    /// </summary>
    public interface IStorageProvider
    {
        IStorage CreateStorage(params object[] parameters);
    }

    public class DefaultStorageProvider : IStorageProvider
    {
        public string PersistentDataPath { get; set; }
        public DefaultStorageProvider(string persistentDataPath = null) => PersistentDataPath = persistentDataPath ?? SystemInfo.GetAppFolder();

        public IStorage CreateStorage(params object[] parameters)
        {
            if (!(parameters.Length == 1 && parameters[0] is Analytics))
            {
                throw new ArgumentException(
                    "Invalid parameters for DefaultStorageProvider. DefaultStorageProvider only accepts 1 parameter and it has to be an instance of Analytics");
            }

            var analytics = (Analytics)parameters[0];
            Configuration config = analytics.Configuration;
            string rootDir = PersistentDataPath;
            string storageDirectory = rootDir + Path.DirectorySeparatorChar +
                                   "segment.data" + Path.DirectorySeparatorChar +
                                   config.WriteKey + Path.DirectorySeparatorChar +
                                   "events";

            var userPrefs = new UserPrefs(rootDir + Path.DirectorySeparatorChar +
                                       "segment.prefs" + Path.DirectorySeparatorChar + config.WriteKey, config.ExceptionHandler);
            var eventStream = new FileEventStream(storageDirectory);
            return new Storage(userPrefs, eventStream, analytics.Store, config.WriteKey, analytics.FileIODispatcher);
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
            var userPrefs = new InMemoryPrefs();
            var eventStream = new InMemoryEventStream();
            return new Storage(userPrefs, eventStream, analytics.Store, analytics.Configuration.WriteKey, analytics.FileIODispatcher);
        }
    }

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
    public class Storage : IStorage, ISubscriber
    {
        private readonly Store _store;

        private readonly string _writeKey;

        internal readonly IPreferences _userPrefs;

        internal readonly IEventStream _eventStream;

        private readonly IDispatcher _ioDispatcher;

        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1);

        internal readonly string _fileIndexKey;

        internal string Begin => "{\"batch\":[";

        internal string End => "],\"sentAt\":\"" + DateTime.UtcNow.ToString("o") + "\",\"writeKey\":\"" + _writeKey + "\"}";

        private string CurrentFile => _writeKey + "-" + _userPrefs.GetInt(_fileIndexKey, 0);

        public const long MaxPayloadSize = 32_000;

        public const long MaxBatchSize = 475_000;

        public const long MaxFileSize = 475_000;

        private const string FileExtension = "json";

        public Storage(IPreferences userPrefs, IEventStream eventStream, Store store, string writeKey, IDispatcher ioDispatcher = default)
        {
            _userPrefs = userPrefs;
            _eventStream = eventStream;
            _store = store;
            _writeKey = writeKey;
            _fileIndexKey = "segment.events.file.index." + writeKey;
            _ioDispatcher = ioDispatcher;
        }

        public async Task Initialize()
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
        public virtual async Task Write(StorageConstants key, string value)
        {
            switch (key)
            {
                case StorageConstants._Events:
                    if (value.Length < MaxPayloadSize)
                    {
                        await StoreEvent(value);
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
        public void WritePrefs(StorageConstants key, string value) => _userPrefs.Put(key, value);

        /// <summary>
        /// Direct writes to a new file, and close the current file.
        /// This function is useful in cases such as `flush`, that
        /// we want to finish writing the current file, and have it
        /// flushed to server.
        /// </summary>
        public virtual async Task Rollover() => await WithLock(PerformRollover);

        public virtual string Read(StorageConstants key)
        {
            switch (key)
            {
                case StorageConstants._Events:
                    return string.Join(",",
                        _eventStream.Read()
                            .Where(f => f.EndsWith(FileExtension)));
                default:
                    return _userPrefs.GetString(key, null);
            }
        }

        public bool Remove(StorageConstants key)
        {
            switch (key)
            {
                case StorageConstants._Events:
                    return true;
                default:
                    _userPrefs.Remove(key);
                    return true;
            }
        }

        public virtual bool RemoveFile(string filePath)
        {
            try
            {
                _eventStream.Remove(filePath);
                return true;
            }
            catch (Exception e)
            {
                Analytics.Logger?.Log(LogLevel.Error, e, "Failed to remove file path.");
                return false;
            }
        }

        public byte[] ReadAsBytes(string source) => _eventStream.ReadAsBytes(source);

        #region State Subscriptions

        public void UserInfoUpdate(IState state)
        {
            var userInfo = (UserInfo)state;
            WritePrefs(StorageConstants.AnonymousId, userInfo._anonymousId);
            if (userInfo._userId != null)
            {
                WritePrefs(StorageConstants.UserId, userInfo._userId);
            }
            else
            {
                Remove(StorageConstants.UserId);
            }

            if (userInfo._traits != null)
            {
                WritePrefs(StorageConstants.Traits, JsonUtility.ToJson(userInfo._traits));
            }
            else
            {
                Remove(StorageConstants.Traits);
            }
        }

        public void SystemUpdate(IState state)
        {
            var system = (System)state;
            WritePrefs(StorageConstants.Settings, JsonUtility.ToJson(system._settings));
        }

        #endregion


        #region File operation

        /// <summary>
        /// closes existing file, if at capacity
        /// opens a new file, if current file is full or uncreated
        /// stores the event
        /// </summary>
        /// <param name="event">event to store</param>
        private async Task StoreEvent(string @event) => await WithLock(async () =>
        {
            _eventStream.OpenOrCreate(CurrentFile, out bool newFile);
            if (newFile)
            {
                await _eventStream.Write(Begin);
            }

            // check if file is at capacity
            if (_eventStream.Length > MaxFileSize)
            {
                await PerformRollover();

                // open the next file
                _eventStream.OpenOrCreate(CurrentFile, out newFile);
                await _eventStream.Write(Begin);
            }

            var contents = new StringBuilder();
            if (!newFile)
            {
                contents.Append(',');
            }

            contents.Append(@event);
            await _eventStream.Write(contents.ToString());
        });


        private async Task PerformRollover()
        {
            if (!_eventStream.IsOpened)
            {
                return;
            }

            await _eventStream.Write(End);
            _eventStream.FinishAndClose(FileExtension);

            IncrementFileIndex();
        }

        private bool IncrementFileIndex()
        {
            int index = _userPrefs.GetInt(_fileIndexKey, 0) + 1;
            try
            {
                _userPrefs.Put(_fileIndexKey, index);
                return true;
            }
            catch (Exception e)
            {
                Analytics.Logger?.Log(LogLevel.Error, e, "Error editing preference file.");
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

        #endregion
    }
}
