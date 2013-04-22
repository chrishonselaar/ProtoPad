using Android.App;
using Android.Widget;
using Android.OS;
using ProtoPadServerLibrary_Android;

namespace SampleApp_Android
{
    [Activity(Label = "SampleApp_Android", MainLauncher = true, Icon = "@drawable/icon")]
    public class MainActivity : Activity
    {
        private ProtoPadServer _protoPadServer;

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);
            SetContentView(Resource.Layout.Main);
            var ipAddressTextView = FindViewById<TextView>(Resource.Id.textView1);

            // Create the ProtoPad server and start listening for messages from the ProtoPad Client.
            _protoPadServer = ProtoPadServer.Create(this);
            
            // And that's it! 
            // ProtoPad Client should be able to automatically discover this app on your local wifi network (through multicast discovery)
            // The ProtoPad Client will be able to issue any code to this app, and that code will be able to inspect and alter your activity to any extent.
            // So get prototyping :-)

            ipAddressTextView.Text = string.Format("Connect ProtoPad Client to me on {0}:{1}", _protoPadServer.LocalIPAddress, _protoPadServer.ListeningPort);
        }

        protected override void OnDestroy()
        {
            // Dispose the ProtoPad server to release some important resources (such as an internally used MultiCast lock)
            if (_protoPadServer != null) _protoPadServer.Dispose();
        }
    }
}