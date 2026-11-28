namespace MobileBenchmark;

/// <summary>
/// The system biometric prompt of the Control Inventory's Device section. Only iOS has one here: the simulator's Face ID is
/// what a test answers; Android has no biometric API in MAUI, so it reports failure.
/// </summary>
internal static class Biometrics
{
#if IOS
    public static async Task<bool> AuthenticateAsync()
    {
        using var context = new LocalAuthentication.LAContext();
        var (success, _) = await context.EvaluatePolicyAsync(
            LocalAuthentication.LAPolicy.DeviceOwnerAuthenticationWithBiometrics,
            "Unlock the Control Inventory"
        );
        return success;
    }
#else
    public static Task<bool> AuthenticateAsync() => Task.FromResult(false);
#endif
}
