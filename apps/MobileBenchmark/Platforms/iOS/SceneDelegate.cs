using Foundation;
using UIKit;

namespace MobileBenchmark;

[Register("SceneDelegate")]
public class SceneDelegate : MauiUISceneDelegate
{
    /// <summary>Hands a <c>e2e-benchmark://</c> link to <c>App.OnAppLinkRequestReceived</c>.</summary>
    public override void OpenUrlContexts(UIScene scene, NSSet<UIOpenUrlContext> urlContexts)
    {
        base.OpenUrlContexts(scene, urlContexts);
        foreach (var context in urlContexts)
        {
            if (context.Url.AbsoluteString is { } link)
            {
                Microsoft.Maui.Controls.Application.Current?.SendOnAppLinkRequestReceived(new Uri(link));
            }
        }
    }
}
