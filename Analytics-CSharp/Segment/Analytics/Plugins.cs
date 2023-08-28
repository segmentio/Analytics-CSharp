using global::System;
using global::System.Collections.Generic;
using Segment.Analytics.Utilities;
using Segment.Serialization;

namespace Segment.Analytics
{
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
        public virtual void Configure(Analytics analytics) => Analytics = analytics;

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

        public virtual PageEvent Page(PageEvent pageEvent) => pageEvent;

        public virtual void Reset() { }

        public virtual void Flush() { }

        public override RawEvent Execute(RawEvent incomingEvent)
        {
            switch (incomingEvent)
            {
                case IdentifyEvent e:
                    return Identify(e);
                case TrackEvent e:
                    return Track(e);
                case PageEvent e:
                    return Page(e);
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
        public override PluginType Type => PluginType.Destination;
        public abstract string Key { get; }

        internal bool _enabled = false;

        private readonly Timeline _timeline = new Timeline();

        public Plugin Add(Plugin plugin)
        {
            plugin.Analytics = Analytics;
            _timeline.Add(plugin);
            return plugin;
        }

        public void Remove(Plugin plugin) => _timeline.Remove(plugin);

        public override void Configure(Analytics analytics)
        {
            Analytics = analytics;
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
                Analytics.Logger.Log(LogLevel.Error, e, "Error applying event in timeline.");
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
            _enabled = Key != null && (settings.Integrations?.ContainsKey(Key) ?? false);
            _timeline.Apply(plugin => plugin.Update(settings, type));
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

            RawEvent beforeResult = _timeline.ApplyPlugins(PluginType.Before, @event);
            RawEvent enrichmentResult = _timeline.ApplyPlugins(PluginType.Enrichment, beforeResult);

            RawEvent destinationResult;
            switch (enrichmentResult)
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
                case PageEvent e:
                    destinationResult = Page(e);
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

            RawEvent afterResult = _timeline.ApplyPlugins(PluginType.After, destinationResult);

            return afterResult;
        }

        public override RawEvent Execute(RawEvent incomingEvent) => Process(incomingEvent);

        internal bool IsDestinationEnabled(RawEvent @event)
        {
            // if event payload has integration marked false then its disabled by customer
            // default to true when missing
            bool customerEnabled = @event?.Integrations?.GetBool(Key, true) ?? true;

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
        public void Apply(Action<Plugin> closure) => Timeline.Apply(closure);

        /// <summary>
        /// Register a plugin to the analytics timeline
        /// </summary>
        /// <param name="plugin">Plugin to be added</param>
        /// <returns>Plugin that added to analytics</returns>
        public Plugin Add(Plugin plugin)
        {
            plugin.Configure(this);
            Timeline.Add(plugin);
            return plugin;
        }

        /// <summary>
        /// Remove a plugin from the analytics timeline
        /// </summary>
        /// <param name="plugin">the plugin to be removed</param>
        public void Remove(Plugin plugin) => Timeline.Remove(plugin);

        /// <summary>
        /// Retrieve the first match of registered plugin. It finds
        ///      1. the first instance of the given class/interface
        ///      2. or the first instance of subclass of the given class/interface
        /// </summary>
        /// <typeparam name="T">Type that implements <see cref="Plugin"/></typeparam>
        /// <returns>The plugin instance of given type T</returns>
        public T Find<T>() where T : Plugin => Timeline.Find<T>();

        /// <summary>
        /// Retrieve all matches of registered plugins. It finds
        ///      1. all instances of the given class/interface
        ///      2. and all instances of subclass of the given class/interface
        /// </summary>
        /// <typeparam name="T">Type that implements <see cref="Plugin"/></typeparam>
        /// <returns>A collection of plugins of the given type T</returns>
        public IEnumerable<T> FindAll<T>() where T : Plugin => Timeline.FindAll<T>();

        /// <summary>
        /// Retrieve the first match of registered destination plugin by key. It finds
        /// </summary>
        /// <param name="destinationKey">the key of <see cref="DestinationPlugin"/></param>
        /// <returns></returns>
        public DestinationPlugin Find(string destinationKey) => Timeline.Find(destinationKey);

        /// <summary>
        /// Manually enable a destination plugin.  This is useful when a given DestinationPlugin doesn't have any Segment tie-ins at all.
        /// This will allow the destination to be processed in the same way within this library.
        /// </summary>
        /// <param name="plugin">Destination plugin that needs to be enabled</param>
        public void ManuallyEnableDestination(DestinationPlugin plugin)
        {
            AnalyticsScope.Launch(AnalyticsDispatcher, async () =>
            {
                await Store.Dispatch<System.AddDestinationToSettingsAction, System>(
                    new System.AddDestinationToSettingsAction(plugin.Key));
            });

            Find(plugin.Key)._enabled = true;
        }
    }
}
