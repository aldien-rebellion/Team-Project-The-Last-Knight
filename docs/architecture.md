# Gameplay module map

Read `GLOSSARY.md` for the domain terms. Existing scene/prefab references, item IDs,
catalog field names, prices, buff durations, and save formats remain compatible.

## Merchant trade

`Assets/Scripts/Inventory/MerchantTrade.cs` owns purchase/sale outcomes, price
lookup, limited stock and successful random-offer progression. `ShopUI` is the
presentation adapter: it displays the result, captures game state on success and
refreshes the view. `ShopItemConfig.cs` keeps the original type namespace for
serialized catalogs; it has no dependency on UI rendering.

`InventoryManager.CanAccept` and `TryAddItem` provide an all-or-nothing receipt.
World pickups continue to use `AddItem` and retain the unaccepted remainder.
Keep capacity policy here rather than repeating it in a caller. Purchases reject
unavailable rune rewards and capped/invalid stat rewards before charging Gold.

Test surface: `MerchantTradeTests`, plus `RandomMerchantOfferTests` and the shop
integration in `QuickItemTests`.

## Quick Item consumption

Each inventory owns one `QuickItemConsumption`. Begin captures the first occupied
quick slot and its item identity. Complete revalidates identity and eligibility,
clears the pending use before applying its effect, and consumes at most once.
Cancel consumes nothing. Loading/resetting inventory cancels a pending use.

`PlayerController` owns animation, movement and interruption timing; it delegates
consumption rules to this module. The legacy `CompletePotionDrink` entry point
shares the pending use during a drink animation. Repeated animation completion
must not create a new immediate use. `QuickItemManager` projects inventory for the
HUD; its cached display records do not decide selection or quantity.

Test surface: `QuickItemConsumptionTests`, `QuickItemTests`, and the controller
drink lifecycle cases in `PlayerStatusAttributeTests`.

## Temporary buffs

`TemporaryBuffs` owns apply/refresh/remove/expiry, effect multipliers and active
snapshots. Its clock seam accepts a `Func<float>`: Unity time in gameplay, manual
time in tests. Display reads never consume the expiry invalidation used by
`PlayerStats.Update` to recalculate cached derived stats.

`PlayerStats` retains its existing entry points and immediate gameplay effects
(Purity ends hurt, Undying sets HP to one, Fortitude adjusts health).
`BuffDisplay` is the resource/formatting adapter shared by HUD and Character Status.
Keep timed rules out of it and keep resource loading out of `TemporaryBuffs`.

Test surface: `TemporaryBuffsTests` uses manual time through the module interface.
`PlayerStatusAttributeTests` and `PlayerBuffHUDTests` verify gameplay/display
integration without editing private buff timestamps.
