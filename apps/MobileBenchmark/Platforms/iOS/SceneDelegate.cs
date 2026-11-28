using Foundation;
using UIKit;

namespace MobileBenchmark;

[Register("SceneDelegate")]
public class SceneDelegate : MauiUISceneDelegate
{
    /// <summary>A link that launches the app arrives in the connection options, not in <see cref="OpenUrl"/>.</summary>
    public override void WillConnect(UIScene scene, UISceneSession session, UISceneConnectionOptions connectionOptions)
    {
        base.WillConnect(scene, session, connectionOptions);
        Forward(connectionOptions.UrlContexts);
    }

    /// <summary>A link that reaches the running app.</summary>
    public override bool OpenUrl(UIScene scene, NSSet<UIOpenUrlContext> urlContexts)
    {
        var handled = base.OpenUrl(scene, urlContexts);
        Forward(urlContexts);
        return handled;
    }

    /// <summary>Hands a <c>e2e-benchmark://</c> link to <c>App.OnAppLinkRequestReceived</c>.</summary>
    private static void Forward(NSSet<UIOpenUrlContext>? urlContexts)
    {
        if (urlContexts is null)
        {
            return;
        }

        foreach (var context in urlContexts)
        {
            if (context.Url.AbsoluteString is { } link)
            {
                Microsoft.Maui.Controls.Application.Current?.SendOnAppLinkRequestReceived(new Uri(link));
            }
        }
    }
}
