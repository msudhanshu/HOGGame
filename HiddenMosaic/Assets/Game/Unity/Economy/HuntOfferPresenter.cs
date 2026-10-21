using Nixin.Game.Core;
using UnityEngine;

namespace Game.Unity
{
    /// <summary>Replaces the package's generic "Need More?" popup with the themed jam popup for
    /// any SpendOrOffer / rewarded-with-fallback placement the game requests. Purchases are
    /// routed to the shop; the original request resolves as declined in that case.</summary>
    public static class HuntOfferPresenter
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (GameServices.Monetization != null)
                GameServices.Monetization.SetPresenter(Present);
        }

        static void Present(OfferRequest offer)
        {
            var hints = offer.PlacementId == HuntPlacements.Hint;
            var resolved = false;
            HuntPopups.Jam(new HuntJamPayload
            {
                Currency = hints ? HuntCurrencies.Hints : HuntCurrencies.Coins,
                Title = hints ? "Out of Hints!" : "Need More?",
                Message = offer.AdAvailable ? "Watch a short video, or visit the shop." : "Visit the shop to continue.",
                AdAvailable = offer.AdAvailable,
                AdLabel = "WATCH VIDEO",
                OnAd = () =>
                {
                    resolved = true;
                    offer.AcceptAd();
                },
                OnShop = () =>
                {
                    if (resolved) return;
                    resolved = true;
                    offer.Decline();
                    HuntPopups.Shop("offer_" + offer.PlacementId);
                },
                OnClose = () =>
                {
                    if (resolved) return;
                    resolved = true;
                    offer.Decline();
                }
            });
        }
    }
}
