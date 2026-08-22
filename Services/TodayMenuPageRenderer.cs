using System.Net;
using System.Text;

namespace StVrainToICSFunctionApp.Services;

public static class TodayMenuPageRenderer
{
    public static string Render(TodayMenuPageModel model)
    {
        var sb = new StringBuilder();
        sb.Append("""
            <!DOCTYPE html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>Lunch Menu — St. Vrain Valley Schools</title>
              <style>
                :root {
                  --bg: #f4f1ea;
                  --card: #ffffff;
                  --ink: #1f2937;
                  --muted: #6b7280;
                  --accent: #c2410c;
                  --line: #e5e7eb;
                  --shadow: 0 10px 30px rgba(15, 23, 42, 0.08);
                }
                * { box-sizing: border-box; }
                body {
                  margin: 0;
                  font-family: "Segoe UI", system-ui, -apple-system, sans-serif;
                  color: var(--ink);
                  background:
                    radial-gradient(circle at top left, #fde68a 0, transparent 28%),
                    radial-gradient(circle at top right, #fed7aa 0, transparent 24%),
                    var(--bg);
                  min-height: 100vh;
                }
                .wrap {
                  max-width: 1100px;
                  margin: 0 auto;
                  padding: 2rem 1.25rem 3rem;
                }
                header {
                  text-align: center;
                  margin-bottom: 1.5rem;
                }
                header h1 {
                  margin: 0 0 0.5rem;
                  font-size: clamp(1.75rem, 4vw, 2.5rem);
                  letter-spacing: -0.02em;
                }
                .nav {
                  display: flex;
                  align-items: center;
                  justify-content: center;
                  gap: 0.75rem;
                  margin-bottom: 0.75rem;
                }
                .nav-btn {
                  display: inline-flex;
                  align-items: center;
                  justify-content: center;
                  width: 2.75rem;
                  height: 2.75rem;
                  border-radius: 999px;
                  border: 1px solid var(--line);
                  background: var(--card);
                  color: var(--ink);
                  text-decoration: none;
                  font-size: 1.35rem;
                  box-shadow: var(--shadow);
                  transition: background 0.15s ease, transform 0.15s ease;
                }
                .nav-btn:hover {
                  background: #fff7ed;
                  transform: translateY(-1px);
                }
                .nav-btn[aria-disabled="true"] {
                  opacity: 0.35;
                  pointer-events: none;
                  box-shadow: none;
                }
                .nav-date {
                  min-width: min(18rem, 70vw);
                  font-size: 1.05rem;
                  color: var(--muted);
                }
                .skip-note {
                  margin: 0 auto 1.5rem;
                  max-width: 36rem;
                  color: var(--muted);
                  font-size: 0.95rem;
                }
                .day-block + .day-block {
                  margin-top: 2rem;
                }
                .day-heading {
                  display: flex;
                  flex-wrap: wrap;
                  align-items: baseline;
                  gap: 0.5rem 0.85rem;
                  margin: 0 0 1rem;
                }
                .day-heading h2 {
                  margin: 0;
                  font-size: 1.2rem;
                }
                .day-heading .when {
                  color: var(--muted);
                  font-size: 0.95rem;
                }
                .grid {
                  display: grid;
                  grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
                  gap: 1.25rem;
                }
                .school-card {
                  background: var(--card);
                  border: 1px solid var(--line);
                  border-radius: 18px;
                  box-shadow: var(--shadow);
                  overflow: hidden;
                  display: flex;
                  flex-direction: column;
                }
                .school-head {
                  padding: 1.25rem 1.25rem 1rem;
                  border-bottom: 1px solid var(--line);
                  background: linear-gradient(180deg, #fff7ed 0%, #ffffff 100%);
                }
                .school-head h3 {
                  margin: 0 0 0.35rem;
                  font-size: 1.2rem;
                }
                .meta {
                  color: var(--muted);
                  font-size: 0.92rem;
                }
                .school-body {
                  padding: 1rem 1.25rem 1.25rem;
                  flex: 1;
                }
                .meal-group + .meal-group {
                  margin-top: 1rem;
                  padding-top: 1rem;
                  border-top: 1px dashed var(--line);
                }
                .meal-group h4 {
                  margin: 0 0 0.5rem;
                  font-size: 1rem;
                  color: var(--accent);
                }
                .meal-group ul {
                  margin: 0;
                  padding-left: 1.15rem;
                }
                .meal-group li {
                  margin: 0.25rem 0;
                }
                .empty {
                  color: var(--muted);
                  font-style: italic;
                }
                .subscribe {
                  margin-top: 1rem;
                  padding-top: 1rem;
                  border-top: 1px solid var(--line);
                }
                .subscribe a {
                  color: var(--accent);
                  text-decoration: none;
                  font-weight: 600;
                }
                .subscribe a:hover {
                  text-decoration: underline;
                }
                footer {
                  margin-top: 2rem;
                  text-align: center;
                  color: var(--muted);
                  font-size: 0.9rem;
                }
              </style>
            </head>
            <body>
              <div class="wrap">
                <header>
                  <h1>School Lunch</h1>
                  <div class="nav" aria-label="Lunch day navigation">
            """);

        AppendNavButton(sb, model.PreviousDateUrl, "←", "Previous lunch day");
        sb.Append("<div class=\"nav-date\">");
        sb.Append(Escape(model.Primary.DateLabel));
        sb.Append("</div>");
        AppendNavButton(sb, model.NextDateUrl, "→", "Next lunch day");

        sb.Append("</div>");
        if (model.IsShowingNextAvailable)
        {
            sb.Append("<p class=\"skip-note\">No lunch today — showing the next published meal day.</p>");
        }

        sb.Append("</header>");

        AppendDayBlock(sb, model.Primary);
        if (model.Upcoming is LunchDayView upcoming)
        {
            AppendDayBlock(sb, upcoming);
        }

        sb.Append("""
                <footer>
                  St. Vrain Valley Schools lunch menus via LINQ Connect
                </footer>
              </div>
            </body>
            </html>
            """);

        return sb.ToString();
    }

    private static void AppendNavButton(StringBuilder sb, string? href, string label, string ariaLabel)
    {
        if (string.IsNullOrEmpty(href))
        {
            sb.Append("<span class=\"nav-btn\" aria-disabled=\"true\" aria-label=\"");
            sb.Append(Escape(ariaLabel));
            sb.Append("\">");
            sb.Append(Escape(label));
            sb.Append("</span>");
            return;
        }

        sb.Append("<a class=\"nav-btn\" href=\"");
        sb.Append(Escape(href));
        sb.Append("\" aria-label=\"");
        sb.Append(Escape(ariaLabel));
        sb.Append("\">");
        sb.Append(Escape(label));
        sb.Append("</a>");
    }

    private static void AppendDayBlock(StringBuilder sb, LunchDayView day)
    {
        sb.Append("<section class=\"day-block\">");
        sb.Append("<div class=\"day-heading\">");
        sb.Append("<h2>");
        sb.Append(Escape(day.RelativeLabel));
        sb.Append("</h2>");
        sb.Append("<span class=\"when\">");
        sb.Append(Escape(day.DateLabel));
        sb.Append("</span>");
        sb.Append("</div>");
        sb.Append("<div class=\"grid\">");

        foreach (SchoolTodayMenu school in day.Schools)
        {
            sb.Append("<article class=\"school-card\">");
            sb.Append("<div class=\"school-head\">");
            sb.Append("<h3>");
            sb.Append(Escape(school.DisplayName));
            sb.Append("</h3>");
            sb.Append("<div class=\"meta\">Lunch at ");
            sb.Append(Escape(school.LunchTime));
            sb.Append("</div>");
            sb.Append("</div>");
            sb.Append("<div class=\"school-body\">");

            if (school.Meals.Count == 0)
            {
                sb.Append("<p class=\"empty\">No lunch menu published for this day.</p>");
            }
            else
            {
                foreach (MenuLunchExtractor.LunchMealGroup meal in school.Meals)
                {
                    sb.Append("<section class=\"meal-group\">");
                    sb.Append("<h4>");
                    sb.Append(Escape(meal.Title));
                    sb.Append("</h4><ul>");
                    foreach (string item in meal.Items)
                    {
                        sb.Append("<li>");
                        sb.Append(Escape(item));
                        sb.Append("</li>");
                    }

                    sb.Append("</ul></section>");
                }
            }

            sb.Append("<div class=\"subscribe\"><a href=\"");
            sb.Append(Escape(school.CalendarUrl));
            sb.Append("\">Subscribe to calendar</a></div>");
            sb.Append("</div></article>");
        }

        sb.Append("</div></section>");
    }

    private static string Escape(string value) => WebUtility.HtmlEncode(value);
}
