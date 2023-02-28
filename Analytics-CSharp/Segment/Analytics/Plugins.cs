namespace Segment.Analytics
{
    using global::System;
    using global::System.Collections.Generic;
    using Segment.Serialization;

    public enum PluginType : int
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
        public abstract PluginType Type { get; }
        public virtual Analytics Analytics { get; set; }

        /// <summary>
        /// A simple setup function that's executed when plugin is attached to analytics
        /// If overridden, ensure that <c>base.Configure()</c> is invoked
        /// </summary>
        /// <param name="analytics"></param>
        public virtual void Configure(Analytics analytics) => this.Analytics = analytics;

        public virtual void Update(Settings settings, UpdateType type) { }

        public virtual RawEvent Execute(RawEvent incomingEvent) => incomingEvent;

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

        public virtual void Reset() { }

        public virtual void Flush() { }

        public override RawEvent Execute(RawEvent incomingEvent)
        {
            switch (incomingEvent)
            {
                case IdentifyEvent e:
                    return this.Identify(e);
                case TrackEvent e:
                    return this.Track(e);
                case ScreenEvent e:
                    return this.Screen(e);
                case AliasEvent e:
                    return this.Alias(e);
                case GroupEvent e:
                    return this.Group(e);
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
        public override PluginType Type => PluginType.Destination;
        public abstract string Key { get; }

        private bool _enabled = false;

        private readonly Timeline _timeline = new Timeline();

        public Plugin Add(Plugin plugin)
        {
            plugin.Analytics = this.Analytics;
            this._timeline.Add(plugin);
            return plugin;
        }

        public void Remove(Plugin plugin) => this._timeline.Remove(plugin);

        public override void Configure(Analytics analytics)
        {
            this.Analytics = analytics;
            this.Apply(plugin => plugin.Configure(analytics));
        }

        public void Apply(Action<Plugin> closure)
        {
            try
            {
                this._timeline.Apply(closure);
            }
            catch (Exception e)
            {
                Analytics.s_logger?.LogError(e, "Error applying event in timeline.");
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
            this._enabled = settings.Integrations?.ContainsKey(this.Key) ?? false;
            this._timeline.Apply(plugin => plugin.Update(settings, type));
        }

        /// <summary>
        /// Special function for DestinationPlugin that manages its own timeline execution
        /// </summary>
        /// <param name="event">event to process</param>
        /// <returns>event after processing</returns>
        public RawEvent Process(RawEvent @event)
        {
            if (!this.IsDestinationEnabled(@event))
            {
                return null;
            }

            var beforeResult = this._timeline.ApplyPlugins(PluginType.Before, @event);
            var enrichmentResult = this._timeline.ApplyPlugins(PluginType.Enrichment, beforeResult);

            RawEvent destinationResult;
            switch (enrichmentResult)
            {
                case AliasEvent e:
                    destinationResult = this.Alias(e);
                    break;
                case GroupEvent e:
                    destinationResult = this.Group(e);
                    break;
                case IdentifyEvent e:
                    destinationResult = this.Identify(e);
                    break;
                case ScreenEvent e:
                    destinationResult = this.Screen(e);
                    break;
                case TrackEvent e:
                    destinationResult = this.Track(e);
                    break;
                default:
                    destinationResult = enrichmentResult;
                    break;
            };

            var afterResult = this._timeline.ApplyPlugins(PluginType.After, destinationResult);

            return afterResult;
        }

        public override RawEvent Execute(RawEvent incomingEvent) => this.Process(incomingEvent);

        internal bool IsDestinationEnabled(RawEvent @event)
        {
            // if event payload has integration marked false then its disabled by customer
            // default to true when missing
            var customerEnabled = @event?.Integrations?.GetBool(this.Key, true) ?? true;

            return this._enabled && customerEnabled;
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
        public void Apply(Action<Plugin> closure) => this.Timeline.Apply(closure);

        /// <summary>
        /// Register a plugin to the analytics timeline
        /// </summary>
        /// <param name="plugin">Plugin to be added</param>
        /// <returns>Plugin that added to analytics</returns>
        public Plugin Add(Plugin plugin)
        {
            plugin.Configure(this);
            this.Timeline.Add(plugin);
            return plugin;
        }

        /// <summary>
        /// Remove a plugin from the analytics timeline
        /// </summary>
        /// <param name="plugin">the plugin to be removed</param>
        public void Remove(Plugin plugin) => this.Timeline.Remove(plugin);

        /// <summary>
        /// Retrieve the first match of registered plugin. It finds
        ///      1. the first instance of the given class/interface
        ///      2. or the first instance of subclass of the given class/interface
        /// </summary>
        /// <typeparam name="T">Type that implements <see cref="Plugin"/></typeparam>
        /// <returns>The plugin instance of given type T</returns>
        public T Find<T>() where T : Plugin => this.Timeline.Find<T>();

        /// <summary>
        /// Retrieve all matches of registered plugins. It finds
        ///      1. all instances of the given class/interface
        ///      2. and all instances of subclass of the given class/interface
        /// </summary>
        /// <typeparam name="T">Type that implements <see cref="Plugin"/></typeparam>
        /// <returns>A collection of plugins of the given type T</returns>
        public IEnumerable<T> FindAll<T>() where T : Plugin => this.Timeline.FindAll<T>();

        /// <summary>
        /// Retrieve the first match of registered destination plugin by key. It finds
        /// </summary>
        /// <param name="destinationKey">the key of <see cref="DestinationPlugin"/></param>
        /// <returns></returns>
        public DestinationPlugin Find(string destinationKey) => this.Timeline.Find(destinationKey);
    }
}
