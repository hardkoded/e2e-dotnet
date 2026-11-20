// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Internal;

/// <summary>
/// Typed accessor for a process-wide slot. A host can load a copy of this assembly other than the runner's (a plugin in its own
/// <see cref="System.Runtime.Loader.AssemblyLoadContext"/>), so runner-owned state is shared through the slot instead of a static, which
/// each copy has of its own. Upstream's <c>realmSlot</c>, which keys <c>globalThis</c> by a registry symbol; the slot lives in
/// <see cref="AppDomain.SetData(string, object?)"/>, which every load context shares. A value is read by another copy only if its type
/// is one the framework defines, so the slot holds plain data.
/// </summary>
internal sealed class RealmSlot<T>(string key)
    where T : class
{
    /// <summary>What every copy locks while it creates the slot's value, so two copies do not each create one.</summary>
    public static object Gate => AppDomain.CurrentDomain;

    public T? Get() => AppDomain.CurrentDomain.GetData(key) as T;

    public void Set(T value) => AppDomain.CurrentDomain.SetData(key, value);

    public void Delete() => AppDomain.CurrentDomain.SetData(key, null);
}
