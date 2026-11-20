// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Internal;

/// <summary>Internal brand keys of the slots the runner shares through the process. Upstream's <c>internal/brands.ts</c>.</summary>
internal static class Brands
{
    /// <summary>Slot holding the secret values the sessions open in the process share. Upstream's <c>sharedSecretsBrand</c>.</summary>
    public const string SharedSecretsBrand = "e2e.sharedSecrets.v1";
}
