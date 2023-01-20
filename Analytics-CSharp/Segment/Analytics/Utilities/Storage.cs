﻿using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
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
        Task Initialize();

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
            var rootDir = config.persistentDataPath;
            var storageDirectory = rootDir + Path.DirectorySeparatorChar +
                                   "segment.data" + Path.DirectorySeparatorChar +
                                   config.writeKey + Path.DirectorySeparatorChar +
                                   "events";
            
            var userPrefs = new UserPrefs(rootDir + Path.DirectorySeparatorChar + 
                                       "segment.prefs" + Path.DirectorySeparatorChar + config.writeKey, config.exceptionHandler);
            var eventStream = new FileEventStream(storageDirectory);
            return new Storage(userPrefs, eventStream, analytics.store, config.writeKey, analytics.fileIODispatcher);   
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
            return new Storage(userPrefs, eventStream, analytics.store, analytics.configuration.writeKey, analytics.fileIODispatcher); 
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
    internal class Storage : IStorage, ISubscriber
    {
        private readonly Store _store;
        
        private readonly string _writeKey;

        private readonly IPreferences _userPrefs;

        private readonly IEventStream _eventStream;

        private readonly IDispatcher _ioDispatcher;

        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1);

        private readonly string _fileIndexKey;
        
        private string Begin => "{\"batch\":[";
        
        private string End => "],\"sentAt\":\"" + DateTime.UtcNow.ToString("o") + "\",\"writeKey\":\"" + _writeKey + "\"}";
        
        private string CurrentFile => _writeKey + "-" + _userPrefs.GetInt(_fileIndexKey, 0) + ".tmp";

        public const long MaxPayloadSize = 32_000;

        public const long MaxBatchSize = 475_000;
        
        private const long MaxFileSize = 475_000;

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
        public virtual async Task Write(Constants key, string value)
        {
            switch (key)
            {
                case Constants._Events:
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
        public void WritePrefs(Constants key, string value)
        {
            _userPrefs.Put(key, value);
        }
        
        /// <summary>
        /// Direct writes to a new file, and close the current file.
        /// This function is useful in cases such as `flush`, that
        /// we want to finish writing the current file, and have it
        /// flushed to server.
        /// </summary>
        public virtual async Task Rollover() => await WithLock(async () =>
        {
            await PerformRollover();
        });
        
        public virtual string Read(Constants key)
        {
            switch (key)
            {
                case Constants._Events:
                    return string.Join(",", 
                        _eventStream.Read()
                            .Where(f => !f.EndsWith(".tmp")));
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
                Analytics.logger?.LogError(e, "Failed to remove file path.");
                return false;
            }
        }

        public byte[] ReadAsBytes(string source)
        {
            return _eventStream.ReadAsBytes(source);
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

        
        #region File operation

        /// <summary>
        /// closes existing file, if at capacity
        /// opens a new file, if current file is full or uncreated
        /// stores the event
        /// </summary>
        /// <param name="event">event to store</param>
        private async Task StoreEvent(string @event) => await WithLock(async () =>
        {
            _eventStream.OpenOrCreate(CurrentFile, out var newFile);
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
            if (!_eventStream.IsOpened) return;

            await _eventStream.Write(End);
            _eventStream.Close();
            
            IncrementFileIndex();
        }

        private bool IncrementFileIndex()
        {
            var index = _userPrefs.GetInt(_fileIndexKey, 0) + 1;
            try
            {
                _userPrefs.Put(_fileIndexKey, index);
                return true;
            }
            catch (Exception e)
            {
                Analytics.logger?.LogError(e, "Error editing preference file.");
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