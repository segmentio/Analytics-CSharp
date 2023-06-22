using System;
using System.Runtime.Serialization;
using Moq;
using Segment.Analytics;
using Segment.Analytics.Utilities;
using Segment.Serialization;

namespace Tests.Utils
{
    class FooBar : ISerializable
    {
        public string foo => "bar";

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("foo", "bar");
        }

        public override bool Equals(object obj)
        {
            if (obj is FooBar fooBar)
            {
                return foo.Equals(fooBar.foo);
            }

            return false;
        }

        public override int GetHashCode()
        {
            return foo.GetHashCode();
        }

        public JsonObject GetJsonObject()
        {
            return new JsonObject
            {
                ["foo"] = foo
            };
        }
    }

    public class StubEventPlugin : EventPlugin
    {
        public override PluginType Type => PluginType.Before;
    }

    public class StubDestinationPlugin : DestinationPlugin
    {
        public override string Key { get; }

        public StubDestinationPlugin(string key)
        {
            Key = key;
        }
    }

    public class MockStorageProvider : IStorageProvider
    {
        public Mock<IStorage> Mock { get; set; }

        public MockStorageProvider(Mock<IStorage> mock) => Mock = mock;

        public IStorage CreateStorage(params object[] parameters)
        {
            return Mock.Object;
        }
    }

    public class MockHttpClientProvider : IHTTPClientProvider
    {
        public Mock<HTTPClient> Mock { get; set; }

        public MockHttpClientProvider(Mock<HTTPClient> mock) => Mock = mock;

        public HTTPClient CreateHTTPClient(string apiKey, string apiHost = null, string cdnHost = null)
        {
            return Mock.Object;
        }
    }
}
