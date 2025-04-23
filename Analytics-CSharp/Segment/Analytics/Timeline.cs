using System.Collections.Concurrent;
using System.Reflection;
using global::System;
using global::System.Collections.Generic;
using global::System.Linq;

namespace Segment.Analytics
{
    /// <summary>
    /// Platform abstraction for managing all plugins and their execution
    /// Currently the execution follows
    ///      Before -> Enrichment -> Destination -> After
    /// </summary>
    public class Timeline
    {
        internal IDictionary<PluginType, Mediator> _plugins;

        public Timeline() => _plugins = new ConcurrentDictionary<PluginType, Mediator>
            {
                [PluginType.Before] = new Mediator(),
                [PluginType.Enrichment] = new Mediator(),
                [PluginType.Destination] = new Mediator(),
                [PluginType.After] = new Mediator(),
                [PluginType.Utility] = new Mediator()
            };

        /// <summary>
        /// initiate the event's lifecycle
        /// </summary>
        /// <param name="incomingEvent">event to be processed</param>
        /// <param name="enrichment">a closure that enables enrichment on the generated event</param>
        /// <returns>event after processing</returns>
        internal RawEvent Process(RawEvent incomingEvent)
        {
            // Apply before and enrichment types first to start the timeline processing.
            RawEvent beforeResult = ApplyPlugins(PluginType.Before, incomingEvent);
            // Enrichment is like middleware, a chance to update the event across the board before going to destinations.
            RawEvent enrichmentResult = ApplyPlugins(PluginType.Enrichment, beforeResult);
            if (enrichmentResult != null && enrichmentResult.Enrichment != null)
            {
                enrichmentResult = enrichmentResult.Enrichment(enrichmentResult);
            }

            // Make sure not to update the events during this next cycle. Since each destination may want different
            // data than other destinations we don't want them conflicting and changing what a real result should be
            ApplyPlugins(PluginType.Destination, enrichmentResult);

            // Finally end with after plugins
            RawEvent afterResult = ApplyPlugins(PluginType.After, enrichmentResult);

            return afterResult;
        }

        /// <summary>
        /// Runs all registered plugins of a particular type on given payload
        /// </summary>
        /// <param name="type">type of <see cref="PluginType"/></param>
        /// <param name="incomingEvent">event to be processed</param>
        /// <returns>processed event</returns>
        internal RawEvent ApplyPlugins(PluginType type, RawEvent incomingEvent)
        {
            RawEvent returnEvent = incomingEvent;
            Mediator mediator = _plugins[type];
            if (returnEvent != null)
            {
                returnEvent = mediator.Execute(returnEvent);
            }
            return returnEvent;
        }

        #region Plugin Support

        /// <summary>
        /// Applies a closure on all registered plugins
        /// </summary>
        /// <param name="closure">closure that applies to plugin</param>
        internal void Apply(Action<Plugin> closure)
        {
            foreach (Plugin plugin in _plugins.Select(item => item.Value).SelectMany(mediator => mediator._plugins))
            {
                closure(plugin);
            }
        }

        /// <summary>
        /// Register a new plugin
        /// </summary>
        /// <param name="plugin">plugin to be registered</param>
        internal void Add(Plugin plugin)
        {
            Mediator mediator = _plugins[plugin.Type];
            mediator?.Add(plugin);
        }

        /// <summary>
        /// Remove a registered plugin
        /// </summary>
        /// <param name="plugin">plugin to be removed</param>
        internal void Remove(Plugin plugin)
        {
            // Remove all plugins with this name in every category
            foreach (KeyValuePair<PluginType, Mediator> item in _plugins)
            {
                Mediator mediator = item.Value;
                mediator.Remove(plugin);
            }
        }

        /// <summary>
        /// Find a registered plugin of given type
        /// </summary>
        /// <typeparam name="T">type that inherits <see cref="Plugin"/></typeparam>
        /// <returns>instance of given type registered in analytics</returns>
        public T Find<T>() where T : Plugin
        {
            foreach (KeyValuePair<PluginType, Mediator> item in _plugins)
            {
                T found = item.Value.Find<T>();
                if (found != null)
                {
                    return found;
                }
            }

            return default;
        }

        /// <summary>
        /// Find all registered plugins of given type, including subtypes
        /// </summary>
        /// <typeparam name="T">type that inherits <see cref="Plugin"/></typeparam>
        /// <returns>list of instances of given type registered in analytics</returns>
        public IEnumerable<T> FindAll<T>() where T : Plugin
        {
            var result = new List<T>();

            foreach (KeyValuePair<PluginType, Mediator> item in _plugins)
            {
                IEnumerable<T> found = item.Value.FindAll<T>();
                result.AddRange(found);
            }

            return result;
        }

        /// <summary>
        /// Find a destination plugin by its key
        /// </summary>
        /// <param name="destination">key of <see cref="DestinationPlugin"/></param>
        /// <returns>instance of destination plugin of given key</returns>
        public DestinationPlugin Find(string destination) => _plugins[PluginType.Destination]?._plugins?.Find(it =>
                                                                          it is DestinationPlugin plugin && plugin.Key.Equals(destination)) as DestinationPlugin;

        #endregion
    }

    internal class Mediator
    {
        internal List<Plugin> _plugins = new List<Plugin>();

        internal void Add(Plugin plugin)
        {
            _plugins.Add(plugin);

            Analytics analytics = plugin.Analytics;
            analytics.AnalyticsScope.Launch(analytics.AnalyticsDispatcher, async () =>
            {
                Settings settings = await plugin.Analytics.SettingsAsync();
                // Fetch system afterwards for a minuscule but cool performance gain
                System system = await analytics.Store.CurrentState<System>();

                // Don't initialize unless we have updated settings from the web.
                // CheckSettings will initialize everything added before then, so wait until other inits have happened.
                // Check for nullability because CurrentState returns default(IState) which could make the .Count throw a NullReferenceException
                if (system._initializedPlugins != null && system._initializedPlugins.Count > 0)
                {
                    await analytics.Store.Dispatch<System.AddInitializedPluginAction, System>(new System.AddInitializedPluginAction(new HashSet<int>{plugin.GetHashCode()}));
                    plugin.Update(settings, UpdateType.Initial);
                }
            });
        }


        internal void Remove(Plugin plugin) => _plugins.RemoveAll(tempPlugin => tempPlugin == plugin);

        internal RawEvent Execute(RawEvent incomingEvent)
        {
            RawEvent result = incomingEvent;
            foreach (Plugin plugin in _plugins.Where(plugin => result != null))
            {
                if (plugin is DestinationPlugin)
                {
                    plugin.Execute(result);
                }
                else
                {
                    result = plugin.Execute(result);
                }
            }
            return result;
        }

        public T Find<T>() where T : Plugin => (T)_plugins.FirstOrDefault(o => typeof(T).GetTypeInfo().IsAssignableFrom(o.GetType().GetTypeInfo()));

        public IEnumerable<T> FindAll<T>() where T : Plugin => _plugins.Where(o => typeof(T).GetTypeInfo().IsAssignableFrom(o.GetType().GetTypeInfo())).Cast<T>();
    }
}
