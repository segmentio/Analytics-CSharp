using System;
using Segment.Analytics;
using Segment.Serialization;
using Xamarin.Forms;

namespace XamarinSample
{
    public partial class MainPage : ContentPage
    {
        enum EventType
        {
            Track, Identify, Screen, Group
        }

        private EventType _type = EventType.Track;

        #region UI setup

        public MainPage()
        {
            InitializeComponent();

            TrackButton.Clicked += Track_Clicked;
            IdentifyButton.Clicked += Identify_Clicked;
            ScreenButton.Clicked += Screen_Clicked;
            GroupButton.Clicked += Group_Clicked;
            FlushButton.Clicked += Flush_Clicked;
            SendEventButton.Clicked += SendEvent_Clicked;

            AddPlugin();
        }

        void Track_Clicked(object sender, EventArgs e)
        {
            _type = EventType.Track;
            EventLabel.Text = "Track Event";
            EventNameEditor.Placeholder = "Event Name";
            Reset();
        }

        void Identify_Clicked(object sender, EventArgs e)
        {
            _type = EventType.Identify;
            EventLabel.Text = "Identify User";
            EventNameEditor.Placeholder = "User Id";
            Reset();
        }

        void Screen_Clicked(object sender, EventArgs e)
        {
            _type = EventType.Screen;
            EventLabel.Text = "Track Screen";
            EventNameEditor.Placeholder = "Screen Name";
            Reset();
        }

        void Group_Clicked(object sender, EventArgs e)
        {
            _type = EventType.Group;
            EventLabel.Text = "Identify Group";
            EventNameEditor.Placeholder = "Group Id";
            Reset();
        }

        void Flush_Clicked(object sender, EventArgs e)
        {
            Flush();
            Reset();
        }

        void SendEvent_Clicked(object sender, EventArgs e)
        {
            string field = string.IsNullOrEmpty(EventNameEditor.Text) ? EventNameEditor.Placeholder : EventNameEditor.Text;
            string key = string.IsNullOrEmpty(PropertyEditor.Text) ? PropertyEditor.Placeholder : PropertyEditor.Text;
            string value = string.IsNullOrEmpty(ValueEditor.Text) ? ValueEditor.Placeholder : ValueEditor.Text;
            var payload = new JsonObject
            {
                [key] = value
            };

            Send(field, payload);
        }

        private void Reset()
        {
            EventNameEditor.Text = "";
            PropertyEditor.Text = "";
            ValueEditor.Text = "";
        }

        #endregion

        #region Analytics usage samples

        void Flush()
        {
            App.analytics.Flush();
        }

        private void Send(string field, JsonObject payload)
        {
            switch (_type)
            {
                case EventType.Track:
                    App.analytics.Track(field, payload);
                    break;
                case EventType.Identify:
                    App.analytics.Identify(field, payload);
                    break;
                case EventType.Screen:
                    App.analytics.Screen(field, payload);
                    break;
                case EventType.Group:
                    App.analytics.Group(field, payload);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void AddPlugin()
        {
            App.analytics.Add(new DisplayResultPlugin(result =>
            {
                Device.BeginInvokeOnMainThread(() =>
                {
                    ConsoleLabel.Text = result;
                });
            }));
        }

        /// <summary>
        /// Sample of a custom plugin that sends the final event payload
        /// to a callback in json format
        /// </summary>
        private class DisplayResultPlugin : Plugin
        {
            public override PluginType Type => PluginType.After;

            private readonly Action<string> _onResult;

            public DisplayResultPlugin(Action<string> onResult)
            {
                _onResult = onResult;
            }

            public override RawEvent Execute(RawEvent incomingEvent)
            {
                string result = JsonUtility.ToJson(incomingEvent);
                _onResult(result);

                return base.Execute(incomingEvent);
            }
        }

        #endregion
    }
}
