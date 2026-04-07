using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace StVrainToICSFunctionApp.Helpers;

public static class Helpers
{
    [return: MaybeNull]
    public static T GetEnvironmentVariable<T>(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return default;
        }

        string valueToConvert = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process) ?? string.Empty;
        if (string.IsNullOrEmpty(valueToConvert))
        {
            return default;
        }

        return (T)Convert.ChangeType(valueToConvert, typeof(T), CultureInfo.InvariantCulture);
    }
}
