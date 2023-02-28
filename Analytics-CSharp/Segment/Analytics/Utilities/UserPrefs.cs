namespace Segment.Analytics.Utilities
{
    using global::System;
    using global::System.Collections.Concurrent;
    using global::System.Collections.Generic;
    using global::System.IO;
    using global::System.Threading;
    using global::System.Threading.Tasks;
    using Segment.Concurrent;
    using Segment.Serialization;

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
                ret = Convert.ToInt32(this._cache[key]);
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
                ret = float.Parse(Convert.ToString(this._cache[key]));
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
                ret = Convert.ToString(this._cache[key]);
            }
            catch
            {
                ret = defaultValue;
            }

            return ret;
        }

        public bool Contains(string key) => this._cache.ContainsKey(key);

        public void Put(string key, int value) => this._cache[key] = value;

        public void Put(string key, float value) => this._cache[key] = value;

        public void Put(string key, string value) => this._cache[key] = value;

        public void Remove(string key) => this._cache.Remove(key);
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
            this._cache = new Dictionary<string, object>();
            this._mutex = new object();
            this._diskWriteMutex = new object();
            this._ongoingDiskWrites = 0;
            this._memoryEpoch = 0;
            this._diskEpoch = 0;
            this._loaded = false;
            this._file = new FileInfo(file);
            this._backupFile = new FileInfo(file + ".bak");

            // uses a new scope for UserPrefs, so interruption does not propagate to analytics scope
            // in addition, file I/O in this class are all blocking. need to have its own threads
            // to prevent blocking analytics threads
            this._scope = new Scope(exceptionHandler);
            this._dispatcher = new Dispatcher(new LimitedConcurrencyLevelTaskScheduler(Environment.ProcessorCount));
            this.StartLoadFromDisk();
        }

        public int GetInt(string key, int defaultValue = -1)
        {
            int ret;

            lock (this._mutex)
            {
                this.AwaitLoadedLocked();

                try
                {
                    ret = Convert.ToInt32(this._cache[key]);
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

            lock (this._mutex)
            {
                this.AwaitLoadedLocked();

                try
                {
                    ret = float.Parse(Convert.ToString(this._cache[key]));
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

            lock (this._mutex)
            {
                this.AwaitLoadedLocked();

                try
                {
                    ret = Convert.ToString(this._cache[key]);
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
            this.AwaitLoadedLocked();

            lock (this._mutex)
            {
                return this._cache.ContainsKey(key);
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
            var editor = this.Edit();
            _ = editor.PutInt(key, value);
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
            var editor = this.Edit();
            _ = editor.PutFloat(key, value);
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
            var editor = this.Edit();
            _ = editor.PutString(key, value);
            editor.Apply();
        }

        /// <summary>
        /// Use for one shot key-value update. If you need to update multiple values at once, try <see cref="Edit"/>
        /// to get an Editor and apply the changes all at once for better performance.
        /// </summary>
        /// <param name="key">key</param>
        public void Remove(string key)
        {
            var editor = this.Edit();
            _ = editor.Remove(key);
            editor.Apply();
        }

        public Editor Edit()
        {
            lock (this._mutex)
            {
                this.AwaitLoadedLocked();
            }

            return new Editor(this);
        }

        internal void EnqueueDiskWrite(long memoryEpochSnapshot) => this._scope.Launch(this._dispatcher, async () => await Task.Run(() => this.CommitToDisk(memoryEpochSnapshot)));

        private void CommitToDisk(long memoryEpochSnapshot)
        {
            lock (this._diskWriteMutex)
            {
                this.WriteToFile(memoryEpochSnapshot);
            }

            lock (this._mutex)
            {
                this._ongoingDiskWrites--;
            }
        }

        private void WriteToFile(long memoryEpochSnapshot)
        {
            if (this._file.Exists)
            {
                lock (this._mutex)
                {
                    // only write when:
                    // 1. disk has an older version than memory
                    // 2. snapshot is final (skip intermediate versions)
                    // otherwise, skip
                    if (!(this._diskEpoch < this._memoryEpoch && memoryEpochSnapshot == this._memoryEpoch))
                    {
                        return;
                    }
                }

                if (!this._backupFile.Exists)
                {
                    try
                    {
                        this.RenameFile(this._file, this._backupFile);
                    }
                    catch (Exception e)
                    {
                        Analytics.s_logger?.LogError(e, "Error encountered renaming file.");
                        return;
                    }
                }
                else
                {
                    this._file.Delete();
                }
            }

            FileStream fs = null;
            try
            {
                fs = this._file.Open(FileMode.Create);
                var json = JsonUtility.ToJson(this._cache);
                var bytes = json.GetBytes();
                fs.Write(bytes, 0, bytes.Length);
                fs.Close();

                // successfully updated file, can safely delete backup and return
                this._backupFile.Delete();
                this._diskEpoch = this._memoryEpoch;

                return;
            }
            catch (Exception e)
            {
                fs?.Close();
                Analytics.s_logger?.LogError(e, "Error encountered updating file.");
            }

            // exception happens during update, remove partial updated file
            if (this._file.Exists)
            {
                this._file.Delete();
            }
        }

        private void StartLoadFromDisk()
        {
            lock (this._mutex)
            {
                this._loaded = false;
            }

            _ = this._scope.Launch(this._dispatcher, async () => await Task.Run(this.LoadFromDisk));
        }

        private void LoadFromDisk()
        {
            Exception thrown = null;

            lock (this._mutex)
            {
                if (this._loaded)
                {
                    return;
                }

                try
                {
                    // if the directory does not exist, create it
                    if (!string.IsNullOrEmpty(this._file.DirectoryName))
                    {
                        _ = Directory.CreateDirectory(this._file.DirectoryName);
                    }

                    // an update failed previously, recover from backup
                    if (this._backupFile.Exists)
                    {
                        this._file.Delete();
                        this.RenameFile(this._backupFile, this._file);
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
                var json = File.ReadAllText(this._file.FullName);
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

            lock (this._mutex)
            {
                this._loaded = true;

                // if there is no exception, use the loaded dictionary
                if (thrown == null)
                {
                    this._cache = dict;
                }

                // if the cache is still null, init it with empty dictionary
                if (this._cache == null)
                {
                    this._cache = new Dictionary<string, object>();
                }

                // It's important that we always signal waiters, even if we'll make
                // them fail with an exception. The try-finally is pretty wide, but
                // better safe than sorry.
                Monitor.PulseAll(this._mutex);
            }
        }

        private void AwaitLoadedLocked()
        {
            while (!this._loaded)
            {
                _ = Monitor.Wait(this._mutex);
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
            this._modified = new Dictionary<string, object>();
            this._userPrefs = userPrefs;
            this._clear = false;
            this._mutex = new object();
        }

        public Editor PutInt(string key, int value)
        {
            lock (this._mutex)
            {
                this._modified[key] = value;
            }

            return this;
        }

        public Editor PutFloat(string key, float value)
        {
            lock (this._mutex)
            {
                this._modified[key] = value;
            }

            return this;
        }

        public Editor PutString(string key, string value)
        {
            lock (this._mutex)
            {
                this._modified[key] = value;
            }

            return this;
        }

        public Editor Remove(string key)
        {
            lock (this._mutex)
            {
                this._modified[key] = this;
            }

            return this;
        }

        public Editor Clear()
        {
            lock (this._mutex)
            {
                this._modified.Clear();
                this._clear = true;
            }

            return this;
        }

        public void Apply()
        {
            var memoryEpochSnapshot = this.CommitToMemory();
            this._userPrefs.EnqueueDiskWrite(memoryEpochSnapshot);
        }

        private long CommitToMemory()
        {
            long memoryEpochSnapshot;

            lock (this._userPrefs._mutex)
            {
                if (this._userPrefs._ongoingDiskWrites > 0)
                {
                    // there are other writes going on, create a copy
                    this._userPrefs._cache = new Dictionary<string, object>(this._userPrefs._cache);
                }

                var copyToDisk = this._userPrefs._cache;
                this._userPrefs._ongoingDiskWrites++;

                lock (this._mutex)
                {
                    var changesMade = false;

                    if (this._clear)
                    {
                        if (copyToDisk.Count != 0)
                        {
                            changesMade = true;
                            copyToDisk.Clear();
                        }
                        this._clear = false;
                    }

                    foreach (var item in this._modified)
                    {
                        var k = item.Key;
                        var v = item.Value;

                        if (v == this || v == null)
                        {
                            if (!copyToDisk.ContainsKey(k))
                            {
                                continue;
                                ;
                            }

                            _ = copyToDisk.Remove(k);
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

                    this._modified.Clear();

                    if (changesMade)
                    {
                        this._userPrefs._memoryEpoch++;
                    }

                    memoryEpochSnapshot = this._userPrefs._memoryEpoch;
                }
            }

            return memoryEpochSnapshot;
        }
    }

    #endregion
}
