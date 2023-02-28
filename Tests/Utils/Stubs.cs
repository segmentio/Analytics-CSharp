using System.Runtime.Serialization;
using Segment.Analytics;
using Segment.Serialization;

namespace Tests.Utils
{
    class FooBar : ISerializable
    {
        public readonly string foo = "bar";

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
}