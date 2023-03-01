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
                Integrations = new JsonObject
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
            
            Assert.Equal(_configuration, actual._configuration);
            Assert.Equal(settings.Integrations.ToString(), actual._settings.Integrations.ToString());
            Assert.False(actual._running);
        }
        
        [Fact]
        public void TestSystemDefaultStateException()
        {
            _storage
                .Setup(o => o.Read(It.IsAny<StorageConstants>()))
                .Throws<Exception>();

            var actual = Segment.Analytics.System.DefaultState(_configuration, _storage.Object);
            
            Assert.Equal(_configuration, actual._configuration);
            Assert.Equal(_settings.Integrations.ToString(), actual._settings.Integrations.ToString());
            Assert.False(actual._running);
        }

        [Fact]
        public async Task TestUpdateSettingsAction()
        {
            await _store.Provide(Segment.Analytics.System.DefaultState(_configuration, _storage.Object));
            await _store.Dispatch<Segment.Analytics.System.UpdateSettingsAction, Segment.Analytics.System>(
                new Segment.Analytics.System.UpdateSettingsAction(_settings));

            var actual = await _store.CurrentState<Segment.Analytics.System>();
            
            Assert.Equal(_settings.Integrations.ToString(), actual._settings.Integrations.ToString());
            Assert.False(actual._running);
        }
        
        [Fact]
        public async Task TestToggleRunningAction()
        {
            await _store.Provide(Segment.Analytics.System.DefaultState(_configuration, _storage.Object));
            await _store.Dispatch<Segment.Analytics.System.ToggleRunningAction, Segment.Analytics.System>(
                new Segment.Analytics.System.ToggleRunningAction(true));

            var actual = await _store.CurrentState<Segment.Analytics.System>();
            
            Assert.Equal(_settings.Integrations.ToString(), actual._settings.Integrations.ToString());
            Assert.True(actual._running);
        }
        
        [Fact]
        public async Task TestAddDestinationToSettingsAction()
        {
            var expectedKey = "foo";
            await _store.Provide(Segment.Analytics.System.DefaultState(_configuration, _storage.Object));
            await _store.Dispatch<Segment.Analytics.System.AddDestinationToSettingsAction, Segment.Analytics.System>(
                new Segment.Analytics.System.AddDestinationToSettingsAction(expectedKey));

            var actual = await _store.CurrentState<Segment.Analytics.System>();
            
            Assert.True(actual._settings.Integrations.GetBool(expectedKey));
            Assert.False(actual._running);
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
                _anonymousId = "foo",
                _userId = "bar",
                _traits = new JsonObject { ["baz"] = "qux" }
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

            var actual = UserInfo.DefaultState(_storage.Object);
            
            Assert.Equal(expectedAnonymousId, actual._anonymousId);
            Assert.Equal(expectedUserId, actual._userId);
            Assert.Equal(expectedTraits.ToString(), actual._traits.ToString());
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

            var actual = UserInfo.DefaultState(_storage.Object);
            
            Assert.Equal(expectedAnonymousId, actual._anonymousId);
            Assert.Equal(expectedUserId, actual._userId);
            Assert.Empty(actual._traits);
        }

        [Fact]
        public async Task TestResetAction()
        {
            await _store.Provide(_userInfo);
            await _store.Dispatch<UserInfo.ResetAction, UserInfo>(new UserInfo.ResetAction());

            var actual = await _store.CurrentState<UserInfo>();
            
            Assert.NotEqual(_userInfo._anonymousId, actual._anonymousId);
            Assert.Null(actual._userId);
            Assert.Null(actual._traits);
        }

        [Fact]
        public async Task TestSetUserIdAction()
        {
            var expectedUserId = "test";
            await _store.Provide(_userInfo);
            await _store.Dispatch<UserInfo.SetUserIdAction, UserInfo>(
                new UserInfo.SetUserIdAction(expectedUserId));

            var actual = await _store.CurrentState<UserInfo>();
            
            Assert.Equal(_userInfo._anonymousId, actual._anonymousId);
            Assert.Equal(expectedUserId, actual._userId);
            Assert.Equal(_userInfo._traits.ToString(), actual._traits.ToString());
        }
        
        [Fact]
        public async Task TestSetTraitsAction()
        {
            var expectedTraits = new JsonObject { ["fred"] = "thud" };
            await _store.Provide(_userInfo);
            await _store.Dispatch<UserInfo.SetTraitsAction, UserInfo>(
                new UserInfo.SetTraitsAction(expectedTraits));

            var actual = await _store.CurrentState<UserInfo>();
            
            Assert.Equal(_userInfo._anonymousId, actual._anonymousId);
            Assert.Equal(_userInfo._userId, actual._userId);
            Assert.Equal(expectedTraits.ToString(), actual._traits.ToString());
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
            
            Assert.Equal(_userInfo._anonymousId, actual._anonymousId);
            Assert.Equal(expectedUserId, actual._userId);
            Assert.Equal(expectedTraits.ToString(), actual._traits.ToString());
        }
        
        [Fact]
        public async Task TestSetAnonymousIdAction()
        {
            var expectedAnonymousId = "test";
            await _store.Provide(_userInfo);
            await _store.Dispatch<UserInfo.SetAnonymousIdAction, UserInfo>(
                new UserInfo.SetAnonymousIdAction(expectedAnonymousId));

            var actual = await _store.CurrentState<UserInfo>();
            
            Assert.Equal(expectedAnonymousId, actual._anonymousId);
            Assert.Equal(_userInfo._userId, actual._userId);
            Assert.Equal(_userInfo._traits.ToString(), actual._traits.ToString());
        }
    }
}