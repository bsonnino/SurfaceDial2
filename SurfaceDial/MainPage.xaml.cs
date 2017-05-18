using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Windows.Devices.Haptics;
using Windows.Foundation;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Input;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace SurfaceDial
{


    public sealed partial class MainPage : Page
    {
        public enum CurrentTool
        {
            Resize,
            Rotate,
            MoveX,
            MoveY,
            Color
        }

        private CurrentTool _currentTool;
        private readonly List<SolidColorBrush> _namedBrushes;
        private int _selBrush;
        private bool _isButtonHolding;

        public MainPage()
        {
            this.InitializeComponent();
            // Create a reference to the RadialController.
            var controller = RadialController.CreateForCurrentView();

            // Create the items for the menu
            var itemResize = RadialControllerMenuItem.CreateFromFontGlyph("Resize", "\xE8B9", "Segoe MDL2 Assets");
            var itemRotate = RadialControllerMenuItem.CreateFromFontGlyph("Rotate", "\xE7AD", "Segoe MDL2 Assets");
            var itemMoveX = RadialControllerMenuItem.CreateFromFontGlyph("MoveX", "\xE8AB", "Segoe MDL2 Assets");
            var itemMoveY = RadialControllerMenuItem.CreateFromFontGlyph("MoveY", "\xE8CB", "Segoe MDL2 Assets");
            var itemColor = RadialControllerMenuItem.CreateFromFontGlyph("Color", "\xE7E6", "Segoe MDL2 Assets");

            // Add the items to the menu
            controller.Menu.Items.Add(itemResize);
            controller.Menu.Items.Add(itemRotate);
            controller.Menu.Items.Add(itemMoveX);
            controller.Menu.Items.Add(itemMoveY);
            controller.Menu.Items.Add(itemColor);

            // Select the correct tool when the item is selected
            itemResize.Invoked += (s, e) => _currentTool = CurrentTool.Resize;
            itemRotate.Invoked += (s, e) => _currentTool = CurrentTool.Rotate;
            itemMoveX.Invoked += (s, e) => _currentTool = CurrentTool.MoveX;
            itemMoveY.Invoked += (s, e) => _currentTool = CurrentTool.MoveY;
            itemColor.Invoked += (s, e) => _currentTool = CurrentTool.Color;

            // Get all named colors and create brushes from them
            _namedBrushes = typeof(Colors).GetRuntimeProperties().Select(c => new SolidColorBrush((Color)c.GetValue(null))).ToList();

            controller.RotationChanged += ControllerRotationChanged;

            // Leave only the Volume default item - Zoom and Undo won't be used
            RadialControllerConfiguration config = RadialControllerConfiguration.GetForCurrentView();
            config.SetDefaultMenuItems(new[] { RadialControllerSystemMenuItemKind.Volume });
            config.ActiveControllerWhenMenuIsSuppressed = controller;
            config.IsMenuSuppressed = true;
            controller.ButtonHolding += (s, e) => _isButtonHolding = true;
            controller.ButtonReleased += (s, e) => _isButtonHolding = false;
            controller.UseAutomaticHapticFeedback = false;
            ToolText.Text = _currentTool.ToString();
        }

        private void ControllerRotationChanged(RadialController sender,
            RadialControllerRotationChangedEventArgs args)
        {
            if (_isButtonHolding)
            {
                _currentTool = args.RotationDeltaInDegrees > 0 ?
                    MoveNext(_currentTool) : MovePrevious(_currentTool);
                ToolText.Text = _currentTool.ToString();
                SendHapticFeedback(args.SimpleHapticsController, 1);
                return;
            }
            switch (_currentTool)
            {
                case CurrentTool.Resize:
                    Scale.ScaleX += args.RotationDeltaInDegrees / 10;
                    Scale.ScaleY += args.RotationDeltaInDegrees / 10;
                    break;
                case CurrentTool.Rotate:
                    Rotate.Angle += args.RotationDeltaInDegrees;
                    break;
                case CurrentTool.MoveX:
                    if (CanMove(Translate, Scale, args.RotationDeltaInDegrees))
                    {
                        Translate.X += args.RotationDeltaInDegrees;
                        if (args.IsButtonPressed)
                            Translate.Y += args.RotationDeltaInDegrees;
                    }
                    else
                        SendHapticFeedback(args.SimpleHapticsController,3);
                    break;
                case CurrentTool.MoveY:
                    if (CanMove(Translate, Scale, args.RotationDeltaInDegrees))
                    {
                        Translate.Y += args.RotationDeltaInDegrees;
                        if (args.IsButtonPressed)
                            Translate.X += args.RotationDeltaInDegrees;
                    }
                    else
                        SendHapticFeedback(args.SimpleHapticsController,3);
                    break;
                case CurrentTool.Color:
                    _selBrush += (int)(args.RotationDeltaInDegrees / 10);
                    if (_selBrush >= _namedBrushes.Count)
                        _selBrush = 0;
                    if (_selBrush < 0)
                        _selBrush = _namedBrushes.Count - 1;
                    Rectangle.Fill = _namedBrushes[(int)_selBrush];
                    break;
                default:
                    break;
            }
        }

        private void SendHapticFeedback(SimpleHapticsController simpleHapticsController, int count)
        {
            var feedback = simpleHapticsController.SupportedFeedback.FirstOrDefault(f => f.Waveform == KnownSimpleHapticsControllerWaveforms.Click);
            if (feedback != null)
                simpleHapticsController.SendHapticFeedbackForPlayCount(feedback, 1, count, TimeSpan.FromMilliseconds(100));
        }

        private bool CanMove(TranslateTransform translate, ScaleTransform scale,
            double delta)
        {
            var canMove = delta > 0 ?
                translate.X + 60 * scale.ScaleX + delta < ActualWidth / 2 &&
                translate.Y + 60 * scale.ScaleY + delta < ActualHeight / 2 :
                translate.X - 60 * scale.ScaleX + delta > -ActualWidth / 2 &&
                translate.Y - 60 * scale.ScaleY + delta > -ActualHeight / 2;
            return canMove;
        }

        private CurrentTool MoveNext(CurrentTool currentTool)
        {
            return Enum.GetValues(typeof(CurrentTool)).Cast<CurrentTool>()
                .FirstOrDefault(t => (int)t > (int)currentTool);
        }
        private CurrentTool MovePrevious(CurrentTool currentTool)
        {
            return currentTool == CurrentTool.Resize ? CurrentTool.Color :
                Enum.GetValues(typeof(CurrentTool)).Cast<CurrentTool>()
                .OrderByDescending(t => t)
                .FirstOrDefault(t => (int)t < (int)currentTool);
        }
    }
}
