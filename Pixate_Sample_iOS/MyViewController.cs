using System;
using MonoTouch.UIKit;
using System.Drawing;
using PixateLib;

namespace Pixate_Sample_iOS
{
    public class MyViewController : UIViewController
    {
        UIButton _button;
        int _numClicks;
        private const float ButtonWidth = 200;
        private const float ButtonHeight = 50;        

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();

            View.Frame = UIScreen.MainScreen.Bounds;
            View.BackgroundColor = UIColor.White;
            View.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;

            _button = UIButton.FromType(UIButtonType.RoundedRect);

            _button.Frame = new RectangleF(
                View.Frame.Width / 2 - ButtonWidth / 2,
                200,
                ButtonWidth,
                ButtonHeight);

            _button.SetTitle("Click me", UIControlState.Normal);

            _button.SetStyleId("button1");

            _button.TouchUpInside += (sender, e) => _button.SetTitle(String.Format("clicked {0} times", _numClicks++), UIControlState.Normal);

            _button.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleTopMargin |
                UIViewAutoresizing.FlexibleBottomMargin;

            View.AddSubview(_button);
        }
    }
}