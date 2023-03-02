using Segment.Analytics.Utilities;
using Xunit;

namespace Tests.Utilities
{
    public class InMemoryPrefsTest
    {
        private readonly IPreferences _prefs;

        public InMemoryPrefsTest()
        {
            _prefs = new InMemoryPrefs();
            _prefs.Put("int", 1);
            _prefs.Put("string", "string");
            _prefs.Put("float", 0.1f);
        }

        [Fact]
        public void GetTest()
        {
            string keyNotExists = "keyNotExists";
            int expectedInt = 100;
            float expectedFloat = 0.1f;
            string expectedString = "string";

            Assert.Equal(1, _prefs.GetInt("int"));
            Assert.Equal(0.1f, _prefs.GetFloat("float"));
            Assert.Equal("string", _prefs.GetString("string"));

            Assert.Equal(-1, _prefs.GetInt(keyNotExists));
            Assert.Equal(-1.0f, _prefs.GetFloat(keyNotExists));
            Assert.Null(_prefs.GetString(keyNotExists));

            Assert.Equal(expectedInt, _prefs.GetInt(keyNotExists, expectedInt));
            Assert.Equal(expectedFloat, _prefs.GetFloat(keyNotExists, expectedFloat));
            Assert.Equal(expectedString, _prefs.GetString(keyNotExists, expectedString));
        }

        [Fact]
        public void PutTest()
        {
            _prefs.Put("int", 2);
            _prefs.Put("float", 0.2f);
            _prefs.Put("string", "stringstring");

            Assert.Equal(2, _prefs.GetInt("int"));
            Assert.Equal(0.2f, _prefs.GetFloat("float"));
            Assert.Equal("stringstring", _prefs.GetString("string"));
        }

        [Fact]
        public void ContainsAndRemoveTest()
        {
            Assert.True(_prefs.Contains("int"));
            _prefs.Remove("int");
            Assert.False(_prefs.Contains("int"));
        }
    }

    public class UserPrefsTest
    {
        private readonly IPreferences _prefs;

        public UserPrefsTest()
        {
            _prefs = new UserPrefs("test");
            _prefs.Put("int", 1);
            _prefs.Put("string", "string");
            _prefs.Put("float", 0.1f);
        }

        [Fact]
        public void GetTest()
        {
            string keyNotExists = "keyNotExists";
            int expectedInt = 100;
            float expectedFloat = 0.1f;
            string expectedString = "string";

            Assert.Equal(1, _prefs.GetInt("int"));
            Assert.Equal(0.1f, _prefs.GetFloat("float"));
            Assert.Equal("string", _prefs.GetString("string"));

            Assert.Equal(-1, _prefs.GetInt(keyNotExists));
            Assert.Equal(-1.0f, _prefs.GetFloat(keyNotExists));
            Assert.Null(_prefs.GetString(keyNotExists));

            Assert.Equal(expectedInt, _prefs.GetInt(keyNotExists, expectedInt));
            Assert.Equal(expectedFloat, _prefs.GetFloat(keyNotExists, expectedFloat));
            Assert.Equal(expectedString, _prefs.GetString(keyNotExists, expectedString));
        }

        [Fact]
        public void PutTest()
        {
            _prefs.Put("int", 2);
            _prefs.Put("float", 0.2f);
            _prefs.Put("string", "stringstring");

            Assert.Equal(2, _prefs.GetInt("int"));
            Assert.Equal(0.2f, _prefs.GetFloat("float"));
            Assert.Equal("stringstring", _prefs.GetString("string"));
        }

        [Fact]
        public void ContainsAndRemoveTest()
        {
            Assert.True(_prefs.Contains("int"));
            _prefs.Remove("int");
            Assert.False(_prefs.Contains("int"));
        }

        [Fact]
        public void EditorTest()
        {
            int expectedInt = 100;
            float expectedFloat = 100f;
            string expectedString = "stringstring";
            var userPrefs = (UserPrefs)_prefs;
            Editor editor = userPrefs.Edit()
                .PutInt("int", expectedInt)
                .PutFloat("float", expectedFloat)
                .PutString("string", expectedString);

            Assert.Equal(1, _prefs.GetInt("int"));
            Assert.Equal(0.1f, _prefs.GetFloat("float"));
            Assert.Equal("string", _prefs.GetString("string"));

            editor.Apply();

            Assert.Equal(expectedInt, _prefs.GetInt("int"));
            Assert.Equal(expectedFloat, _prefs.GetFloat("float"));
            Assert.Equal(expectedString, _prefs.GetString("string"));
        }
    }
}
