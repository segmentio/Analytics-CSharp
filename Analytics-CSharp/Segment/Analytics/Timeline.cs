﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace Segment.Analytics
{
    /// <summary>
    /// Platform abstraction for managing all plugins and their execution
    /// Currently the execution follows
    ///      Before -> Enrichment -> Destination -> After
    /// </summary>
    public class Timeline
    {
        internal Dictionary<PluginType, Mediator> plugins;

        public Timeline()
        {
            plugins = new Dictionary<PluginType, Mediator>()
            {
                { PluginType.Before, new Mediator() },
                { PluginType.Enrichment, new Mediator() },
                { PluginType.Destination, new Mediator() },
                { PluginType.After, new Mediator() },
                { PluginType.Utility, new Mediator() }
            };
        }
        
        /// <summary>
        /// initiate the event's lifecycle
        /// </summary>
        /// <param name="incomingEvent">event to be processed</param>
        /// <returns>event after processing</returns>
        internal RawEvent Process(RawEvent incomingEvent)
        {
            // Apply before and enrichment types first to start the timeline processing.
            var beforeResult = ApplyPlugins(PluginType.Before, incomingEvent);
            // Enrichment is like middleware, a chance to update the event across the board before going to destinations.
            var enrichmentResult = ApplyPlugins(PluginType.Enrichment, beforeResult);
            
            // Make sure not to update the events during this next cycle. Since each destination may want different 
            // data than other destinations we don't want them conflicting and changing what a real result should be
            ApplyPlugins(PluginType.Destination, enrichmentResult);
            
            // Finally end with after plugins
            var afterResult = ApplyPlugins(PluginType.After, enrichmentResult);
            
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
            var returnEvent = incomingEvent;
            var mediator = plugins[type];
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
            foreach (var plugin in plugins.Select(item => item.Value).SelectMany(mediator => mediator.plugins))
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
            var mediator = plugins[plugin.type];
            mediator?.Add(plugin);
        }

        /// <summary>
        /// Remove a registered plugin
        /// </summary>
        /// <param name="plugin">plugin to be removed</param>
        internal void Remove(Plugin plugin)
        {
            // Remove all plugins with this name in every category
            foreach (var item in plugins.ToList())
            {
                var mediator = item.Value;
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
            foreach (var item in plugins)
            {
                var found = item.Value.Find<T>();
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
            
            foreach (var item in plugins)
            {
                var found = item.Value.FindAll<T>();
                result.AddRange(found);
            }

            return result;
        }

        /// <summary>
        /// Find a destination plugin by its key
        /// </summary>
        /// <param name="destination">key of <see cref="DestinationPlugin"/></param>
        /// <returns>instance of destination plugin of given key</returns>
        public DestinationPlugin Find(string destination)
        {
            return plugins[PluginType.Destination]?.plugins?.Find(it =>
                it is DestinationPlugin plugin && plugin.key.Equals(destination)) as DestinationPlugin;
        }
        
        #endregion
    }

    internal class Mediator
    {
        internal List<Plugin> plugins = new List<Plugin>();

        internal void Add(Plugin plugin)
        {
            plugins.Add(plugin);

            var analytics = plugin.analytics;
            analytics.analyticsScope.Launch(analytics.analyticsDispatcher, async () =>
            {
                var settings = await plugin.analytics.SettingsAsync();
                if (settings.HasValue)
                {
                    plugin.Update(settings.Value, UpdateType.Initial);
                }
            });
        }

        internal void Remove(Plugin plugin)
        {
            plugins.RemoveAll(tempPlugin => tempPlugin == plugin);
        }
        
        internal RawEvent Execute(RawEvent incomingEvent)
        {
            RawEvent result = incomingEvent;
            foreach (var plugin in plugins.Where(plugin => result != null))
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

        public T Find<T>() where T : Plugin
        {
            return (T) plugins.FirstOrDefault(typeof(T).IsInstanceOfType);
        }

        public IEnumerable<T> FindAll<T>() where T : Plugin
        {
            return plugins.Where(typeof(T).IsInstanceOfType).Cast<T>();
        }
    }
}