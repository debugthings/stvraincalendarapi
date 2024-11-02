using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Serialization;
using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using StVrainToICSFunctionApp.Models;
using System.Text;

namespace StVrainToICSFunctionApp.Formatters
{
    public class ICSTextOutputFormatter : TextOutputFormatter
    {
        public const string inputSessionContext = "inputsession";
        private const string calendarType = "text/calendar";
        private const string defaultTimeZone = "America/Denver";

        public ICSTextOutputFormatter()
        {
            SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse(calendarType));

            SupportedEncodings.Add(Encoding.UTF8);
            SupportedEncodings.Add(Encoding.ASCII);
            SupportedEncodings.Add(Encoding.Unicode);
            
        }

        public override bool CanWriteResult(OutputFormatterCanWriteContext context)
        {
            return context.Object is Menu;
        }

        public async override Task WriteResponseBodyAsync(OutputFormatterWriteContext context, Encoding selectedEncoding)
        {
            var httpContext = context.HttpContext;
            string outPut = string.Empty;
            if (context.HttpContext.Items[inputSessionContext] is Session inputSession)
            {
                if (context.Object is Menu menu)
                {
                    ILogger _logger = httpContext.RequestServices.GetService<ILogger<ConvertToICS>>();
                    outPut = FormatMenuToICS(menu, inputSession, _logger);
                }
            }

            await httpContext.Response.WriteAsync(outPut, selectedEncoding);
        }

        private string FormatMenuToICS(Menu menu, Session inputSession, ILogger _logger)
        {
            var sb = new StringBuilder();
            // The calendar wants a timezone.
            var calendar = new Ical.Net.Calendar();
            calendar.AddTimeZone(new VTimeZone(defaultTimeZone)); // TZ should be added

            _logger?.LogInformation($"Using {defaultTimeZone} for the time zone.");
            try
            {
                if (inputSession == Session.Academic)
                {
                    _logger?.LogInformation($"Starting the creation of the {inputSession} calendar events.");
                    _logger?.LogInformation($"Using {defaultTimeZone} for the time zone.");
                    foreach (var academiccalendar in menu?.AcademicCalendars ?? [])
                    {
                        foreach (var academicDay in academiccalendar?.Days ?? [])
                        {
                            DateTime dateTimeOffset = DateTime.Parse(academicDay?.Date ?? DateTime.Now.ToString());
                            string note = academicDay?.Note ?? string.Empty;
                            var calendarEvent = new CalendarEvent
                            {
                                // If Name property is used, it MUST be RFC 5545 compliant
                                Summary = academicDay?.Note ?? "Meal Name Empty", // Should always be present
                                Description = sb.ToString(), // optional
                                IsAllDay = true,
                                Start = new CalDateTime(dateTimeOffset),

                            };
                            sb.Clear();
                            calendar.Events.Add(calendarEvent);
                        }
                    }
                }
                else
                {
                    foreach (var familymenusession in menu?.FamilyMenuSessions ?? [])
                    {
                        // This is Breakfast, Lunch, Snacks
                        // We only care about Breakfast and Lunch
                        var sessionName = familymenusession.ServingSession;
                        if (Enum.TryParse(sessionName, out Session session) && session == inputSession)
                        {
                            foreach (var menuplan in familymenusession?.MenuPlans ?? [])
                            {
                                // This will either be Breakfast {yyyy} or Elementary & PK Lunch {yyyy}
                                // Unknown what this will look like in 2026 but can assume it will be the same.
                                var menuPlan = menuplan.MenuPlanName ?? string.Empty;
                                bool isWhatWeWant = false;

                                switch (session)
                                {
                                    case Session.Breakfast:
                                        isWhatWeWant = menuPlan.StartsWith("Breakfast", StringComparison.OrdinalIgnoreCase);
                                        break;
                                    case Session.Lunch:
                                        isWhatWeWant = menuPlan.StartsWith("Elementary & PK Lunch", StringComparison.OrdinalIgnoreCase);
                                        break;
                                    default:
                                        break;
                                }

                                if (isWhatWeWant)
                                {
                                    _logger?.LogInformation($"Starting the creation of the {inputSession} calendar events.");
                                    _logger?.LogInformation($"Using {defaultTimeZone} for the time zone.");
                                    foreach (var day in menuplan?.Days ?? [])
                                    {
                                        if (!string.IsNullOrEmpty(day.Date))
                                        {
                                            // The day of the week for the meal.
                                            DateTime dateTimeOffset = DateTime.Parse(day.Date);
                                            // The session enum has the lunch hour set as the value so we don't have to do any switching
                                            var date = dateTimeOffset.AddHours((int)session).AddMinutes(30);

                                            foreach (var menumeal in day?.MenuMeals ?? [])
                                            {
                                                if (menumeal != null)
                                                {
                                                    // Only add the meals
                                                    IEnumerable<Recipe[]?> recipeMeals = menumeal?.RecipeCategories?.Where(rc => !string.IsNullOrEmpty(rc.CategoryName) && rc.CategoryName.Equals("Meal", StringComparison.OrdinalIgnoreCase)).Select(rc => rc.Recipes) ?? [];
                                                    if (recipeMeals.Any())
                                                    {
                                                        foreach (var recipes in recipeMeals)
                                                        {
                                                            for (int i = 0; i < (recipes?.Length ?? 0); i++)
                                                            {
                                                                string recipeName = recipes?[i]?.RecipeName ?? "Item Name Empty";
                                                                if (i < (recipes?.Length ?? 0) - 1)
                                                                {
                                                                    sb.AppendLine(recipeName);
                                                                }
                                                                else
                                                                {
                                                                    sb.Append(recipeName);
                                                                }
                                                            }
                                                        }
                                                        var calendarEvent = new CalendarEvent
                                                        {
                                                            // If Name property is used, it MUST be RFC 5545 compliant
                                                            Summary = menumeal?.MenuMealName ?? "Meal Name Empty", // Should always be present
                                                            Description = sb.ToString(), // optional
                                                            Start = new CalDateTime(date),
                                                            End = new CalDateTime(date.AddMinutes(30)),
                                                        };
                                                        sb.Clear();
                                                        calendar.Events.Add(calendarEvent);
                                                        _logger?.LogInformation($"Added {calendarEvent.Summary} on {calendarEvent.Start} to the {inputSession} calendar.");
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Exception while creating the {inputSession} calendar.");
            }

            _logger?.LogInformation($"Successfully created the {inputSession} calendar");
            var serializer = new CalendarSerializer();
            var whatToSend = serializer.SerializeToString(calendar);
            return whatToSend;
        }
    }
}
