// Copyright 2026 Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static WinAppBenchmark.App.Support;

namespace WinAppBenchmark.Examples;

/// <summary>
/// Non-text form controls behind a gated submit: a radio group, a set of checkboxes, a switch, and a
/// notes field. Success requires the exact combination described in the on-screen instructions. The
/// mobile app also gates the submit on the soft keyboard being dismissed; a desktop has none, so that
/// gate and its "keyboard-warning" line are not here.
/// </summary>
public sealed class ChoiceControls : Component
{
    private static readonly string[] Sizes = ["Small", "Medium", "Large"];
    private static readonly string[] Toppings = ["Cheese", "Olives", "Mushrooms", "Onions"];
    private static readonly string[] RequiredToppings = ["Cheese", "Olives"];

    public override Element Render()
    {
        var (size, setSize) = UseState<string?>(null);
        var (toppings, setToppings) = UseState<IReadOnlySet<string>>(new HashSet<string>());
        var (rushDelivery, setRushDelivery) = UseState(false);
        var (notes, setNotes) = UseState("");
        var (submitted, setSubmitted) = UseState(false);

        var orderValid = size == "Medium"
            && toppings.Count == RequiredToppings.Length
            && RequiredToppings.All(toppings.Contains)
            && rushDelivery;

        // Toggles a topping checkbox on or off.
        void ToggleTopping(string topping) =>
            setToppings(toppings.Contains(topping) ? toppings.Where(other => other != topping).ToHashSet() : [.. toppings, topping]);

        if (submitted)
        {
            return VStack(Success("Order placed: Medium with Cheese, Olives (rush)")).Padding(24);
        }

        return ScrollView(VStack(12, [
            Hint("Order a Medium pizza with exactly Cheese and Olives, enable rush delivery, then place the order."),
            BodyStrong("Size"),
            HStack(12, [.. Sizes.Select(option =>
                RadioButton(option, size == option, isChecked =>
                {
                    if (isChecked)
                    {
                        setSize(option);
                    }
                }, groupName: "size")
                .AutomationId($"size-{option.ToLowerInvariant()}")
                .WithKey(option))]),
            BodyStrong("Toppings"),
            .. Toppings.Select(topping =>
                CheckBox(toppings.Contains(topping), _ => ToggleTopping(topping), label: topping)
                    .AutomationId($"topping-{topping.ToLowerInvariant()}")
                    .WithKey(topping)),
            ToggleSwitch(rushDelivery, setRushDelivery, header: "Rush delivery").AutomationName("Rush delivery").AutomationId("rush-delivery"),
            BodyStrong("Delivery notes"),
            TextBox(notes, setNotes, placeholderText: "Ring the doorbell twice").AutomationName("Delivery notes").AutomationId("delivery-notes"),
            Button("Place order", () => setSubmitted(true)).IsEnabled(orderValid).AutomationId("place-order"),
        ]).Padding(24));
    }
}
