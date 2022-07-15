using System;
using System.Collections.Generic;
using Segment.Serialization;

namespace Segment.Analytics
{
    public enum PluginType: int
    {
        Before = 0,
        Enrichment,
        Destination,
        After,
        Utility
    }

    public enum UpdateType
    {
        Initial,
        Refresh
    }

    // Note: We are using abstract class instead of interfaces because version 8 C# does not support generics with
    // nullable types, nor does it support default implementations with generics. This should be revisited once Unity
    // moves to C# v9. 
    public abstract class Plugin
    {
        internal abstract PluginType type { get; }
        internal virtual Analytics analytics { get; set; }

        internal virtual void Configure(Analytics analytics)
        {
            this.analytics = analytics;
        }
        
        internal virtual void Update(Settings settings, UpdateType type) { }
        
        internal virtual RawEvent? Execute(RawEvent incomingEvent)
        {
            return incomingEvent;
        }
        
        public virtual void Shutdown() { }
    }

    public abstract class EventPlugin : Plugin
    {
        public virtual IdentifyEvent? Identify(IdentifyEvent identifyEvent) => identifyEvent;

        public virtual TrackEvent? Track(TrackEvent trackEvent) => trackEvent;

        public virtual GroupEvent? Group(GroupEvent groupEvent) => groupEvent;

        public virtual AliasEvent? Alias(AliasEvent aliasEvent) => aliasEvent;

        public virtual ScreenEvent? Screen(ScreenEvent screenEvent) => screenEvent;

        public virtual void Reset() {}

        public virtual void Flush() {}

        internal override RawEvent? Execute(RawEvent incomingEvent) => incomingEvent switch
        {
            IdentifyEvent e => Identify(e),
            TrackEvent e => Track(e),
            ScreenEvent e => Screen(e),
            AliasEvent e => Alias(e),
            GroupEvent e => Group(e),
            _ => incomingEvent
        };
    }

    public abstract class DestinationPlugin : EventPlugin
    {
        internal override PluginType type => PluginType.Destination;
        public abstract string key { get; }
        
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

        internal override void Configure(Analytics analytics)
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
                Debug.Log($"Exception hit {e.StackTrace}");
                throw;
            }
            
        }

        public RawEvent? Process(RawEvent? @event)
        {
            if (!IsDestinationEnabled(@event))
            {
                return null;
            }
            
            var beforeResult = _timeline.ApplyPlugins(PluginType.Before, @event);
            var enrichmentResult = _timeline.ApplyPlugins(PluginType.Enrichment, beforeResult);

            RawEvent destinationResult = enrichmentResult switch
            {
                AliasEvent e => Alias(e),
                GroupEvent e => Group(e),
                IdentifyEvent e => Identify(e),
                ScreenEvent e => Screen(e),
                TrackEvent e => Track(e),
                _ => enrichmentResult
            };

            var afterResult = _timeline.ApplyPlugins(PluginType.After, destinationResult);

            return afterResult;
        }

        internal override RawEvent? Execute(RawEvent incomingEvent) => Process(incomingEvent);

        internal bool IsDestinationEnabled(RawEvent? @event)
        {
            // if event payload has integration marked false then its disabled by customer
            // default to true when missing
            var customerEnabled = @event?.integrations?.GetBool(key, true) ?? true;
            var hasSettings = analytics.Settings()?.integrations?.ContainsKey(key) ?? false;
            
            return hasSettings && customerEnabled;
        }
    }

    public abstract class UtilityPlugin : Plugin { }
    
    public partial class Analytics
    {   
        public void Apply(Action<Plugin> closure) => timeline.Apply(closure);

        public Plugin Add(Plugin plugin)
        {
            plugin.Configure(this);
            timeline.Add(plugin);
            return plugin;
        }

        public void Remove(Plugin plugin)
        {
            timeline.Remove(plugin);
        }

        public T Find<T>(Type plugin) where T : Plugin => timeline.Find<T>(plugin);

        public IEnumerable<T> FindAll<T>(Type plugin) where T : Plugin => timeline.FindAll<T>(plugin);

        public DestinationPlugin? Find(string destinationKey) => timeline.Find(destinationKey);
    }
}