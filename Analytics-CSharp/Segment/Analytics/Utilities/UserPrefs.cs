using global::System;
using global::System.Collections.Concurrent;
using global::System.Collections.Generic;
using global::System.IO;
using global::System.Threading;
using global::System.Threading.Tasks;
using Segment.Concurrent;
using Segment.Serialization;

namespace Segment.Analytics.Utilities
{
    public interface IPreferences
    {
        int GetInt(string key, int defaultValue = -1);

        float GetFloat(string key, float defaultValue = -1.0f);

        string GetString(string key, string defaultValue = null);

        bool Contains(string key);

        void Put(string key, int value);

        void Put(string key, float value);

        void Put(string key, string value);

        void Remove(string key);
    }

    /// <summary>
    /// InMemoryPrefs does not persists user prefs. This is designed for stateless server.
    /// </summary>
    public class InMemoryPrefs : IPreferences
    {
        private readonly IDictionary<string, object> _cache = new ConcurrentDictionary<string, object>();

        public int GetInt(string key, int defaultValue = -1)
        {
            int ret;

            try
            {
                ret = Convert.ToInt32(_cache[key]);
            }
            catch
            {
                ret = defaultValue;
            }

            return ret;
        }

        public float GetFloat(string key, float defaultValue = -1)
        {
            float ret;

            try
            {
                ret = float.Parse(Convert.ToString(_cache[key]));
                ;
            }
            catch
            {
                ret = defaultValue;
            }

            return ret;
        }

        public string GetString(string key, string defaultValue = null)
        {
            string ret;

            try
            {
                ret = Convert.ToString(_cache[key]);
            }
            catch
            {
                ret = defaultValue;
            }

            return ret;
        }

        public bool Contains(string key) => _cache.ContainsKey(key);

        public void Put(string key, int value) => _cache[key] = value;

        public void Put(string key, float value) => _cache[key] = value;

        public void Put(string key, string value) => _cache[key] = value;

        public void Remove(string key) => _cache.Remove(key);
    }

    /**
     * This UserPrefs is a translation of the Android SharedPreference.
     * Refer to <see cref="https://android.googlesource.com/platform/frameworks/base.git/+/master/core/java/android/app/SharedPreferencesImpl.java"/>
     * for the original implementation.
     */
    public class UserPrefs : IPreferences
    {
        internal Dictionary<string, object> _cache;

        internal readonly object _mutex;

        internal readonly object _diskWriteMutex;

        internal int _ongoingDiskWrites;

        internal long _memoryEpoch;

        internal long _diskEpoch;

        private bool _loaded;

        private readonly FileInfo _file;

        private readonly FileInfo _backupFile;

        private readonly Scope _scope;

        private readonly IDispatcher _dispatcher;

        public UserPrefs(string file, ICoroutineExceptionHandler exceptionHandler = null)
        {
            _cache = new Dictionary<string, object>();
            _mutex = new object();
            _diskWriteMutex = new object();
            _ongoingDiskWrites = 0;
            _memoryEpoch = 0;
            _diskEpoch = 0;
            _loaded = false;
            _file = new FileInfo(file);
            _backupFile = new FileInfo(file + ".bak");

            // uses a new scope for UserPrefs, so interruption does not propagate to analytics scope
            // in addition, file I/O in this class are all blocking. need to have its own threads
            // to prevent blocking analytics threads
            _scope = new Scope(exceptionHandler);
            _dispatcher = new Dispatcher(new LimitedConcurrencyLevelTaskScheduler(Environment.ProcessorCount));
            StartLoadFromDisk();
        }

        public int GetInt(string key, int defaultValue = -1)
        {
            int ret;

            lock (_mutex)
            {
                AwaitLoadedLocked();

                try
                {
                    ret = Convert.ToInt32(_cache[key]);
                }
                catch
                {
                    ret = defaultValue;
                }
            }

            return ret;
        }

        public float GetFloat(string key, float defaultValue = -1.0f)
        {
            float ret;

            lock (_mutex)
            {
                AwaitLoadedLocked();

                try
                {
                    ret = float.Parse(Convert.ToString(_cache[key]));
                }
                catch
                {
                    ret = defaultValue;
                }
            }

            return ret;
        }

        public string GetString(string key, string defaultValue = null)
        {
            string ret;

            lock (_mutex)
            {
                AwaitLoadedLocked();

                try
                {
                    ret = Convert.ToString(_cache[key]);
                }
                catch
                {
                    ret = defaultValue;
                }
            }

            return ret;
        }

        public bool Contains(string key)
        {
            AwaitLoadedLocked();

            lock (_mutex)
            {
                return _cache.ContainsKey(key);
            }
        }

        /// <summary>
        /// Use for one shot key-value update. If you need to update multiple values at once, try <see cref="Edit"/>
        /// to get an Editor and apply the changes all at once for better performance.
        /// </summary>
        /// <param name="key">key</param>
        /// <param name="value">value</param>
        public void Put(string key, int value)
        {
            Editor editor = Edit();
            editor.PutInt(key, value);
            editor.Apply();
        }

        /// <summary>
        /// Use for one shot key-value update. If you need to update multiple values at once, try <see cref="Edit"/>
        /// to get an Editor and apply the changes all at once for better performance.
        /// </summary>
        /// <param name="key">key</param>
        /// <param name="value">value</param>
        public void Put(string key, float value)
        {
            Editor editor = Edit();
            editor.PutFloat(key, value);
            editor.Apply();
        }

        /// <summary>
        /// Use for one shot key-value update. If you need to update multiple values at once, try <see cref="Edit"/>
        /// to get an Editor and apply the changes all at once for better performance.
        /// </summary>
        /// <param name="key">key</param>
        /// <param name="value">value</param>
        public void Put(string key, string value)
        {
            Editor editor = Edit();
            editor.PutString(key, value);
            editor.Apply();
        }

        /// <summary>
        /// Use for one shot key-value update. If you need to update multiple values at once, try <see cref="Edit"/>
        /// to get an Editor and apply the changes all at once for better performance.
        /// </summary>
        /// <param name="key">key</param>
        public void Remove(string key)
        {
            Editor editor = Edit();
            editor.Remove(key);
            editor.Apply();
        }

        public Editor Edit()
        {
            lock (_mutex)
            {
                AwaitLoadedLocked();
            }

            return new Editor(this);
        }

        internal void EnqueueDiskWrite(long memoryEpochSnapshot) => _scope.Launch(_dispatcher, async () => await Task.Run(() => CommitToDisk(memoryEpochSnapshot)));

        private void CommitToDisk(long memoryEpochSnapshot)
        {
            lock (_diskWriteMutex)
            {
                WriteToFile(memoryEpochSnapshot);
            }

            lock (_mutex)
            {
                _ongoingDiskWrites--;
            }
        }

        private void WriteToFile(long memoryEpochSnapshot)
        {
            if (_file.Exists)
            {
                lock (_mutex)
                {
                    // only write when:
                    // 1. disk has an older version than memory
                    // 2. snapshot is final (skip intermediate versions)
                    // otherwise, skip
                    if (!(_diskEpoch < _memoryEpoch && memoryEpochSnapshot == _memoryEpoch))
                    {
                        return;
                    }
                }

                if (!_backupFile.Exists)
                {
                    try
                    {
                        RenameFile(_file, _backupFile);
                    }
                    catch (Exception e)
                    {
                        Analytics.s_logger?.LogError(e, "Error encountered renaming file.");
                        return;
                    }
                }
                else
                {
                    _file.Delete();
                }
            }

            FileStream fs = null;
            try
            {
                fs = _file.Open(FileMode.Create);
                string json = JsonUtility.ToJson(_cache);
                byte[] bytes = json.GetBytes();
                fs.Write(bytes, 0, bytes.Length);
                fs.Close();

                // successfully updated file, can safely delete backup and return
                _backupFile.Delete();
                _diskEpoch = _memoryEpoch;

                return;
            }
            catch (Exception e)
            {
                fs?.Close();
                Analytics.s_logger?.LogError(e, "Error encountered updating file.");
            }

            // exception happens during update, remove partial updated file
            if (_file.Exists)
            {
                _file.Delete();
            }
        }

        private void StartLoadFromDisk()
        {
            lock (_mutex)
            {
                _loaded = false;
            }

            _scope.Launch(_dispatcher, async () => await Task.Run(LoadFromDisk));
        }

        private void LoadFromDisk()
        {
            Exception thrown = null;

            lock (_mutex)
            {
                if (_loaded)
                {
                    return;
                }

                try
                {
                    // if the directory does not exist, create it
                    if (!string.IsNullOrEmpty(_file.DirectoryName))
                    {
                        Directory.CreateDirectory(_file.DirectoryName);
                    }

                    // an update failed previously, recover from backup
                    if (_backupFile.Exists)
                    {
                        _file.Delete();
                        RenameFile(_backupFile, _file);
                    }
                }
                catch (Exception e)
                {
                    Analytics.s_logger?.LogError(e, "Error on restoring user prefs.");
                    thrown = e;
                }
            }

            Dictionary<string, object> dict = null;

            try
            {
                string json = File.ReadAllText(_file.FullName);
                if (string.IsNullOrEmpty(json))
                {
                    dict = new Dictionary<string, object>();
                }
                else
                {
                    dict = JsonUtility.FromJson<Dictionary<string, object>>(json);
                }
            }
            catch (Exception e)
            {
                Analytics.s_logger?.LogError(e, "Error on deserializing cached user prefs.");
                thrown = e;
            }

            lock (_mutex)
            {
                _loaded = true;

                // if there is no exception, use the loaded dictionary
                if (thrown == null)
                {
                    _cache = dict;
                }

                // if the cache is still null, init it with empty dictionary
                if (_cache == null)
                {
                    _cache = new Dictionary<string, object>();
                }

                // It's important that we always signal waiters, even if we'll make
                // them fail with an exception. The try-finally is pretty wide, but
                // better safe than sorry.
                Monitor.PulseAll(_mutex);
            }
        }

        private void AwaitLoadedLocked()
        {
            while (!_loaded)
            {
                Monitor.Wait(_mutex);
            }
        }

        private void RenameFile(FileInfo from, FileInfo to)
        {
            File.Move(from.FullName, to.FullName);
            from.Refresh();
            to.Refresh();
        }
    }

    #region Editor

    public class Editor
    {
        private readonly Dictionary<string, object> _modified;

        private readonly UserPrefs _userPrefs;

        private bool _clear;

        private readonly object _mutex;

        public Editor(UserPrefs userPrefs)
        {
            _modified = new Dictionary<string, object>();
            _userPrefs = userPrefs;
            _clear = false;
            _mutex = new object();
        }

        public Editor PutInt(string key, int value)
        {
            lock (_mutex)
            {
                _modified[key] = value;
            }

            return this;
        }

        public Editor PutFloat(string key, float value)
        {
            lock (_mutex)
            {
                _modified[key] = value;
            }

            return this;
        }

        public Editor PutString(string key, string value)
        {
            lock (_mutex)
            {
                _modified[key] = value;
            }

            return this;
        }

        public Editor Remove(string key)
        {
            lock (_mutex)
            {
                _modified[key] = this;
            }

            return this;
        }

        public Editor Clear()
        {
            lock (_mutex)
            {
                _modified.Clear();
                _clear = true;
            }

            return this;
        }

        public void Apply()
        {
            long memoryEpochSnapshot = CommitToMemory();
            _userPrefs.EnqueueDiskWrite(memoryEpochSnapshot);
        }

        private long CommitToMemory()
        {
            long memoryEpochSnapshot;

            lock (_userPrefs._mutex)
            {
                if (_userPrefs._ongoingDiskWrites > 0)
                {
                    // there are other writes going on, create a copy
                    _userPrefs._cache = new Dictionary<string, object>(_userPrefs._cache);
                }

                Dictionary<string, object> copyToDisk = _userPrefs._cache;
                _userPrefs._ongoingDiskWrites++;

                lock (_mutex)
                {
                    bool changesMade = false;

                    if (_clear)
                    {
                        if (copyToDisk.Count != 0)
                        {
                            changesMade = true;
                            copyToDisk.Clear();
                        }
                        _clear = false;
                    }

                    foreach (KeyValuePair<string, object> item in _modified)
                    {
                        string k = item.Key;
                        object v = item.Value;

                        if (v == this || v == null)
                        {
                            if (!copyToDisk.ContainsKey(k))
                            {
                                continue;
                                ;
                            }

                            copyToDisk.Remove(k);
                        }
                        else
                        {
                            if (copyToDisk.ContainsKey(k))
                            {
                                object existingValue = copyToDisk[k];
                                if (existingValue != null && existingValue.Equals(v))
                                {
                                    continue;
                                }
                            }

                            copyToDisk[k] = v;
                        }

                        changesMade = true;
                    }

                    _modified.Clear();

                    if (changesMade)
                    {
                        _userPrefs._memoryEpoch++;
                    }

                    memoryEpochSnapshot = _userPrefs._memoryEpoch;
                }
            }

            return memoryEpochSnapshot;
        }
    }

    #endregion
}
