# Analytics-CSharp

The hassle-free way to add Segment analytics to your .Net app (Unity/Xamarin/.Net). Analytics helps you measure your users, product, and business. It unlocks insights into your app's funnel, core business metrics, and whether you have product-market fit.

Analytics-CSharp is supported across the following platforms:
* net/.net core/.net framework
* mono
* universal windows platform
* xamarin
    * ios
    * mac
    * android
* unity
    * ios
    * android
    * pc, mac, linux

**NOTE: This project is currently only available in Pilot phase and is covered by Segment's [First Access & Beta Preview Terms](https://segment.com/legal/first-access-beta-preview/).  We encourage you to try out this new library. Please provide feedback via Github issues/PRs, and feel free to submit pull requests.**

## Getting Started

To get started with the Analytics-CSharp library:

1. Create a Source in Segment.
   1. Go to **Connections > Sources > Add Source**.
   2. Search for **Xamarin** or **Unity** or **.NET** and click **Add source**.
2. Add the Analytics dependency to your project.
    ```
    dotnet add package Segment.Analytics.CSharp --version <LATEST_VERSION>
    ```
   **Analytics-CSharp** is distributed via NuGet. Check other installation options [here](https://www.nuget.org/packages/Segment.Analytics.CSharp/).
3. Initialize and configure the client.

    ```c#
        // NOTE: persistentDataPath is different on different platform
        // for Xamarin use: Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        // for Unity use: Application.persistentDataPath
        var configuration = new Configuration("<YOUR WRITE KEY>",
                persistentDataPath: "<PATH TO STORE DATA>",
                flushAt: 1,
                flushInterval: 10);
        var analytics = new Analytics(configuration);
    ```

   <br>These are the options you can apply to configure the client:

|  Option Name                    | Description                                                                                                                                                                                                                                                                   |
| --------------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
 | `writeKey` *required*           | This is your Segment write key.                                                                                                                                                                                                                                               |
 | `apiHost`                       | Default set to `api.segment.io/v1`. <br> This sets a default API Host to which Segment sends events.                                                                                                                                                                          |
 | `autoAddSegmentDestination`     | Default set to `true`. <br> This automatically adds the Segment Destination plugin. You can set this to `false` if you want to manually add the Segment Destination plugin.                                                                                                   |
 | `defaultSettings`               | Default set to `{}`. <br> The settings object used as fallback in case of network failure.                                                                                                                                                                                    |
 | `flushAt`                       | Default set to `20`. <br> The count of events at which Segment flushes events.                                                                                                                                                                                                |
 | `flushInterval`                 | Default set to `30` (seconds). <br> The interval in seconds at which Segment flushes events.                                                                                                                                                                                  |
| `exceptionHandler`                 | Set a an exception handler to handle errors happened in async methods within the analytics scope                                                                                                                                                                              |
 | `StorageProvider`| Set how you want your data to be stored. <br> `DefaultStorageProvider` is used by default which stores data to local storage. `InMemoryStorageProvider` is also provided in the library. You can also write your own storage solution by implementing `IStorageProvider` and `IStorage` |

## Tracking Methods

Once you've installed the mobile or server Analytics-CSharp library, you can start collecting data through Segment's tracking methods:
- [Identify](#identify)
- [Track](#track)
- [Screen](#screen)
- [Group](#group)

> info ""
> For any of the different methods described, you can replace the properties and traits in the code samples with variables that represent the data collected.

### Identify
The [Identify](/docs/connections/spec/identify/) method lets you tie a user to their actions and record traits about them. This includes a unique user ID and any optional traits you know about them like their email, name, address. The traits option can include any information you want to tie to the user. When using any of the reserved traits, be sure the information reflects the name of the trait. For example, `email`  should always be a string of the user's email address.

```c#
analytics.Identify("user-123", new JsonObject {
    ["username"] = "MisterWhiskers",
    ["email"] = "hello@test.com",
    ["plan"] = "premium"
});
```

### Track
The [Track](/docs/connections/spec/track/) method lets you record the actions your users perform. Every action triggers an event, which also has associated properties that the track method records.

```c#
analytics.Track("View Product", new JsonObject {
    ["productId"] = 123,
    ["productName"] = "Striped trousers"
});
```

### Screen
The [Screen](/docs/connections/spec/screen/) method lets you record whenever a user sees a screen in your mobile app, along with optional extra information about the page being viewed.

You'll want to record a screen event whenever the user opens a screen in your app. This could be a view, fragment, dialog or activity depending on your app.

Not all integrations support screen, so when it's not supported explicitly, the screen method tracks as an event with the same parameters.

```c#
analytics.Screen("ScreenName", new JsonObject {
    ["productSlug"] = "example-product-123"
});
```

### Group
The [Group](/docs/connections/spec/group/) method lets you associate an individual user with a group— whether it's a company, organization, account, project, or team. This includes a unique group identifier and any additional group traits you may have, like company name, industry, number of employees. You can include any information you want to associate with the group in the traits option. When using any of the reserved group traits, be sure the information reflects the name of the trait. For example, email should always be a string of the user's email address.

```c#
analytics.Group("user-123", new JsonObject {
    ["username"] = "MisterWhiskers",
    ["email"] = "hello@test.com",
    ["plan"] = "premium"
});
```

## Plugin Architecture
Segment's plugin architecture enables you to modify and augment how the analytics client works. From modifying event payloads to changing analytics functionality, plugins help to speed up the process of getting things done.

Plugins are run through a timeline, which executes in order of insertion based on their entry types. Segment has these 5 entry types:

| Type          | Details                                                                                        |
|---------------| ---------------------------------------------------------------------------------------------- |
| `Before`      | Executes before event processing begins.                                                       |
| `Enrichment`  | Executes as the first level of event processing.                                               |
| `Destination` | Executes as events begin to pass off to destinations.                                          |
| `After`       | Executes after all event processing completes. You can use this to perform cleanup operations. |
| `Utility`     | Executes only with manual calls such as Logging.                                               |

### Fundamentals
There are 3 basic types of plugins that you can use as a foundation for modifying functionality. They are: [`Plugin`](#plugin), [`EventPlugin`](#eventplugin), and [`DestinationPlugin`](#destinationplugin).

#### Plugin
`Plugin` acts on any event payload going through the timeline.

For example, if you want to add something to the context object of any event payload as an enrichment:

```c#
class SomePlugin : Plugin
{
    public override PluginType type => PluginType.Enrichment;

    public override RawEvent Execute(RawEvent incomingEvent)
    {
        incomingEvent.context["foo"] = "bar";
        return incomingEvent;
    }
}
```

#### EventPlugin
`EventPlugin` is a plugin interface that acts on specific event types. You can choose the event types by only overriding the event functions you want.

For example, if you only want to act on `track` & `identify` events:

```c#
class SomePlugin : EventPlugin
{
    public override PluginType type => PluginType.Enrichment;

    public override IdentifyEvent Identify(IdentifyEvent identifyEvent)
    {
        // code to modify identify event
        return identifyEvent;
    }

    public override TrackEvent Track(TrackEvent trackEvent)
    {
        // code to modify track event
        return trackEvent;
    }
}
```

#### DestinationPlugin
The `DestinationPlugin` interface is commonly used for device-mode destinations. This plugin contains an internal timeline that follows the same process as the analytics timeline, enabling you to modify and augment how events reach a particular destination.

For example, if you want to implement a device-mode destination plugin for Amplitude, you can use this:

```c#
class AmplitudePlugin : DestinationPlugin
{
    public override string key =>
        "Amplitude"; // This is the name of the destination plugin, it is used to retrieve settings internally

    private Amplitude amplitudeSDK: // This is an instance of the partner SDK

    public AmplitudePlugin()
    {
        amplitudeSDK = Amplitude.instance;
        amplitudeSDK.initialize(applicationContext, "API_KEY");
    }

    /*
    * Implementing this function allows this plugin to hook into any track events
    * coming into the analytics timeline
    */
    public override TrackEvent Track(TrackEvent trackEvent)
    {
        amplitudeSDK.logEvent(trackEvent.@event);
        return trackEvent;
    }
}
```

### Advanced concepts

- `configure(Analytics)`: Use this function to setup your plugin. This implicitly calls once the plugin registers.
- `update(Settings)`: Use this function to react to any settings updates. This implicitly calls when settings update. You can force a settings update by calling `analytics.checkSettings()`.
- `DestinationPlugin` timeline: The destination plugin contains an internal timeline that follows the same process as the analytics timeline, enabling you to modify/augment how events reach the particular destination. For example if you only wanted to add a context key when sending an event to `Amplitude`:

```c#
class AmplitudeEnrichment : Plugin
{
    public override PluginType type => PluginType.Enrichment;

    public override RawEvent Execute(RawEvent incomingEvent)
    {
        incomingEvent.context["foo"] = "bar";
        return incomingEvent;
    }
}


var amplitudePlugin = new AmplitudePlugin(); // add amplitudePlugin to the analytics client
analytics.Add(amplitudePlugin);
amplitudePlugin.Add(new AmplitudeEnrichment()); // add enrichment plugin to amplitude timeline
```

## Adding a plugin
Adding plugins enable you to modify your analytics implementation to best fit your needs. You can add a plugin using this:

```c#
var yourPlugin = new SomePlugin()
analytics.Add(yourPlugin)
```

Though you can add plugins anywhere in your code, it's best to implement your plugin when you configure the client.

Here's an example of adding a plugin to the context object of any event payload as an enrichment:

```c#
class SomePlugin : Plugin
{
    public override PluginType type => PluginType.Enrichment;

    public override RawEvent Execute(RawEvent incomingEvent)
    {
        incomingEvent.context["foo"] = "bar";
        return incomingEvent;
    }
}

var yourPlugin = new SomePlugin()
analytics.Add(yourPlugin)
```

### Example projects using Analytics-CSharp
See how different platforms and languages use Analytics-CSharp in different [example projects](https://github.com/segmentio/Analytics-CSharp/tree/main/Samples).

## Utility Methods
The Analytics-CSharp utility methods help you work with plugins from the analytics timeline. They include:
- [Add](#add)
- [Find](#find)
- [Remove](#remove)
- [Reset](#reset)

There's also the [Flush](#flush) method to help you manage the current queue of events.

### Add
The Add method lets you add a plugin to the analytics timeline.

```c#
class SomePlugin : Plugin
{
    public override PluginType type => PluginType.Enrichment;

    public override RawEvent Execute(RawEvent incomingEvent)
    {
        incomingEvent.context["foo"] = "bar";
        return incomingEvent;
    }
}

var somePlugin = new SomePlugin();
analytics.Add(somePlugin);
```

### Find
The Find method lets you find a registered plugin from the analytics timeline.

```c#
var plugin = analytics.Find<SomePlugin>();
```

### Remove
The Remove methods lets you remove a registered plugin from the analytics timeline.

```c#
analytics.remove(somePlugin);
```

### Flush
The Flush method lets you force flush the current queue of events regardless of what the `flushAt` and `flushInterval` is set to.

```c#
analytics.Flush();
```

### Reset
The `reset` method clears the SDK’s internal stores for the current user and group. This is useful for apps where users log in and out with different identities on the same device over time.

```c#
analytics.Reset()
```

## Compatibility
This library targets `.NET Standard 2.0`. Checkout [here](https://docs.microsoft.com/en-us/dotnet/standard/net-standard?tabs=net-standard-2-0) for compatible platforms.

## Changelog
[View the Analytics-CSharp changelog on GitHub](https://github.com/segmentio/analytics-csharp/releases).


## Contributing

See the [contributing guide](CONTRIBUTING.md) to learn how to contribute to the repository and the development workflow.

## Integrating with Segment

Interested in integrating your service with us? Check out our [Partners page](https://segment.com/partners/) for more details.

## Code of Conduct

Before contributing, please also see our [code of conduct](CODE_OF_CONDUCT.md).

## License
```
MIT License

Copyright (c) 2021 Segment

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```
