using System;
using System.Collections.Generic;
using Segment.Serialization;

namespace Segment.Analytics
{
    public enum PluginType: int
    {
        Before = 0, // Executed before event processing begins
        Enrichment, // Executed as the first level of event processing
        Destination, // Executed as events begin to pass off to destinations.
        After, // Executed after all event processing is completed.  This can be used to perform cleanup operations, etc.
        Utility // Executed only when called manually, such as Logging.
    }

    public enum UpdateType
    {
        Initial,
        Refresh
    }

    /// <summary>
    /// Most simple abstraction for an plugin
    /// </summary>
    public abstract class Plugin
    {
        public abstract PluginType type { get; }
        public virtual Analytics analytics { get; set; }

        /// <summary>
        /// A simple setup function that's executed when plugin is attached to analytics
        /// If overridden, ensure that <c>base.Configure()</c> is invoked
        /// </summary>
        /// <param name="analytics"></param>
        public virtual void Configure(Analytics analytics)
        {
            this.analytics = analytics;
        }
        
        public virtual void Update(Settings settings, UpdateType type) { }
        
        public virtual RawEvent Execute(RawEvent incomingEvent)
        {
            return incomingEvent;
        }
        
        public virtual void Shutdown() { }
    }

    /// <summary>
    /// Advanced plugin that can act on specific event payloads
    /// </summary>
    public abstract class EventPlugin : Plugin
    {
        public virtual IdentifyEvent Identify(IdentifyEvent identifyEvent) => identifyEvent;

        public virtual TrackEvent Track(TrackEvent trackEvent) => trackEvent;

        public virtual GroupEvent Group(GroupEvent groupEvent) => groupEvent;

        public virtual AliasEvent Alias(AliasEvent aliasEvent) => aliasEvent;

        public virtual ScreenEvent Screen(ScreenEvent screenEvent) => screenEvent;

        public virtual void Reset() {}

        public virtual void Flush() {}

        public override RawEvent Execute(RawEvent incomingEvent)
        {
            switch (incomingEvent)
            {
                case IdentifyEvent e:
                    return Identify(e);
                case TrackEvent e:
                    return Track(e);
                case ScreenEvent e:
                    return Screen(e);
                case AliasEvent e:
                    return Alias(e);
                case GroupEvent e:
                    return Group(e);
                default:
                    return incomingEvent;
            }
        }
    }

    /// <summary>
    /// Basic abstraction for device-mode destinations. Allows overriding track, identify, screen, group, alias, flush and reset
    /// </summary>
    public abstract class DestinationPlugin : EventPlugin
    {
        public override PluginType type => PluginType.Destination;
        public abstract string key { get; }

        private bool _enabled = false;
        
        private readonly Timeline _timeline = new Timeline();

        public Plugin Add(Plugin plugin)
        {
            plugin.analytics = analytics;
            _timeline.Add(plugin);
            return plugin;
        }

        public void Remove(Plugin plugin)
        {
            _timeline.Remove(plugin);
        }

        public override void Configure(Analytics analytics)
        {
            this.analytics = analytics;
            Apply(plugin => plugin.Configure(analytics));
        }

        public void Apply(Action<Plugin> closure)
        {
            try
            {
                _timeline.Apply(closure);
            }
            catch (Exception e)
            {
                Analytics.logger?.LogError(e, "Error applying event in timeline.");
            }
        }

        /// <summary>
        /// Update <c>enabled</c> state of destination and apply settings update to destination timeline
        /// We recommend calling <c>base.update(..., ...)</c> in case this function is overridden
        /// </summary>
        /// <param name="settings">instance of <see cref="Settings"/></param>
        /// <param name="type">value of <see cref="UpdateType"/>. is the update an initialization or refreshment</param>
        public override void Update(Settings settings, UpdateType type)
        {
            _enabled = settings.integrations?.ContainsKey(key) ?? false;
            _timeline.Apply(plugin =>
            {
                plugin.Update(settings, type);
            });
        }

        /// <summary>
        /// Special function for DestinationPlugin that manages its own timeline execution
        /// </summary>
        /// <param name="event">event to process</param>
        /// <returns>event after processing</returns>
        public RawEvent Process(RawEvent @event)
        {
            if (!IsDestinationEnabled(@event))
            {
                return null;
            }
            
            var beforeResult = _timeline.ApplyPlugins(PluginType.Before, @event);
            var enrichmentResult = _timeline.ApplyPlugins(PluginType.Enrichment, beforeResult);

            RawEvent destinationResult;
            switch(enrichmentResult)
            {
                case AliasEvent e:
                    destinationResult = Alias(e);
                    break;
                case GroupEvent e:
                    destinationResult = Group(e);
                    break;
                case IdentifyEvent e:
                    destinationResult = Identify(e);
                    break;
                case ScreenEvent e:
                    destinationResult = Screen(e);
                    break;
                case TrackEvent e:
                    destinationResult = Track(e);
                    break;
                default:
                    destinationResult = enrichmentResult;
                    break;
            };

            var afterResult = _timeline.ApplyPlugins(PluginType.After, destinationResult);

            return afterResult;
        }

        public override RawEvent Execute(RawEvent incomingEvent) => Process(incomingEvent);

        internal bool IsDestinationEnabled(RawEvent @event)
        {
            // if event payload has integration marked false then its disabled by customer
            // default to true when missing
            var customerEnabled = @event?.integrations?.GetBool(key, true) ?? true;
            
            return _enabled && customerEnabled;
        }
    }

    public abstract class UtilityPlugin : Plugin { }
    
    public partial class Analytics
    {   
        /// <summary>
        /// Apply a closure to all plugins registered to the analytics client. Ideal for invoking
        /// functions for Utility plugins
        /// </summary>
        /// <param name="closure">Closure of what should be applied</param>
        public void Apply(Action<Plugin> closure) => timeline.Apply(closure);

        /// <summary>
        /// Register a plugin to the analytics timeline
        /// </summary>
        /// <param name="plugin">Plugin to be added</param>
        /// <returns>Plugin that added to analytics</returns>
        public Plugin Add(Plugin plugin)
        {
            plugin.Configure(this);
            timeline.Add(plugin);
            return plugin;
        }

        /// <summary>
        /// Remove a plugin from the analytics timeline
        /// </summary>
        /// <param name="plugin">the plugin to be removed</param>
        public void Remove(Plugin plugin)
        {
            timeline.Remove(plugin);
        }

        /// <summary>
        /// Retrieve the first match of registered plugin. It finds
        ///      1. the first instance of the given class/interface
        ///      2. or the first instance of subclass of the given class/interface
        /// </summary>
        /// <typeparam name="T">Type that implements <see cref="Plugin"/></typeparam>
        /// <returns>The plugin instance of given type T</returns>
        public T Find<T>() where T : Plugin => timeline.Find<T>();

        /// <summary>
        /// Retrieve all matches of registered plugins. It finds
        ///      1. all instances of the given class/interface
        ///      2. and all instances of subclass of the given class/interface
        /// </summary>
        /// <typeparam name="T">Type that implements <see cref="Plugin"/></typeparam>
        /// <returns>A collection of plugins of the given type T</returns>
        public IEnumerable<T> FindAll<T>() where T : Plugin => timeline.FindAll<T>();

        /// <summary>
        /// Retrieve the first match of registered destination plugin by key. It finds
        /// </summary>
        /// <param name="destinationKey">the key of <see cref="DestinationPlugin"/></param>
        /// <returns></returns>
        public DestinationPlugin Find(string destinationKey) => timeline.Find(destinationKey);
    }
}