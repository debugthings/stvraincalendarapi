using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StVrainToICSFunctionApp.Helpers
{
    public static class Helpers
    {
        public static T GetEnvironmentVariable<T>(string name)
        {
            if (!string.IsNullOrEmpty(name))
            {
                string valueToConvert = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process) ?? string.Empty;
                if (!string.IsNullOrEmpty(valueToConvert))
                {
                    return (T)Convert.ChangeType(valueToConvert, typeof(T)) ?? default(T);
                }
            }
            return default;
        }
    }
}
