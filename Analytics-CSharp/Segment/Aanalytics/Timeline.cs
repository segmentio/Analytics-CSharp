using System;
using System.Collections.Generic;
using System.Linq;

namespace Segment.Analytics
{
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
        
        internal RawEvent Process<TE>(TE incomingEvent) where TE : RawEvent
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

        internal void Apply(Action<Plugin> closure)
        {
            foreach (var plugin in plugins.Select(item => item.Value).SelectMany(mediator => mediator.plugins))
            {
                closure(plugin);
            }
        }

        internal void Add(Plugin plugin)
        {
            var mediator = plugins[plugin.type];
            mediator?.Add(plugin);
        }

        internal void Remove(Plugin plugin)
        {
            // Remove all plugins with this name in every category
            foreach (var item in plugins)
            {
                var mediator = item.Value;
                
                var toRemove = mediator.plugins.Where(storedPlugin => storedPlugin == plugin);
                foreach (var removePlugin in toRemove)
                {
                    removePlugin.Shutdown();
                    mediator.Remove(removePlugin);
                }
            }
        }

        public T Find<T>(Type plugin) where T : Plugin
        {
            foreach (var item in plugins)
            {
                var found = item.Value.Find<T>(plugin);
                if (found != null)
                {
                    return found;
                }
            }

            return default;
        }
        
        public IEnumerable<T> FindAll<T>(Type plugin) where T : Plugin
        {
            var result = new List<T>();
            
            foreach (var item in plugins)
            {
                var found = item.Value.FindAll<T>(plugin);
                result.AddRange(found);
            }

            return result;
        }

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
            
            var settings = plugin.analytics.Settings();
            if (settings.HasValue)
            {
                plugin.Update(settings.Value, UpdateType.Initial);
            }
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

        public T Find<T>(Type pluginType) where T : Plugin
        {
            return (T) plugins.FirstOrDefault(pluginType.IsInstanceOfType);
        }

        public IEnumerable<T> FindAll<T>(Type pluginType) where T : Plugin
        {
            return plugins.Where(pluginType.IsInstanceOfType).Cast<T>();
        }
    }
}