using System;
using System.Threading.Tasks;
using Moq;
using Segment.Analytics;
using Segment.Analytics.Utilities;
using Segment.Serialization;
using Segment.Sovran;
using Xunit;

namespace Tests
{
    public class SystemTest
    {
        private Store _store;

        private Settings _settings;

        private Configuration _configuration;

        private Mock<IStorage> _storage;

        public SystemTest()
        {
            _store = new Store(true);
            _settings = new Settings
            {
                integrations = new JsonObject
                {
                    ["foo"] = "bar"
                }
            };
            _configuration = new Configuration(
                writeKey: "123",
                autoAddSegmentDestination: false,
                userSynchronizeDispatcher: true,
                defaultSettings: _settings
            );
            _storage = new Mock<IStorage>();
        }

        [Fact]
        public void TestSystemDefaultState()
        {
            var settingsStr =
                "{\"integrations\":{\"Segment.io\":{\"apiKey\":\"1vNgUqwJeCHmqgI9S1sOm9UHCyfYqbaQ\"}},\"plan\":{},\"edgeFunction\":{}}";
            var settings = JsonUtility.FromJson<Settings>(settingsStr);
            _storage
                .Setup(o => o.Read(It.IsAny<StorageConstants>()))
                .Returns(settingsStr);

            var actual = Segment.Analytics.System.DefaultState(_configuration, _storage.Object);
            
            Assert.Equal(_configuration, actual.configuration);
            Assert.Equal(settings.integrations.ToString(), actual.settings.integrations.ToString());
            Assert.False(actual.running);
        }
        
        [Fact]
        public void TestSystemDefaultStateException()
        {
            _storage
                .Setup(o => o.Read(It.IsAny<StorageConstants>()))
                .Throws<Exception>();

            var actual = Segment.Analytics.System.DefaultState(_configuration, _storage.Object);
            
            Assert.Equal(_configuration, actual.configuration);
            Assert.Equal(_settings.integrations.ToString(), actual.settings.integrations.ToString());
            Assert.False(actual.running);
        }

        [Fact]
        public async Task TestUpdateSettingsAction()
        {
            await _store.Provide(Segment.Analytics.System.DefaultState(_configuration, _storage.Object));
            await _store.Dispatch<Segment.Analytics.System.UpdateSettingsAction, Segment.Analytics.System>(
                new Segment.Analytics.System.UpdateSettingsAction(_settings));

            var actual = await _store.CurrentState<Segment.Analytics.System>();
            
            Assert.Equal(_settings.integrations.ToString(), actual.settings.integrations.ToString());
            Assert.False(actual.running);
        }
        
        [Fact]
        public async Task TestToggleRunningAction()
        {
            await _store.Provide(Segment.Analytics.System.DefaultState(_configuration, _storage.Object));
            await _store.Dispatch<Segment.Analytics.System.ToggleRunningAction, Segment.Analytics.System>(
                new Segment.Analytics.System.ToggleRunningAction(true));

            var actual = await _store.CurrentState<Segment.Analytics.System>();
            
            Assert.Equal(_settings.integrations.ToString(), actual.settings.integrations.ToString());
            Assert.True(actual.running);
        }
        
        [Fact]
        public async Task TestAddDestinationToSettingsAction()
        {
            var expectedKey = "foo";
            await _store.Provide(Segment.Analytics.System.DefaultState(_configuration, _storage.Object));
            await _store.Dispatch<Segment.Analytics.System.AddDestinationToSettingsAction, Segment.Analytics.System>(
                new Segment.Analytics.System.AddDestinationToSettingsAction(expectedKey));

            var actual = await _store.CurrentState<Segment.Analytics.System>();
            
            Assert.True(actual.settings.integrations.GetBool(expectedKey));
            Assert.False(actual.running);
        }
    }

    public class UserInfoTest
    {
        private Store _store;

        private UserInfo _userInfo;

        private Configuration _configuration;

        private Mock<IStorage> _storage;

        public UserInfoTest()
        {
            _store = new Store(true);
            _userInfo = new UserInfo()
            {
                anonymousId = "foo",
                userId = "bar",
                traits = new JsonObject { ["baz"] = "qux" }
            };
            _configuration = new Configuration(
                writeKey: "123",
                storageProvider: new DefaultStorageProvider("tests"),
                autoAddSegmentDestination: false,
                userSynchronizeDispatcher: true
            );
            _storage = new Mock<IStorage>();
        }

        [Fact]
        public void TestUserInfoDefaultState()
        {
            var expectedAnonymousId = "foo";
            var expectedUserId = "bar";
            var expectedTraits = new JsonObject {["foo"] = "bar"};
            _storage
                .Setup(o => o.Read(StorageConstants.UserId))
                .Returns(expectedUserId);
            _storage
                .Setup(o => o.Read(StorageConstants.AnonymousId))
                .Returns(expectedAnonymousId);
            _storage
                .Setup(o => o.Read(StorageConstants.Traits))
                .Returns(JsonUtility.ToJson(expectedTraits));

            var actual = UserInfo.DefaultState(_configuration, _storage.Object);
            
            Assert.Equal(expectedAnonymousId, actual.anonymousId);
            Assert.Equal(expectedUserId, actual.userId);
            Assert.Equal(expectedTraits.ToString(), actual.traits.ToString());
        }
        
        [Fact]
        public void TestUserInfoDefaultStateException()
        {
            var expectedAnonymousId = "foo";
            var expectedUserId = "bar";
            var badTraits = "{";
            _storage
                .Setup(o => o.Read(StorageConstants.UserId))
                .Returns(expectedUserId);
            _storage
                .Setup(o => o.Read(StorageConstants.AnonymousId))
                .Returns(expectedAnonymousId);
            _storage
                .Setup(o => o.Read(StorageConstants.Traits))
                .Returns(badTraits);

            var actual = UserInfo.DefaultState(_configuration, _storage.Object);
            
            Assert.Equal(expectedAnonymousId, actual.anonymousId);
            Assert.Equal(expectedUserId, actual.userId);
            Assert.Empty(actual.traits);
        }

        [Fact]
        public async Task TestResetAction()
        {
            await _store.Provide(_userInfo);
            await _store.Dispatch<UserInfo.ResetAction, UserInfo>(new UserInfo.ResetAction());

            var actual = await _store.CurrentState<UserInfo>();
            
            Assert.NotEqual(_userInfo.anonymousId, actual.anonymousId);
            Assert.Null(actual.userId);
            Assert.Null(actual.traits);
        }

        [Fact]
        public async Task TestSetUserIdAction()
        {
            var expectedUserId = "test";
            await _store.Provide(_userInfo);
            await _store.Dispatch<UserInfo.SetUserIdAction, UserInfo>(
                new UserInfo.SetUserIdAction(expectedUserId));

            var actual = await _store.CurrentState<UserInfo>();
            
            Assert.Equal(_userInfo.anonymousId, actual.anonymousId);
            Assert.Equal(expectedUserId, actual.userId);
            Assert.Equal(_userInfo.traits.ToString(), actual.traits.ToString());
        }
        
        [Fact]
        public async Task TestSetTraitsAction()
        {
            var expectedTraits = new JsonObject { ["fred"] = "thud" };
            await _store.Provide(_userInfo);
            await _store.Dispatch<UserInfo.SetTraitsAction, UserInfo>(
                new UserInfo.SetTraitsAction(expectedTraits));

            var actual = await _store.CurrentState<UserInfo>();
            
            Assert.Equal(_userInfo.anonymousId, actual.anonymousId);
            Assert.Equal(_userInfo.userId, actual.userId);
            Assert.Equal(expectedTraits.ToString(), actual.traits.ToString());
        }
        
        
        [Fact]
        public async Task TestSetUserIdAndTraitsAction()
        {
            var expectedUserId = "test";
            var expectedTraits = new JsonObject { ["fred"] = "thud" };
            await _store.Provide(_userInfo);
            await _store.Dispatch<UserInfo.SetUserIdAndTraitsAction, UserInfo>(
                new UserInfo.SetUserIdAndTraitsAction(expectedUserId, expectedTraits));

            var actual = await _store.CurrentState<UserInfo>();
            
            Assert.Equal(_userInfo.anonymousId, actual.anonymousId);
            Assert.Equal(expectedUserId, actual.userId);
            Assert.Equal(expectedTraits.ToString(), actual.traits.ToString());
        }
        
        [Fact]
        public async Task TestSetAnonymousIdAction()
        {
            var expectedAnonymousId = "test";
            await _store.Provide(_userInfo);
            await _store.Dispatch<UserInfo.SetAnonymousIdAction, UserInfo>(
                new UserInfo.SetAnonymousIdAction(expectedAnonymousId));

            var actual = await _store.CurrentState<UserInfo>();
            
            Assert.Equal(expectedAnonymousId, actual.anonymousId);
            Assert.Equal(_userInfo.userId, actual.userId);
            Assert.Equal(_userInfo.traits.ToString(), actual.traits.ToString());
        }
    }
}