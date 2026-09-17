# Message Catalog sample

`MessageCatalogSample.cs` walks through the Localization Core without a scene, an
asset, or any `UnityEngine` API. Call `MessageCatalogSample.Run()` from any script,
test, or editor menu and read the returned text.

It shows, in order:

1. Three message tables (`en`, `ru`, `ja`) built with `MessageTableBuilder`, each with a
   plain placeholder message and a plural message whose branches match the CLDR
   categories of that language.
2. A `LocalizationCatalog` with `en` as the default locale.
3. Formatting `items` for `ru-RU`: the catalog truncates to `ru` and the Russian rules
   select the `many` branch for 5, so the result reports `ru` as the resolved locale.
4. Formatting `greeting` for `de`, which has no table: the default locale answers and
   `UsedFallback` is true rather than the text being empty.
5. `LocalizationValidator` checking every locale against the source locale, with stable
   diagnostic codes for anything that disagrees. The sample data is consistent, so the
   report is valid with no diagnostics.

The sample is staging material like the package that carries it; it has not compiled or
run until the first Unity gate records a receipt.
