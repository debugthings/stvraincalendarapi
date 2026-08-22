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
using StVrainToICSFunctionApp.Services;
using System.Text;

namespace StVrainToICSFunctionApp.Formatters
{
    public class ICSTextOutputFormatter : TextOutputFormatter
    {
        public const string inputSessionContext = "inputsession";
        public const string displayTimeHhmmContext = "displaytimehhmm";
        public const string sectionFilterContext = "sectionfilter";
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
                    CalendarSectionFilter? sectionFilter =
                        httpContext.Items.TryGetValue(sectionFilterContext, out object? filterObj)
                        && filterObj is CalendarSectionFilter filter
                            ? filter
                            : null;
                    outPut = FormatMenuToICS(menu, inputSession, displayTimeHhmm, sectionFilter, logger);
                }
            }

            await httpContext.Response.WriteAsync(outPut, selectedEncoding);
        }

        private string FormatMenuToICS(
            Menu menu,
            Session inputSession,
            int? displayTimeHhmm,
            CalendarSectionFilter? sectionFilter,
            ILogger<ConvertToICS> logger) =>
            FormatMenuToICSCore(menu, inputSession, displayTimeHhmm, sectionFilter, logger);

        internal static string FormatMenuToICSCore(
            Menu menu,
            Session inputSession,
            int? displayTimeHhmm,
            CalendarSectionFilter? sectionFilter,
            ILogger logger)
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
                        // Breakfast, Lunch, snacks, etc.
                        var sessionName = familymenusession.ServingSession;
                        if (string.IsNullOrEmpty(sessionName))
                        {
                            continue;
                        }

                        bool filterDriven = sectionFilter is not null;
                        if (filterDriven)
                        {
                            if (!sectionFilter!.AllowsSession(sessionName))
                            {
                                continue;
                            }
                        }
                        else if (!Enum.TryParse(sessionName, out Session parsed) || parsed != inputSession)
                        {
                            continue;
                        }

                        Session sessionForTime = Enum.TryParse(sessionName, out Session parsedSession)
                            && parsedSession is Session.Breakfast or Session.Lunch
                                ? parsedSession
                                : inputSession;

                        foreach (var menuplan in familymenusession?.MenuPlans ?? [])
                        {
                            if (IsSuperSnack(menuplan))
                            {
                                logger.LogInformation("Skipping super snack plan {MenuPlanName}.", menuplan.MenuPlanName);
                                continue;
                            }

                            if (sectionFilter is not null
                                && !sectionFilter.AllowsPlan(sessionName, menuplan.MenuPlanName))
                            {
                                continue;
                            }

                            logger.LogInformation(
                                "Using menu plan {MenuPlanName} for {Session} (ServingSession={ServingSession}).",
                                menuplan.MenuPlanName,
                                inputSession,
                                sessionName);
                            logger.LogInformation("Starting the creation of the {Session} calendar events.", inputSession);
                            logger.LogInformation("Using {TimeZone} for the time zone.", defaultTimeZone);

                            int? planDisplayTime = sectionFilter?.DisplayTimeFor(
                                sessionName,
                                menuplan.MenuPlanName,
                                displayTimeHhmm) ?? displayTimeHhmm;

                            foreach (var day in menuplan?.Days ?? [])
                            {
                                if (!string.IsNullOrEmpty(day.Date))
                                {
                                    DateTime dateTimeOffset = DateTime.Parse(day.Date, CultureInfo.InvariantCulture);
                                    DateTime date = MealStart(dateTimeOffset, sessionForTime, planDisplayTime);

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

                                        if (sectionFilter is not null
                                            && !sectionFilter.AllowsMeal(sessionName, menuplan?.MenuPlanName, summary))
                                        {
                                            continue;
                                        }

                                        IReadOnlyList<string> recipeNames = sectionFilter is not null
                                            ? MenuLunchExtractor.GetRecipeNamesForCalendar(menumeal)
                                            : MenuLunchExtractor.GetMealRecipeNames(menumeal);
                                        if (recipeNames.Count == 0)
                                        {
                                            continue;
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
