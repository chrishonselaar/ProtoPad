using MonoTouch.Foundation;
using MonoTouch.UIKit;
using ProtoPadServerLibrary_iOS;

namespace SampleApp_iOS
{
    [Register("AppDelegate")]
    public class AppDelegate : UIApplicationDelegate
    {
        private UIWindow _window;
        private ProtoPadServer _protoPadServer;

        public override bool FinishedLaunching(UIApplication app, NSDictionary options)
        {
            _window = new UIWindow(UIScreen.MainScreen.Bounds);
            _window.MakeKeyAndVisible();

            

            // Create the ProtoPad server and start listening for messages from the ProtoPad Client.
            _protoPadServer = ProtoPadServer.Create(this, _window);

            // And that's it! 
            // ProtoPad Client should be able to automatically discover this app on your local wifi network (through multicast discovery)
            // The ProtoPad Client will be able to issue any code to this app, and that code will be able to inspect and alter your activity to any extent.
            // So get prototyping :-)

            var ipAddressLabel = new UILabel(_window.Bounds)
                {
                    Text = string.Format("Connect ProtoPad Client to me on {0}:{1}", _protoPadServer.LocalIPAddress, _protoPadServer.ListeningPort),
                    LineBreakMode = UILineBreakMode.WordWrap,
                    Lines = 0
                };
            _window.AddSubview(ipAddressLabel);

            return true;
        }

        public override void WillTerminate(UIApplication application)
        {   
            // Dispose the ProtoPad server to stop listening for requests
            // and to release some important resources
            if (_protoPadServer != null) _protoPadServer.Dispose();
        }
    }
}