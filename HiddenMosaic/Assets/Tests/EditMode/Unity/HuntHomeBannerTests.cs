using Nixin.Game.Core;
using Nixin.Monetization;
using NUnit.Framework;
using UnityEngine;

namespace Game.Unity.EditTests
{
    public sealed class HuntHomeBannerTests
    {
        [Test]
        public void Catalog_HomeBanner_IsBottomBannerPlacement()
        {
            var catalog = Resources.Load<MonetizationCatalogAsset>("NixinMonetization/MonetizationCatalog");
            Assert.IsNotNull(catalog);

            PlacementRow row = null;
            for (var i = 0; i < catalog.Placements.Count; i++)
            {
                if (catalog.Placements[i].id == HuntPlacements.HomeBanner)
                    row = catalog.Placements[i];
            }

            Assert.IsNotNull(row);
            Assert.AreEqual(PlacementKind.Banner, row.kind);
            Assert.IsTrue(row.enabled);
        }
    }
}
