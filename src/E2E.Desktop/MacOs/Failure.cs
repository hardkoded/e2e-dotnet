// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Desktop.MacOs;

/// <summary>
/// Whether a failed input may have reached the app.
/// The line is drawn at the app's content: text typed, a value set, a press, a selection, an item chosen, focus moved into a
/// control, or a list scrolled to bring a control into view. Bringing a window forward, and opening a list's menu that closed
/// again with nothing chosen, change nothing the app keeps, so a failure after only those is still <see cref="NotDelivered"/>.
/// </summary>
internal enum Delivery
{
    /// <summary>Nothing reached the app's content, so the caller may try another way.</summary>
    NotDelivered,

    /// <summary>Something was sent, and its effect could not be confirmed.</summary>
    Unknown,
}

/// <summary>Every code a failure carries.</summary>
internal static class Codes
{
    public const string StaleRef = "STALE_REF";
    public const string ElementNotFound = "ELEMENT_NOT_FOUND";
    public const string ActionNotSupported = "ACTION_NOT_SUPPORTED";
    public const string ActionFailed = "ACTION_FAILED";
    public const string AppNotFound = "APP_NOT_FOUND";
    public const string WindowNotFound = "WINDOW_NOT_FOUND";
    public const string AppUnresponsive = "APP_UNRESPONSIVE";
    public const string PermDenied = "PERM_DENIED";
    public const string PolicyDenied = "POLICY_DENIED";
    public const string Timeout = "TIMEOUT";
    public const string InvalidArgs = "INVALID_ARGS";
    public const string Internal = "INTERNAL";
}

/// <summary>
/// A failure, with a code from the closed list the engine maps, and whether anything reached the app. Port of <c>failure.rs</c>:
/// where Rust returns <c>Outcome&lt;T&gt;</c>, the C# port throws this exception, and <see cref="MacOsDriver"/> turns it into the
/// failed <see cref="Envelope{T}"/> at the boundary.
/// </summary>
internal sealed class Failure : Exception
{
    public Failure(string code, string message, Delivery delivery, IReadOnlyDictionary<string, object?>? details = null)
        : base(message)
    {
        Code = code;
        Delivery = delivery;
        Details = details;
    }

    public string Code { get; }

    public Delivery Delivery { get; }

    public IReadOnlyDictionary<string, object?>? Details { get; }

    /// <summary>A failure that sent nothing.</summary>
    public static Failure Refused(string code, string message) => new(code, message, Delivery.NotDelivered);

    /// <summary>A failure after input may have reached the app.</summary>
    public static Failure Unconfirmed(string code, string message) => new(code, message, Delivery.Unknown);

    public static Failure TimedOut(string what) => Refused(Codes.Timeout, $"{what} ran out of time");

    /// <summary>The details of a stall the engine may read again past: <c>{ retryable: true }</c>.</summary>
    public static IReadOnlyDictionary<string, object?> Retryable { get; } =
        new Dictionary<string, object?> { ["retryable"] = true };

    public Failure WithDetails(IReadOnlyDictionary<string, object?> details) => new(Code, Message, Delivery, details);

    public Failure WithDetail(string key, object? value) =>
        WithDetails(new Dictionary<string, object?> { [key] = value });

    /// <summary>The same failure, marked as one that may have reached the app.</summary>
    public Failure Delivered() => new(Code, Message, Delivery.Unknown, Details);

    /// <summary>The same failure with another message.</summary>
    public Failure WithMessage(string message) => new(Code, message, Delivery, Details);

    public DriverError ToError() =>
        new(
            Code,
            Message,
            new Disposition(Delivery == Delivery.NotDelivered ? "not_delivered" : "delivered_unverified"),
            Details
        );
}
