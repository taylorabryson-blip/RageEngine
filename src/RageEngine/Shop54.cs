// Rage Engine 0.54 - the mod's items in the Crystal Hunt shop (the shop at your home).
// In Crystal Hunt there's no item book, so the serums, syringes, toxin oils and daggers, the NPC Spawner, the
// faction noisemakers, the Seer's Mirror and (0.55) the ten faction weapons are put on the shop's shelves - every time you're home, anything of ours
// that isn't already on display is added back.
//   - Each item is registered in a shop category that sells the same kind of thing (potions with potions, weapons
//     with weapons), so the game prices it from its own "value" and can buy it back from you.
//   - "Mod items in the shop": All (everything), Some (the tools plus a rotating handful of serums and toxins - for
//     a shop that would otherwise get crowded) or Off.
// Nothing here runs outside the home level, and every game call is guarded: if this game build's shop works
// differently, this switches itself off and logs why, and the shop is left exactly as the game made it.
using System;
using System.Collections.Generic;
using ThunderRoad;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RageEngine
{
    public sealed partial class RageEngineScript
    {
        public static ModOptionString[] shopValues54 = { new ModOptionString("All", null, "all"), new ModOptionString("Some", null, "some"), new ModOptionString("Off", null, "off") };
        [ModOptionCategory("Rage", 0), ModOptionOrder(70)]
        [ModOption("Mod items in the shop", "Crystal Hunt: Rage Engine's serums, toxins, faction weapons, the NPC Spawner, faction noisemakers and the Seer's Mirror on the home shop's shelves. Some = the tools and weapons plus a rotating handful.", valueSourceName = nameof(shopValues54), defaultValueIndex = 0)]
        public static string shop54 = "all";

        static float nextShop54;
        static object shopDone54;                 // the Shop we last stocked (a new level = a new Shop object)

        void TickShop54()
        {
            float now = Time.time;
            if (now < nextShop54) return;
            nextShop54 = now + 3f;
            if (shop54 == "off") return;
            Shop shop = null;
            Guard("shop54", () => shop = Shop.local);
            if (shop == null || ReferenceEquals(shop, shopDone54)) return;
            shopDone54 = shop;
            Guard("shop54", () => StockShop54(shop));
        }

        static void StockShop54(Shop shop)
        {
            if (shop.data == null || shop.data.shopStockCategories == null || shop.shopContents == null) return;
            var items = new List<ItemData>();
            var list = Catalog.GetDataList(Category.Item);
            if (list != null) foreach (var d in list) if (d is ItemData id && id.id != null && id.id.StartsWith("RE_") && ForSale54(id)) items.Add(id);
            if (items.Count == 0) return;
            // the shop prices things by category: put each of ours in a category that sells the same type
            int registered = 0;
            foreach (var it in items)
            {
                var cat = Category54(shop.data, it);
                if (cat == null) continue;
                if (cat.allAcceptedIDs == null) cat.allAcceptedIDs = new List<string>();
                if (!cat.allAcceptedIDs.Contains(it.id)) { cat.allAcceptedIDs.Add(it.id); registered++; }
            }
            // what's on the shelves already
            var have = new HashSet<string>();
            if (shop.shopContents.contents != null) foreach (var c in shop.shopContents.contents) if (c != null && c.referenceID != null) have.Add(c.referenceID);
            var add = new List<ItemData>();
            if (shop54 == "some")
            {
                var tools = new List<ItemData>(); var rest = new List<ItemData>();
                foreach (var it in items) (it.category == "Utilities" || IsWeapon55(it.id) ? tools : rest).Add(it);      // 0.55: the weapons always
                add.AddRange(tools);
                for (int i = 0; i < 12 && rest.Count > 0; i++) { int k = Random.Range(0, rest.Count); add.Add(rest[k]); rest.RemoveAt(k); }
            }
            else add.AddRange(items);
            int added = 0;
            foreach (var it in add)
            {
                if (have.Contains(it.id) || Category54(shop.data, it) == null) continue;
                shop.shopContents.AddItemContent(it);
                added++;
            }
            if (added > 0) Shop.RefreshContent();
            Log($"shop: {added} Rage Engine item(s) put on the shelves ({registered} newly priced, {items.Count} for sale)");
        }

        /// Things that are for sale: everything in our book categories (serums, syringes, oils, toxin daggers, tools).
        /// 0.55: + the ten faction weapons. The two rare finds and the treasures are found, not bought.
        static bool ForSale54(ItemData d) => !IsRare55(d.id) && (d.category == "Potions" || d.category == "Daggers" || d.category == "Utilities" || IsWeapon55(d.id));

        /// The shop category that already sells this type of item (else one that sells potions, else the first).
        static ShopData.ShopStockCategory Category54(ShopData data, ItemData it)
        {
            ShopData.ShopStockCategory potions = null, first = null;
            foreach (var cat in data.shopStockCategories)
            {
                if (cat == null) continue;
                if (first == null) first = cat;
                if (cat.allAcceptedIDs == null) continue;
                if (cat.allAcceptedIDs.Contains(it.id)) return cat;
                foreach (var id in cat.allAcceptedIDs)
                {
                    if (id == null || id.StartsWith("RE_")) continue;
                    var d = Catalog.GetData<ItemData>(id, false);
                    if (d == null) continue;
                    if (d.type == it.type) return cat;
                    if (d.type == ItemData.Type.Potion && potions == null) potions = cat;
                    break;                                                       // one sample per category is enough
                }
            }
            return potions ?? first;
        }
    }
}
