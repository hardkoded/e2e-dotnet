// Copyright 2026 Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static WinAppBenchmark.App.Support;

namespace WinAppBenchmark.Examples;

/// <summary>
/// Master-detail drill-in with near-identical repeated cards. The agent must disambiguate the "Trail
/// Mix 500 g" card from its sibling variants, open its detail view, set the quantity to 2 with the
/// stepper, add it to the cart, and confirm the cart contents. Favorite hearts are interactive decoys.
/// </summary>
public sealed class ProductCatalog : Component
{
    private const string TargetId = "trail-mix-500";
    private const int TargetQuantity = 2;

    private sealed record Product(string Id, string Name, string Size, string Price);

    private enum View { List, Detail, Cart }

    private static readonly Product[] Products =
    [
        new("trail-mix-250", "Trail Mix", "250 g", "$3.99"),
        new("trail-mix-500", "Trail Mix", "500 g", "$6.99"),
        new("trail-mix-1000", "Trail Mix", "1 kg", "$11.99"),
        new("granola-500", "Granola", "500 g", "$5.49"),
        new("almonds-250", "Almonds", "250 g", "$7.99"),
        new("cashews-250", "Cashews", "250 g", "$8.49"),
    ];

    public override Element Render()
    {
        var (view, setView) = UseState(View.List);
        var (selectedId, setSelectedId) = UseState<string?>(null);
        var (quantity, setQuantity) = UseState(1);
        var (cart, setCart) = UseState<IReadOnlyDictionary<string, int>>(new Dictionary<string, int>());
        var (favorites, setFavorites) = UseState<IReadOnlySet<string>>(new HashSet<string>());

        var cartCount = cart.Values.Sum();
        var succeeded = cart.Count == 1 && cart.TryGetValue(TargetId, out var targetQuantity) && targetQuantity == TargetQuantity;
        var selected = Products.FirstOrDefault(product => product.Id == selectedId);

        // Toggles the favorite heart for a product; a decoy interaction that never contributes to the success state.
        void ToggleFavorite(string id) =>
            setFavorites(favorites.Contains(id) ? favorites.Where(other => other != id).ToHashSet() : [.. favorites, id]);

        // Adds the currently selected product and quantity to the cart, then returns to the list view.
        void AddToCart()
        {
            if (selected is null)
            {
                return;
            }

            var next = new Dictionary<string, int>(cart);
            next[selected.Id] = next.GetValueOrDefault(selected.Id) + quantity;
            setCart(next);
            setView(View.List);
        }

        if (view == View.Detail && selected is not null)
        {
            return VStack(12,
                Button("‹ Back to catalog", () => setView(View.List)).TextLink().AutomationId("back-to-list"),
                Subtitle($"{selected.Name} {selected.Size}"),
                Body(selected.Price),
                HStack(12,
                    Button("−", () => setQuantity(Math.Max(1, quantity - 1))).AutomationName("Decrease quantity").AutomationId("quantity-decrease"),
                    Body(quantity.ToString()).MinWidth(24).VAlign(VerticalAlignment.Center).AutomationId("quantity-value"),
                    Button("+", () => setQuantity(quantity + 1)).AutomationName("Increase quantity").AutomationId("quantity-increase")),
                Button("Add to cart", AddToCart).AutomationId("add-to-cart"))
                .Padding(24);
        }

        if (view == View.Cart)
        {
            return VStack(12, [
                Button("‹ Back to catalog", () => setView(View.List)).TextLink().AutomationId("back-to-list"),
                Subtitle("Cart"),
                When(cartCount == 0, () => Hint("The cart is empty")),
                .. Products.Where(product => cart.ContainsKey(product.Id)).Select(product =>
                    Body($"{cart[product.Id]} × {product.Name} {product.Size}").WithKey(product.Id)),
                When(succeeded, () => Success("Order ready: 2 × Trail Mix 500 g")),
            ]).Padding(24);
        }

        return ScrollView(VStack(12, [
            Hint($"Add exactly {TargetQuantity} × Trail Mix 500 g to the cart, then review the cart"),
            Button($"Cart ({cartCount})", () => setView(View.Cart)).AutomationId("open-cart"),
            .. Products.Select(product => HStack(8,
                    Button(VStack(2, BodyStrong(product.Name), Caption($"{product.Size} · {product.Price}")), () =>
                    {
                        setSelectedId(product.Id);
                        setQuantity(1);
                        setView(View.Detail);
                    })
                    .AutomationName($"{product.Name} {product.Size} {product.Price}")
                    .AutomationId($"product-{product.Id}")
                    .Flex(grow: 1),
                    Button(favorites.Contains(product.Id) ? "♥" : "♡", () => ToggleFavorite(product.Id))
                        .AutomationName($"Favorite {product.Name} {product.Size}")
                        .AutomationId($"favorite-{product.Id}"))
                .WithKey(product.Id)),
        ]).Padding(24));
    }
}
