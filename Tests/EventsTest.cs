using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Moq;
using Segment.Analytics;
using Segment.Analytics.Utilities;
using Segment.Serialization;
using Tests.Utils;
using Xunit;

namespace Tests
{
    public class EventsTest
    {
        private readonly Analytics _analytics;

        private Settings? _settings;

        private readonly Mock<StubEventPlugin> _plugin;

        private readonly Mock<StubAfterEventPlugin> _afterPlugin;

        public EventsTest()
        {
            _settings = JsonUtility.FromJson<Settings?>(
                "{\"integrations\":{\"Segment.io\":{\"apiKey\":\"1vNgUqwJeCHmqgI9S1sOm9UHCyfYqbaQ\"}},\"plan\":{},\"edgeFunction\":{}}");

            var mockHttpClient = new Mock<HTTPClient>(null, null, null);
            mockHttpClient
                .Setup(httpClient => httpClient.Settings())
                .ReturnsAsync(_settings);

            _plugin = new Mock<StubEventPlugin>
            {
                CallBase = true
            };

            _afterPlugin = new Mock<StubAfterEventPlugin> { CallBase = true };

            var config = new Configuration(
                writeKey: "123",
                storageProvider: new DefaultStorageProvider("tests"),
                autoAddSegmentDestination: false,
                useSynchronizeDispatcher: true,
                httpClientProvider: new MockHttpClientProvider(mockHttpClient)
            );
            _analytics = new Analytics(config);
        }

        [Fact]
        public void TestTrack()
        {
            var expected = new JsonObject
            {
                ["foo"] = "bar"
            };
            string expectedEvent = "foo";
            var actual = new List<TrackEvent>();
            _plugin.Setup(o => o.Track(Capture.In(actual)));

            _analytics.Add(_plugin.Object);
            _analytics.Track(expectedEvent, expected);

            Assert.NotEmpty(actual);
            Assert.Equal(expected, actual[0].Properties);
            Assert.Equal(expectedEvent, actual[0].Event);
        }

        [Fact]
        public void TestTrackNoProperties()
        {
            string expectedEvent = "foo";
            var actual = new List<TrackEvent>();
            _plugin.Setup(o => o.Track(Capture.In(actual)));

            _analytics.Add(_plugin.Object);
            _analytics.Track(expectedEvent);

            Assert.NotEmpty(actual);
            Assert.True(actual[0].Properties.Count == 0);
            Assert.Equal(expectedEvent, actual[0].Event);
        }

        [Fact]
        public void TestTrackT()
        {
            var expected = new FooBar();
            string expectedEvent = "foo";
            var actual = new List<TrackEvent>();
            _plugin.Setup(o => o.Track(Capture.In(actual)));

            _analytics.Add(_plugin.Object);
            _analytics.Track(expectedEvent, expected);

            Assert.NotEmpty(actual);
            Assert.Equal(expected.GetJsonObject(), actual[0].Properties);
            Assert.Equal(expectedEvent, actual[0].Event);
        }

        [Fact]
        public void TestTrackTNullProperties()
        {
            string expectedEvent = "foo";
            var actual = new List<TrackEvent>();
            _plugin.Setup(o => o.Track(Capture.In(actual)));

            _analytics.Add(_plugin.Object);
            _analytics.Track(expectedEvent, (FooBar) null);

            Assert.NotEmpty(actual);
            Assert.True(actual[0].Properties.Count == 0);
            Assert.Equal(expectedEvent, actual[0].Event);
        }

        [Fact]
        public void TestTrackTNoProperties()
        {
            string expectedEvent = "foo";
            var actual = new List<TrackEvent>();
            _plugin.Setup(o => o.Track(Capture.In(actual)));

            _analytics.Add(_plugin.Object);
            _analytics.Track(expectedEvent);

            Assert.NotEmpty(actual);
            Assert.True(actual[0].Properties.Count == 0);
            Assert.Equal(expectedEvent, actual[0].Event);
        }

        [Fact]
        public void TestTrackEnrichment()
        {
            string expectedEvent = "foo";
            string expectedAnonymousId = "bar";
            var actual = new List<TrackEvent>();
            _afterPlugin.Setup(o => o.Track(Capture.In(actual)));

            _analytics.Add(_afterPlugin.Object);
            _analytics.Track(expectedEvent, enrichment: @event =>
            {
                @event.AnonymousId = expectedAnonymousId;
                return @event;
            });

            Assert.NotEmpty(actual);
            Assert.True(actual[0].Properties.Count == 0);
            Assert.Equal(expectedEvent, actual[0].Event);
            Assert.Equal(expectedAnonymousId, actual[0].AnonymousId);
        }

        [Fact]
        public void TestIdentify()
        {
            var expected = new JsonObject
            {
                ["foo"] = "bar"
            };
            string expectedUserId = "newUserId";
            var actual = new List<IdentifyEvent>();
            _plugin.Setup(o => o.Identify(Capture.In(actual)));

            _analytics.Add(_plugin.Object);
            _analytics.Identify(expectedUserId, expected);

            string actualUserId = _analytics.UserId();

            Assert.NotEmpty(actual);
            Assert.Equal(expected, actual[0].Traits);
            Assert.Equal(expectedUserId, actualUserId);
        }

        [Fact]
        public void TestIdentifyEnrichment()
        {
            var expected = new JsonObject
            {
                ["foo"] = "bar"
            };
            string expectedUserId = "newUserId";
            var actual = new List<IdentifyEvent>();
            _afterPlugin.Setup(o => o.Identify(Capture.In(actual)));

            _analytics.Add(_afterPlugin.Object);
            _analytics.Identify(expectedUserId, expected, @event =>
            {
                if (@event is IdentifyEvent identifyEvent)
                {
                    identifyEvent.Traits["foo"] = "baz";
                }

                return @event;
            });

            string actualUserId = _analytics.UserId();

            Assert.NotEmpty(actual);
            Assert.Equal(expected, actual[0].Traits);
            Assert.Equal(expectedUserId, actualUserId);
        }

        [Fact]
        public void TestIdentifyNoTraits()
        {
            string expectedUserId = "newUserId";
            var actual = new List<IdentifyEvent>();
            _plugin.Setup(o => o.Identify(Capture.In(actual)));

            _analytics.Add(_plugin.Object);
            _analytics.Identify(expectedUserId);

            string actualUserId = _analytics.UserId();

            Assert.NotEmpty(actual);
            Assert.True(actual[0].Traits.Count == 0);
            Assert.Equal(expectedUserId, actualUserId);
        }

        [Fact]
        public void TestIdentifyNoUserId()
        {
            var expected = new JsonObject
            {
                ["foo"] = "bar"
            };
            var actual = new List<IdentifyEvent>();
            _plugin.Setup(o => o.Identify(Capture.In(actual)));
            string expectedUserId = _analytics.UserId();

            _analytics.Add(_plugin.Object);
            _analytics.Identify(expected);

            string actualUserId = _analytics.UserId();

            Assert.NotEmpty(actual);
            Assert.Equal(expected, actual[0].Traits);
            Assert.Equal(expectedUserId, actualUserId);
        }

        [Fact]
        public void TestIdentifyNoUserIdNullTraits()
        {
            var actual = new List<IdentifyEvent>();
            _plugin.Setup(o => o.Identify(Capture.In(actual)));
            string expectedUserId = _analytics.UserId();

            _analytics.Add(_plugin.Object);
            _analytics.Identify((JsonObject) null);

            string actualUserId = _analytics.UserId();

            Assert.NotEmpty(actual);
            Assert.True(actual[0].Traits.Count == 0);
            Assert.Equal(expectedUserId, actualUserId);
        }

        [Fact]
        public void TestIdentifyT()
        {
            var expected = new FooBar();
            string expectedUserId = "newUserId";
            var actual = new List<IdentifyEvent>();
            _plugin.Setup(o => o.Identify(Capture.In(actual)));

            _analytics.Add(_plugin.Object);
            _analytics.Identify(expectedUserId, expected);

            string actualUserId = _analytics.UserId();

            Assert.NotEmpty(actual);
            Assert.Equal(expected.GetJsonObject(), actual[0].Traits);
            Assert.Equal(expectedUserId, actualUserId);
        }

        [Fact]
        public void TestIdentifyTNullTraits()
        {
            string expectedUserId = "newUserId";
            var actual = new List<IdentifyEvent>();
            _plugin.Setup(o => o.Identify(Capture.In(actual)));

            _analytics.Add(_plugin.Object);
            _analytics.Identify(expectedUserId, (FooBar) null);

            string actualUserId = _analytics.UserId();

            Assert.NotEmpty(actual);
            Assert.True(actual[0].Traits.Count == 0);
            Assert.Equal(expectedUserId, actualUserId);
        }

        [Fact]
        public void TestIdentifyTNoTraits()
        {
            string expectedUserId = "newUserId";
            var actual = new List<IdentifyEvent>();
            _plugin.Setup(o => o.Identify(Capture.In(actual)));

            _analytics.Add(_plugin.Object);
            _analytics.Identify(expectedUserId, (FooBar) null);
            string actualUserId = _analytics.UserId();

            Assert.NotEmpty(actual);
            Assert.True(actual[0].Traits.Count == 0);
            Assert.Equal(expectedUserId, actualUserId);
        }

        [Fact]
        public void TestIdentifyTNoUserId()
        {
            var expected = new FooBar();
            var actual = new List<IdentifyEvent>();
            _plugin.Setup(o => o.Identify(Capture.In(actual)));
            string expectedUserId = _analytics.UserId();

            _analytics.Add(_plugin.Object);
            _analytics.Identify(expected);
            string actualUserId = _analytics.UserId();

            Assert.NotEmpty(actual);
            Assert.Equal(expected.GetJsonObject(), actual[0].Traits);
            Assert.Equal(expectedUserId, actualUserId);
        }

        [Fact]
        public void TestIdentifyTNoUserIdNullTraits()
        {
            var actual = new List<IdentifyEvent>();
            _plugin.Setup(o => o.Identify(Capture.In(actual)));
            string expectedUserId = _analytics.UserId();

            _analytics.Add(_plugin.Object);
            _analytics.Identify((FooBar) null);
            string actualUserId = _analytics.UserId();

            Assert.NotEmpty(actual);
            Assert.True(actual[0].Traits.Count == 0);
            Assert.Equal(expectedUserId, actualUserId);
        }

        [Fact]
        public void TestIdentifyReload()
        {
            string expectedUserId = "newUserId";
            var actualIdentify = new List<IdentifyEvent>();
            var actualTrack = new List<TrackEvent>();
            _plugin.Setup(o => o.Identify(Capture.In(actualIdentify)));
            _plugin.Setup(o => o.Track(Capture.In(actualTrack)));

            _analytics.Add(_plugin.Object);
            _analytics.Identify(expectedUserId);

            _analytics.Identify(userId:null, traits:null);

            var userIdEmpty = UserInfo.DefaultState(_analytics.Storage);
            Assert.Null(userIdEmpty._userId);
        }

        [Fact]
        public void TestScreen()
        {
            var expected = new JsonObject
            {
                ["foo"] = "bar"
            };
            string expectedTitle = "foo";
            string expectedCategory = "bar";
            var actual = new List<ScreenEvent>();
            _plugin.Setup(o => o.Screen(Capture.In(actual)));

            _analytics.Add(_plugin.Object);
            _analytics.Screen(expectedTitle, expected, expectedCategory);

            Assert.NotEmpty(actual);
            Assert.Equal(expected, actual[0].Properties);
            Assert.Equal(expectedTitle, actual[0].Name);
            Assert.Equal(expectedCategory, actual[0].Category);
        }

        [Fact]
        public void TestScreenEnrichment()
        {
            var expected = new JsonObject
            {
                ["foo"] = "bar"
            };
            string expectedTitle = "foo";
            string expectedCategory = "bar";
            var actual = new List<ScreenEvent>();
            _afterPlugin.Setup(o => o.Screen(Capture.In(actual)));

            _analytics.Add(_afterPlugin.Object);
            _analytics.Screen(expectedTitle, expected, expectedCategory, @event =>
            {
                if (@event is ScreenEvent screenEvent)
                {
                    screenEvent.Properties["foo"] = "baz";
                }

                return @event;
            });

            Assert.NotEmpty(actual);
            Assert.Equal(expected, actual[0].Properties);
            Assert.Equal(expectedTitle, actual[0].Name);
            Assert.Equal(expectedCategory, actual[0].Category);
        }

        [Fact]
        public void TestScreenWithNulls()
        {
            var actual = new List<ScreenEvent>();
            _plugin.Setup(o => o.Screen(Capture.In(actual)));

            _analytics.Add(_plugin.Object);
            _analytics.Screen(null, null, null);

            Assert.NotEmpty(actual);
            Assert.True(actual[0].Properties.Count == 0);
            Assert.Null(actual[0].Name);
            Assert.Null(actual[0].Category);
        }

        [Fact]
        public void TestScreenT()
        {
            var expected = new FooBar();
            string expectedTitle = "foo";
            string expectedCategory = "bar";
            var actual = new List<ScreenEvent>();
            _plugin.Setup(o => o.Screen(Capture.In(actual)));

            _analytics.Add(_plugin.Object);
            _analytics.Screen(expectedTitle, expected, expectedCategory);

            Assert.NotEmpty(actual);
            Assert.Equal(expected.GetJsonObject(), actual[0].Properties);
            Assert.Equal(expectedTitle, actual[0].Name);
            Assert.Equal(expectedCategory, actual[0].Category);
        }

        [Fact]
        public void TestScreenTWithNulls()
        {
            var actual = new List<ScreenEvent>();
            _plugin.Setup(o => o.Screen(Capture.In(actual)));

            _analytics.Add(_plugin.Object);
            _analytics.Screen(null, (FooBar) null, null);

            Assert.NotEmpty(actual);
            Assert.True(actual[0].Properties.Count == 0);
            Assert.Null(actual[0].Name);
            Assert.Null(actual[0].Category);
        }

        [Fact]
        public void TestPage()
        {
            var expected = new JsonObject
            {
                ["foo"] = "bar"
            };
            string expectedTitle = "foo";
            string expectedCategory = "bar";
            var actual = new List<PageEvent>();
            _plugin.Setup(o => o.Page(Capture.In(actual)));

            _analytics.Add(_plugin.Object);
            _analytics.Page(expectedTitle, expected, expectedCategory);

            Assert.NotEmpty(actual);
            Assert.Equal(expected, actual[0].Properties);
            Assert.Equal(expectedTitle, actual[0].Name);
            Assert.Equal(expectedCategory, actual[0].Category);
            Assert.Equal("page", actual[0].Type);
        }

        [Fact]
        public void TestPageEnrichment()
        {
            var expected = new JsonObject
            {
                ["foo"] = "bar"
            };
            string expectedTitle = "foo";
            string expectedCategory = "bar";
            var actual = new List<PageEvent>();
            _afterPlugin.Setup(o => o.Page(Capture.In(actual)));

            _analytics.Add(_afterPlugin.Object);
            _analytics.Page(expectedTitle, expected, expectedCategory, @event =>
            {
                if (@event is PageEvent pageEvent)
                {
                    pageEvent.Properties["foo"] = "baz";
                }

                return @event;
            });

            Assert.NotEmpty(actual);
            Assert.Equal(expected, actual[0].Properties);
            Assert.Equal(expectedTitle, actual[0].Name);
            Assert.Equal(expectedCategory, actual[0].Category);
            Assert.Equal("page", actual[0].Type);
        }

        [Fact]
        public void TestPageWithNulls()
        {
            var actual = new List<PageEvent>();
            _plugin.Setup(o => o.Page(Capture.In(actual)));

            _analytics.Add(_plugin.Object);
            _analytics.Page(null, null, null);

            Assert.NotEmpty(actual);
            Assert.True(actual[0].Properties.Count == 0);
            Assert.Null(actual[0].Name);
            Assert.Null(actual[0].Category);
            Assert.Equal("page", actual[0].Type);
        }

        [Fact]
        public void TestPageT()
        {
            var expected = new FooBar();
            string expectedTitle = "foo";
            string expectedCategory = "bar";
            var actual = new List<PageEvent>();
            _plugin.Setup(o => o.Page(Capture.In(actual)));

            _analytics.Add(_plugin.Object);
            _analytics.Page(expectedTitle, expected, expectedCategory);

            Assert.NotEmpty(actual);
            Assert.Equal(expected.GetJsonObject(), actual[0].Properties);
            Assert.Equal(expectedTitle, actual[0].Name);
            Assert.Equal(expectedCategory, actual[0].Category);
            Assert.Equal("page", actual[0].Type);
        }

        [Fact]
        public void TestPageTWithNulls()
        {
            var actual = new List<PageEvent>();
            _plugin.Setup(o => o.Page(Capture.In(actual)));

            _analytics.Add(_plugin.Object);
            _analytics.Page(null, (FooBar) null, null);

            Assert.NotEmpty(actual);
            Assert.True(actual[0].Properties.Count == 0);
            Assert.Null(actual[0].Name);
            Assert.Null(actual[0].Category);
            Assert.Equal("page", actual[0].Type);
        }

        [Fact]
        public void TestGroup()
        {
            var expected = new JsonObject
            {
                ["foo"] = "bar"
            };
            string expectedGroupId = "foo";
            var actual = new List<GroupEvent>();
            _plugin.Setup(o => o.Group(Capture.In(actual)));

            _analytics.Add(_plugin.Object);
            _analytics.Group(expectedGroupId, expected);

            Assert.NotEmpty(actual);
            Assert.Equal(expected, actual[0].Traits);
            Assert.Equal(expectedGroupId, actual[0].GroupId);
        }

        [Fact]
        public void TestGroupEnrichment()
        {
            var expected = new JsonObject
            {
                ["foo"] = "bar"
            };
            string expectedGroupId = "foo";
            var actual = new List<GroupEvent>();
            _afterPlugin.Setup(o => o.Group(Capture.In(actual)));

            _analytics.Add(_afterPlugin.Object);
            _analytics.Group(expectedGroupId, expected, @event =>
            {
                if (@event is GroupEvent groupEvent)
                {
                    groupEvent.Traits["foo"] = "baz";
                }

                return @event;
            });

            Assert.NotEmpty(actual);
            Assert.Equal(expected, actual[0].Traits);
            Assert.Equal(expectedGroupId, actual[0].GroupId);
        }

        [Fact]
        public void TestGroupNoProperties()
        {
            string expectedGroupId = "foo";
            var actual = new List<GroupEvent>();
            _plugin.Setup(o => o.Group(Capture.In(actual)));

            _analytics.Add(_plugin.Object);
            _analytics.Group(expectedGroupId);

            Assert.NotEmpty(actual);
            Assert.True(actual[0].Traits.Count == 0);
            Assert.Equal(expectedGroupId, actual[0].GroupId);
        }

        [Fact]
        public void TestGroupT()
        {
            var expected = new FooBar();
            string expectedGroupId = "foo";
            var actual = new List<GroupEvent>();
            _plugin.Setup(o => o.Group(Capture.In(actual)));

            _analytics.Add(_plugin.Object);
            _analytics.Group(expectedGroupId, expected);

            Assert.NotEmpty(actual);
            Assert.Equal(expected.GetJsonObject(), actual[0].Traits);
            Assert.Equal(expectedGroupId, actual[0].GroupId);
        }

        [Fact]
        public void TestGroupTNullProperties()
        {
            string expectedGroupId = "foo";
            var actual = new List<GroupEvent>();
            _plugin.Setup(o => o.Group(Capture.In(actual)));

            _analytics.Add(_plugin.Object);
            _analytics.Group(expectedGroupId, (FooBar) null);

            Assert.NotEmpty(actual);
            Assert.True(actual[0].Traits.Count == 0);
            Assert.Equal(expectedGroupId, actual[0].GroupId);
        }

        [Fact]
        public void TestAlias()
        {
            string expectedPrevious = "foo";
            string expected = "bar";
            var actual = new List<AliasEvent>();
            _plugin.Setup(o => o.Alias(Capture.In(actual)));

            _analytics.Add(_plugin.Object);
            _analytics.Identify(expectedPrevious);
            _analytics.Alias(expected);

            Assert.NotEmpty(actual);
            Assert.Equal(expectedPrevious, actual[0].PreviousId);
            Assert.Equal(expected, actual[0].UserId);
        }

        [Fact]
        public void TestAliasEnrichment()
        {
            string expectedPrevious = "foo";
            string expected = "bar";
            var actual = new List<AliasEvent>();
            _afterPlugin.Setup(o => o.Alias(Capture.In(actual)));

            _analytics.Add(_afterPlugin.Object);
            _analytics.Identify(expectedPrevious);
            _analytics.Alias(expected, @event =>
            {
                if (@event is AliasEvent aliasEvent)
                {
                    aliasEvent.AnonymousId = "test";
                }

                return @event;
            });

            Assert.NotEmpty(actual);
            Assert.Equal(expectedPrevious, actual[0].PreviousId);
            Assert.Equal(expected, actual[0].UserId);
            Assert.Equal("test", actual[0].AnonymousId);
        }
    }

    public class DelayedEventsTest
    {
        private readonly Analytics _analytics;

        private Settings? _settings;

        private readonly Mock<StubEventPlugin> _plugin;

        private readonly Mock<StubAfterEventPlugin> _afterPlugin;

        private readonly SemaphoreSlim _httpSemaphore;
        private readonly SemaphoreSlim _assertSemaphore;
        private readonly List<RawEvent> _actual;

        public DelayedEventsTest()
        {
            _httpSemaphore = new SemaphoreSlim(0);
            _assertSemaphore = new SemaphoreSlim(0);
            _settings = JsonUtility.FromJson<Settings?>(
                "{\"integrations\":{\"Segment.io\":{\"apiKey\":\"1vNgUqwJeCHmqgI9S1sOm9UHCyfYqbaQ\"}},\"plan\":{},\"edgeFunction\":{}}");

            var mockHttpClient = new Mock<HTTPClient>(null, null, null);
            mockHttpClient
                .Setup(httpClient => httpClient.Settings())
                .Returns(async () =>
                {
                    // suspend http calls until we tracked events
                    // this will force events get into startup queue
                    await _httpSemaphore.WaitAsync();
                    return _settings;
                });

            _plugin = new Mock<StubEventPlugin>
            {
                CallBase = true
            };

            _afterPlugin = new Mock<StubAfterEventPlugin> { CallBase = true };
            _actual = new List<RawEvent>();
            _afterPlugin.Setup(o => o.Execute(Capture.In(_actual)))
                .Returns((RawEvent e) =>
                {
                    // since this is an after plugin, when its execute function is called,
                    // it is guaranteed that the enrichment closure has been called.
                    // so we can release the semaphore on assertions.
                    _assertSemaphore.Release();
                    return e;
                });

            var config = new Configuration(
                writeKey: "123",
                storageProvider: new DefaultStorageProvider("tests"),
                autoAddSegmentDestination: false,
                useSynchronizeDispatcher: false,    // we need async analytics to buildup events on start queue
                httpClientProvider: new MockHttpClientProvider(mockHttpClient)
            );
            _analytics = new Analytics(config);
        }

        [Fact]
        public void TestTrackEnrichment()
        {
            string expectedEvent = "foo";
            string expectedAnonymousId = "bar";

            _analytics.Add(_afterPlugin.Object);
            _analytics.Track(expectedEvent, enrichment: @event =>
            {
                @event.AnonymousId = expectedAnonymousId;
                return @event;
            });

            // now we have tracked event, i.e. event added to startup queue
            // release the semaphore put on http client, so we startup queue will replay the events
            _httpSemaphore.Release();
            // now we need to wait for events being fully replayed before making assertions
            _assertSemaphore.Wait();

            Assert.NotEmpty(_actual);
            Assert.IsType<TrackEvent>(_actual[0]);
            var actual = _actual[0] as TrackEvent;
            Debug.Assert(actual != null, nameof(actual) + " != null");
            Assert.True(actual.Properties.Count == 0);
            Assert.Equal(expectedEvent, actual.Event);
            Assert.Equal(expectedAnonymousId, actual.AnonymousId);
        }

        [Fact]
        public void TestIdentifyEnrichment()
        {
            var expected = new JsonObject
            {
                ["foo"] = "bar"
            };
            string expectedUserId = "newUserId";

            _analytics.Add(_afterPlugin.Object);
            _analytics.Identify(expectedUserId, expected, @event =>
            {
                if (@event is IdentifyEvent identifyEvent)
                {
                    identifyEvent.Traits["foo"] = "baz";
                }

                return @event;
            });

            // now we have tracked event, i.e. event added to startup queue
            // release the semaphore put on http client, so we startup queue will replay the events
            _httpSemaphore.Release();
            // now we need to wait for events being fully replayed before making assertions
            _assertSemaphore.Wait();

            string actualUserId = _analytics.UserId();

            Assert.NotEmpty(_actual);
            var actual = _actual[0] as IdentifyEvent;
            Debug.Assert(actual != null, nameof(actual) + " != null");
            Assert.Equal(expected, actual.Traits);
            Assert.Equal(expectedUserId, actualUserId);
        }

        [Fact]
        public void TestScreenEnrichment()
        {
            var expected = new JsonObject
            {
                ["foo"] = "bar"
            };
            string expectedTitle = "foo";
            string expectedCategory = "bar";

            _analytics.Add(_afterPlugin.Object);
            _analytics.Screen(expectedTitle, expected, expectedCategory, @event =>
            {
                if (@event is ScreenEvent screenEvent)
                {
                    screenEvent.Properties["foo"] = "baz";
                }

                return @event;
            });

            // now we have tracked event, i.e. event added to startup queue
            // release the semaphore put on http client, so we startup queue will replay the events
            _httpSemaphore.Release();
            // now we need to wait for events being fully replayed before making assertions
            _assertSemaphore.Wait();

            Assert.NotEmpty(_actual);
            var actual = _actual[0] as ScreenEvent;
            Debug.Assert(actual != null, nameof(actual) + " != null");
            Assert.Equal(expected, actual.Properties);
            Assert.Equal(expectedTitle, actual.Name);
            Assert.Equal(expectedCategory, actual.Category);
        }

        [Fact]
        public void TestPageEnrichment()
        {
            var expected = new JsonObject
            {
                ["foo"] = "bar"
            };
            string expectedTitle = "foo";
            string expectedCategory = "bar";

            _analytics.Add(_afterPlugin.Object);
            _analytics.Page(expectedTitle, expected, expectedCategory, @event =>
            {
                if (@event is PageEvent pageEvent)
                {
                    pageEvent.Properties["foo"] = "baz";
                }

                return @event;
            });

            // now we have tracked event, i.e. event added to startup queue
            // release the semaphore put on http client, so we startup queue will replay the events
            _httpSemaphore.Release();
            // now we need to wait for events being fully replayed before making assertions
            _assertSemaphore.Wait();

            Assert.NotEmpty(_actual);
            var actual = _actual[0] as PageEvent;
            Debug.Assert(actual != null, nameof(actual) + " != null");
            Assert.Equal(expected, actual.Properties);
            Assert.Equal(expectedTitle, actual.Name);
            Assert.Equal(expectedCategory, actual.Category);
            Assert.Equal("page", actual.Type);
        }

        [Fact]
        public void TestGroupEnrichment()
        {
            var expected = new JsonObject
            {
                ["foo"] = "bar"
            };
            string expectedGroupId = "foo";

            _analytics.Add(_afterPlugin.Object);
            _analytics.Group(expectedGroupId, expected, @event =>
            {
                if (@event is GroupEvent groupEvent)
                {
                    groupEvent.Traits["foo"] = "baz";
                }

                return @event;
            });

            // now we have tracked event, i.e. event added to startup queue
            // release the semaphore put on http client, so we startup queue will replay the events
            _httpSemaphore.Release();
            // now we need to wait for events being fully replayed before making assertions
            _assertSemaphore.Wait();

            Assert.NotEmpty(_actual);
            var actual = _actual[0] as GroupEvent;
            Debug.Assert(actual != null, nameof(actual) + " != null");
            Assert.Equal(expected, actual.Traits);
            Assert.Equal(expectedGroupId, actual.GroupId);
        }

        [Fact]
        public void TestAliasEnrichment()
        {
            string expected = "bar";

            _analytics.Add(_afterPlugin.Object);
            _analytics.Alias(expected, @event =>
            {
                if (@event is AliasEvent aliasEvent)
                {
                    aliasEvent.AnonymousId = "test";
                }

                return @event;
            });

            // now we have tracked event, i.e. event added to startup queue
            // release the semaphore put on http client, so we startup queue will replay the events
            _httpSemaphore.Release();
            // now we need to wait for events being fully replayed before making assertions
            _assertSemaphore.Wait();

            Assert.NotEmpty(_actual);
            var actual = _actual.Find(o => o is AliasEvent) as AliasEvent;
            Debug.Assert(actual != null, nameof(actual) + " != null");
            Assert.Equal(expected, actual.UserId);
            Assert.Equal("test", actual.AnonymousId);
        }
    }
}
