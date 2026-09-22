using System;
using System.Numerics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using FluentIcons.Common;
using FluentSymbolIcon = FluentIcons.WinUI.SymbolIcon;

namespace FluentScrobbler.Services
{
    public static class HeartUiHelper
    {
        public static readonly SolidColorBrush HeartRedBrush = new(Windows.UI.Color.FromArgb(255, 235, 47, 89));

        public static void SetHeartVisual(FluentSymbolIcon icon, bool isLoved)
        {
            icon.IconVariant = isLoved ? IconVariant.Filled : IconVariant.Regular;
            if (isLoved)
            {
                icon.Foreground = HeartRedBrush;
            }
            else
            {
                if (Application.Current.Resources.TryGetValue("TextFillColorSecondaryBrush", out var brush) && brush is Brush b)
                {
                    icon.Foreground = b;
                }
            }
        }

        public static void AnimateHeartPop(UIElement element)
        {
            try
            {
                var visual = ElementCompositionPreview.GetElementVisual(element);
                var compositor = visual.Compositor;

                var anim = compositor.CreateVector3KeyFrameAnimation();
                anim.InsertKeyFrame(0.0f, new Vector3(1f, 1f, 1f));
                anim.InsertKeyFrame(0.35f, new Vector3(1.4f, 1.4f, 1f));
                anim.InsertKeyFrame(0.65f, new Vector3(0.85f, 0.85f, 1f));
                anim.InsertKeyFrame(1.0f, new Vector3(1f, 1f, 1f));
                anim.Duration = TimeSpan.FromMilliseconds(300);

                float cx = element.ActualSize.X > 0 ? element.ActualSize.X / 2.0f : 10f;
                float cy = element.ActualSize.Y > 0 ? element.ActualSize.Y / 2.0f : 10f;
                visual.CenterPoint = new Vector3(cx, cy, 0f);

                visual.StartAnimation("Scale", anim);
            }
            catch { }
        }
    }
}
