using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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
                ret = float.Parse(Convert.ToString(_cache[key]));;
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

        public bool Contains(string key)
        {
            return _cache.ContainsKey(key);
        }

        public void Put(string key, int value)
        {
            _cache[key] = value;
        }

        public void Put(string key, float value)
        {
            _cache[key] = value;
        }

        public void Put(string key, string value)
        {
            _cache[key] = value;
        }

        public void Remove(string key)
        {
            _cache.Remove(key);
        }
    }
    
    /**
     * This UserPrefs is a translation of the Android SharedPreference.
     * Refer to <see cref="https://android.googlesource.com/platform/frameworks/base.git/+/master/core/java/android/app/SharedPreferencesImpl.java"/>
     * for the original implementation.
     */
    public class UserPrefs : IPreferences
    {
        internal Dictionary<string, object> cache;

        internal readonly object mutex;

        internal readonly object diskWriteMutex;

        internal int ongoingDiskWrites;

        internal long memoryEpoch;

        internal long diskEpoch;

        private bool _loaded;

        private readonly FileInfo _file;

        private readonly FileInfo _backupFile;

        private readonly Scope _scope;

        private readonly IDispatcher _dispatcher;

        public UserPrefs(string file, ICoroutineExceptionHandler exceptionHandler = null)
        {
            cache = new Dictionary<string, object>();
            mutex = new object();
            diskWriteMutex = new object();
            ongoingDiskWrites = 0;
            memoryEpoch = 0;
            diskEpoch = 0;
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
            
            lock (mutex)
            {
                AwaitLoadedLocked();
                
                try
                {
                    ret = Convert.ToInt32(cache[key]);
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
            
            lock (mutex)
            {
                AwaitLoadedLocked();

                try
                {
                    ret = float.Parse(Convert.ToString(cache[key]));
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
            
            lock (mutex)
            {
                AwaitLoadedLocked();

                try
                {
                    ret = Convert.ToString(cache[key]);
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

            lock (mutex)
            {
                return cache.ContainsKey(key);
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
            var editor = Edit();
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
            var editor = Edit();
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
            var editor = Edit();
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
            var editor = Edit();
            editor.Remove(key);
            editor.Apply();
        }

        public Editor Edit()
        {
            lock (mutex)
            {
                AwaitLoadedLocked();
            }
            
            return new Editor(this);
        }

        internal void EnqueueDiskWrite(long memoryEpochSnapshot)
        {
            _scope.Launch(_dispatcher, async () =>
            {
                await Task.Run(() => { CommitToDisk(memoryEpochSnapshot); });
            });
        }

        private void CommitToDisk(long memoryEpochSnapshot)
        {
            lock (diskWriteMutex)
            {
                WriteToFile(memoryEpochSnapshot);    
            }
            
            lock (mutex)
            {   
                ongoingDiskWrites--;
            }
        }

        private void WriteToFile(long memoryEpochSnapshot)
        {
            if (_file.Exists)
            {
                lock (mutex)
                {
                    // only write when:
                    // 1. disk has an older version than memory
                    // 2. snapshot is final (skip intermediate versions)
                    // otherwise, skip
                    if (!(diskEpoch < memoryEpoch && memoryEpochSnapshot == memoryEpoch))
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
                        Analytics.logger?.LogError(e, "Error encountered renaming file.");
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
                var json = JsonUtility.ToJson(cache);
                var bytes = json.GetBytes();
                fs.Write(bytes, 0, bytes.Length);
                fs.Close();

                // successfully updated file, can safely delete backup and return
                _backupFile.Delete();
                diskEpoch = memoryEpoch;

                return;
            }
            catch (Exception e)
            {
                fs?.Close();
                Analytics.logger?.LogError(e, "Error encountered updating file.");
            }

            // exception happens during update, remove partial updated file
            if (_file.Exists)
            {
                _file.Delete();
            }
        }

        private void StartLoadFromDisk()
        {
            lock (mutex)
            {
                _loaded = false;
            }
            
            _scope.Launch(_dispatcher, async () =>
            {
                await Task.Run(LoadFromDisk);
            });
        }

        private void LoadFromDisk()
        {
            Exception thrown = null;
            
            lock (mutex)
            {
                if (_loaded) return;

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
                    Analytics.logger?.LogError(e, "Error on restoring user prefs.");
                    thrown = e;
                }
            }

            Dictionary<string, object> dict = null;

            try
            {
                var json = File.ReadAllText(_file.FullName);
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
                Analytics.logger?.LogError(e, "Error on deserializing cached user prefs.");
                thrown = e;
            }
            
            lock (mutex)
            {
                _loaded = true;

                // if there is no exception, use the loaded dictionary
                if (thrown == null)
                {
                    cache = dict;    
                }

                // if the cache is still null, init it with empty dictionary
                if (cache == null)
                {
                    cache = new Dictionary<string, object>();
                }
                
                // It's important that we always signal waiters, even if we'll make
                // them fail with an exception. The try-finally is pretty wide, but
                // better safe than sorry.
                Monitor.PulseAll(mutex);
            }
        }

        private void AwaitLoadedLocked()
        {
            while (!_loaded)
            {
                Monitor.Wait(mutex);
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
        private Dictionary<string, object> _modified;

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
            var memoryEpochSnapshot = CommitToMemory();
            _userPrefs.EnqueueDiskWrite(memoryEpochSnapshot);
        }
        
        private long CommitToMemory()
        {
            long memoryEpochSnapshot;
            
            lock (_userPrefs.mutex)
            {
                if (_userPrefs.ongoingDiskWrites > 0)
                {
                    // there are other writes going on, create a copy
                    _userPrefs.cache = new Dictionary<string, object>(_userPrefs.cache);
                }
                
                var copyToDisk = _userPrefs.cache;
                _userPrefs.ongoingDiskWrites++;

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

                    foreach (var item in _modified)
                    {
                        var k = item.Key;
                        var v = item.Value;

                        if (v == this || v == null)
                        {
                            if (!copyToDisk.ContainsKey(k))
                            {
                                continue;;
                            }

                            copyToDisk.Remove(k);
                        }
                        else
                        {
                            if (copyToDisk.ContainsKey(k))
                            {
                                var existingValue = copyToDisk[k];
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
                        _userPrefs.memoryEpoch++;
                    }

                    memoryEpochSnapshot = _userPrefs.memoryEpoch;
                }
            }

            return memoryEpochSnapshot;
        }
    }
    
    #endregion
}