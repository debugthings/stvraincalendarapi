using System.Globalization;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Serialization;
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
        public const string displayTimeHhmmContext = "displaytimehhmm";
        private const string calendarType = "text/calendar";
        private const string defaultTimeZone = "America/Denver";
        private const int baseMinutesPastHour = 30;

        /// <summary>
        /// Parses a clock time from the route, e.g. 1100, 1130, 1200.
        /// </summary>
        public static bool TryGetClockTime(int hhmm, out int hours, out int minutes)
        {
            hours = hhmm / 100;
            minutes = hhmm % 100;
            if (hhmm < 0 || hours > 23 || minutes > 59)
            {
                hours = 0;
                minutes = 0;
                return false;
            }

            return true;
        }

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
                    ILogger<ConvertToICS> logger = httpContext.RequestServices.GetRequiredService<ILogger<ConvertToICS>>();
                    int? displayTimeHhmm = httpContext.Items.TryGetValue(displayTimeHhmmContext, out object? timeObj) && timeObj is int hhmm
                        ? hhmm
                        : null;
                    outPut = FormatMenuToICS(menu, inputSession, displayTimeHhmm, logger);
                }
            }

            await httpContext.Response.WriteAsync(outPut, selectedEncoding);
        }

        private string FormatMenuToICS(Menu menu, Session inputSession, int? displayTimeHhmm, ILogger<ConvertToICS> logger)
        {
            var sb = new StringBuilder();
            // The calendar wants a timezone.
            var calendar = new Ical.Net.Calendar();
            calendar.AddTimeZone(new VTimeZone(defaultTimeZone)); // TZ should be added

            logger.LogInformation("Using {TimeZone} for the time zone.", defaultTimeZone);
            try
            {
                if (inputSession == Session.Academic)
                {
                    logger.LogInformation("Starting the creation of the {Session} calendar events.", inputSession);
                    logger.LogInformation("Using {TimeZone} for the time zone.", defaultTimeZone);
                    foreach (var academiccalendar in menu?.AcademicCalendars ?? [])
                    {
                        foreach (var academicDay in academiccalendar?.Days ?? [])
                        {
                            DateTime dateTimeOffset = DateTime.Parse(
                                academicDay?.Date ?? DateTime.Now.ToString(CultureInfo.InvariantCulture),
                                CultureInfo.InvariantCulture);
                            string note = academicDay?.Note ?? string.Empty;
                            // Ical.Net 5+: all-day events use DATE-only CalDateTime; IsAllDay is derived.
                            var dayStart = new CalDateTime(dateTimeOffset.Year, dateTimeOffset.Month, dateTimeOffset.Day);
                            var next = dateTimeOffset.AddDays(1);
                            var dayEnd = new CalDateTime(next.Year, next.Month, next.Day);
                            var calendarEvent = new CalendarEvent
                            {
                                // If Name property is used, it MUST be RFC 5545 compliant
                                Summary = academicDay?.Note ?? "Meal Name Empty", // Should always be present
                                Description = sb.ToString(), // optional
                                Start = dayStart,
                                End = dayEnd,
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
                                if (IsSuperSnack(menuplan))
                                {
                                    logger.LogInformation("Skipping super snack plan {MenuPlanName}.", menuplan.MenuPlanName);
                                    continue;
                                }

                                // FamilyMenuSession.ServingSession (Breakfast/Lunch) is the filter; plan names
                                // vary by school level (Elementary Lunch, Middle School Lunch, etc.).
                                logger.LogInformation(
                                    "Using menu plan {MenuPlanName} for {Session} (ServingSession={ServingSession}).",
                                    menuplan.MenuPlanName,
                                    inputSession,
                                    sessionName);
                                logger.LogInformation("Starting the creation of the {Session} calendar events.", inputSession);
                                logger.LogInformation("Using {TimeZone} for the time zone.", defaultTimeZone);
                                foreach (var day in menuplan?.Days ?? [])
                                {
                                    if (!string.IsNullOrEmpty(day.Date))
                                    {
                                        // The day of the week for the meal.
                                        DateTime dateTimeOffset = DateTime.Parse(day.Date, CultureInfo.InvariantCulture);
                                        // Default: Lunch 11:30, Breakfast 8:30. Optional route time is HHmm (1100, 1130, 1200).
                                        DateTime date = MealStart(dateTimeOffset, session, displayTimeHhmm);

                                        foreach (var menumeal in day?.MenuMeals ?? [])
                                        {
                                            if (IsSuperSnack(menumeal))
                                            {
                                                continue;
                                            }

                                            string? summary = menumeal?.MenuMealName;
                                            if (string.IsNullOrEmpty(summary))
                                            {
                                                continue;
                                            }

                                            IEnumerable<Recipe[]?> recipeMeals = menumeal?.RecipeCategories?
                                                .Where(rc => !string.IsNullOrEmpty(rc.CategoryName) && rc.CategoryName.Equals("Meal", StringComparison.OrdinalIgnoreCase))
                                                .Select(rc => rc.Recipes)
                                                ?? [];
                                            if (!recipeMeals.Any())
                                            {
                                                continue;
                                            }

                                            var recipeNames = new List<string>();
                                            foreach (var recipes in recipeMeals)
                                            {
                                                for (int i = 0; i < (recipes?.Length ?? 0); i++)
                                                {
                                                    string recipeName = recipes?[i]?.RecipeName ?? "Item Name Empty";
                                                    recipeNames.Add(recipeName);
                                                }
                                            }

                                            var calendarEvent = new CalendarEvent
                                            {
                                                Summary = summary,
                                                Description = string.Join(Environment.NewLine, recipeNames),
                                                Start = new CalDateTime(date),
                                                End = new CalDateTime(date.AddMinutes(30)),
                                            };
                                            calendar.Events.Add(calendarEvent);
                                            logger.LogInformation(
                                                "Added {Summary} on {Start} to the {Session} calendar.",
                                                calendarEvent.Summary,
                                                calendarEvent.Start,
                                                inputSession);
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
                logger.LogError(ex, "Exception while creating the {Session} calendar.", inputSession);
            }

            logger.LogInformation("Successfully created the {Session} calendar", inputSession);
            var serializer = new CalendarSerializer();
            return serializer.SerializeToString(calendar) ?? string.Empty;
        }

        internal static bool IsSuperSnack(MenuPlan? plan) =>
            !string.IsNullOrEmpty(plan?.MenuPlanName)
            && plan.MenuPlanName.Contains("Super Snack", StringComparison.OrdinalIgnoreCase);

        internal static bool IsSuperSnack(MenuMeal? meal) =>
            !string.IsNullOrEmpty(meal?.MenuMealName)
            && meal.MenuMealName.Contains("Super Snack", StringComparison.OrdinalIgnoreCase);

        internal static DateTime MealStart(DateTime day, Session session, int? displayTimeHhmm)
        {
            if (displayTimeHhmm is int hhmm && TryGetClockTime(hhmm, out int hours, out int minutes))
            {
                return day.AddHours(hours).AddMinutes(minutes);
            }

            return day.AddHours((int)session).AddMinutes(baseMinutesPastHour);
        }
    }
}
