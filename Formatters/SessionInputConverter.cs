using Microsoft.Azure.Functions.Worker.Converters;
using StVrainToICSFunctionApp.Models;

namespace StVrainToICSFunctionApp.Formatters
{
    public class SessionInputConverter : IInputConverter
    {
        public ValueTask<ConversionResult> ConvertAsync(ConverterContext context)
        {
            if (context.TargetType == typeof(Session) && Enum.TryParse(context.Source as string, true, out Session outsession))
            {
                return ValueTask.FromResult(ConversionResult.Success(outsession));
            }

            return ValueTask.FromResult(ConversionResult.Success(Session.None));
        }
    }
}
