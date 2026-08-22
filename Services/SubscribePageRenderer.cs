using System.Globalization;
using System.Net;
using System.Text;

namespace StVrainToICSFunctionApp.Services;

public static class SubscribePageRenderer
{
    public static string Render(string? preferredSchoolCode, string? preferredBuildingId, int defaultDisplayTime)
    {
        string preferredCode = preferredSchoolCode ?? string.Empty;
        string preferredBuilding = preferredBuildingId ?? string.Empty;
        string lunchDefault = defaultDisplayTime.ToString("0000", CultureInfo.InvariantCulture);

        var sb = new StringBuilder();
        sb.Append("""
            <!DOCTYPE html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>Subscribe — Menu Calendar</title>
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
                .wrap { max-width: 760px; margin: 0 auto; padding: 2rem 1.25rem 3rem; }
                h1 { margin: 0 0 0.35rem; font-size: clamp(1.6rem, 4vw, 2.2rem); }
                .lede { color: var(--muted); margin: 0 0 1.5rem; }
                .card {
                  background: var(--card);
                  border: 1px solid var(--line);
                  border-radius: 18px;
                  box-shadow: var(--shadow);
                  padding: 1.25rem 1.35rem;
                }
                label { display: block; font-weight: 600; margin: 1rem 0 0.4rem; }
                label:first-child { margin-top: 0; }
                select, input[type="text"] {
                  width: 100%;
                  padding: 0.65rem 0.75rem;
                  border: 1px solid var(--line);
                  border-radius: 10px;
                  font: inherit;
                }
                .sections { margin-top: 0.5rem; }
                .session {
                  border: 1px solid var(--line);
                  border-radius: 14px;
                  padding: 0.9rem 1rem 1rem;
                  margin-top: 0.85rem;
                  background: linear-gradient(180deg, #fff7ed 0%, #ffffff 40%);
                }
                .session-head {
                  display: flex;
                  flex-wrap: wrap;
                  gap: 0.75rem 1rem;
                  align-items: center;
                  justify-content: space-between;
                  margin-bottom: 0.65rem;
                }
                .session-head h2 {
                  margin: 0;
                  font-size: 1.1rem;
                }
                .session-time {
                  display: flex;
                  align-items: center;
                  gap: 0.4rem;
                  font-weight: 500;
                  color: var(--muted);
                  font-size: 0.92rem;
                }
                .session-time input {
                  width: 5.5rem;
                  padding: 0.4rem 0.5rem;
                }
                .plan {
                  border: 1px solid var(--line);
                  border-radius: 12px;
                  padding: 0.75rem 0.9rem;
                  margin-top: 0.65rem;
                  background: #fff;
                }
                .plan > label { margin: 0; font-size: 1rem; }
                .meals { margin: 0.55rem 0 0 1.1rem; }
                .meals label {
                  font-weight: 400;
                  margin: 0.3rem 0;
                  display: flex;
                  gap: 0.45rem;
                  align-items: center;
                }
                .actions { margin-top: 1.25rem; display: flex; gap: 0.75rem; flex-wrap: wrap; }
                .btn {
                  background: var(--accent);
                  color: #fff;
                  border: 0;
                  border-radius: 999px;
                  padding: 0.7rem 1.2rem;
                  font: inherit;
                  font-weight: 600;
                  cursor: pointer;
                }
                .btn:disabled { opacity: 0.5; cursor: not-allowed; }
                .btn-secondary {
                  background: transparent;
                  color: var(--accent);
                  border: 1px solid var(--accent);
                }
                .status { margin-top: 0.85rem; color: var(--muted); min-height: 1.25rem; }
                .result {
                  display: none;
                  margin-top: 1.25rem;
                  padding-top: 1.1rem;
                  border-top: 1px solid var(--line);
                }
                .result.show { display: block; }
                .result a { color: var(--accent); font-weight: 600; word-break: break-all; }
                .back { display: inline-block; margin-bottom: 1rem; color: var(--accent); text-decoration: none; font-weight: 600; }
                .hint { color: var(--muted); font-size: 0.9rem; margin: 0.35rem 0 0; }
              </style>
            </head>
            <body>
              <div class="wrap">
                <a class="back" href="/">← Back to today's lunch</a>
                <h1>Subscribe to menu calendar</h1>
                <p class="lede">Pick a school, which serving sessions and meal lines to include, and a start time per session. We'll create a short Google Calendar–friendly link.</p>
                <div class="card">
                  <label for="school">School</label>
                  <select id="school"><option value="">Loading schools…</option></select>

                  <label>Menu sections</label>
                  <p class="hint">Grouped by serving session (Breakfast, Lunch, snacks, …). Breakfast and Lunch are selected by default.</p>
                  <div class="sections" id="sections"><p class="hint">Select a school to load sections.</p></div>

                  <div class="actions">
                    <button class="btn" id="generate" type="button">Generate calendar link</button>
                    <button class="btn btn-secondary" id="selectAll" type="button">Select all</button>
                    <button class="btn btn-secondary" id="selectNone" type="button">Select none</button>
                  </div>
                  <div class="status" id="status"></div>
                  <div class="result" id="result">
                    <p>Your Google Calendar–friendly link:</p>
                    <p><a id="resultUrl" href="#" target="_blank" rel="noopener"></a></p>
                    <p class="hint">Optional .ics URL: <a id="resultIcs" href="#" target="_blank" rel="noopener"></a></p>
                    <div class="actions">
                      <button class="btn btn-secondary" id="copy" type="button">Copy link</button>
                    </div>
                  </div>
                </div>
              </div>
              <script>
            """);

        sb.Append("const preferredSchoolCode = ");
        sb.Append(ToJsString(preferredCode));
        sb.Append(";\nconst preferredBuildingId = ");
        sb.Append(ToJsString(preferredBuilding));
        sb.Append(";\nconst lunchDefaultTime = ");
        sb.Append(ToJsString(lunchDefault));
        sb.Append(";\n");

        sb.Append("""
            const schoolEl = document.getElementById('school');
            const sectionsEl = document.getElementById('sections');
            const statusEl = document.getElementById('status');
            const resultEl = document.getElementById('result');
            const generateBtn = document.getElementById('generate');

            document.getElementById('selectAll').addEventListener('click', () => setAllMeals(true));
            document.getElementById('selectNone').addEventListener('click', () => setAllMeals(false));
            document.getElementById('copy').addEventListener('click', async () => {
              const url = document.getElementById('resultUrl').href;
              try {
                await navigator.clipboard.writeText(url);
                statusEl.textContent = 'Link copied.';
              } catch {
                statusEl.textContent = 'Copy failed — select the link manually.';
              }
            });

            schoolEl.addEventListener('change', () => loadSections());
            generateBtn.addEventListener('click', () => createLink());

            function isDefaultSession(name) {
              const n = String(name || '').toLowerCase();
              return n.includes('breakfast') || n.includes('lunch');
            }

            function setAllMeals(checked) {
              sectionsEl.querySelectorAll('input[type="checkbox"]').forEach(cb => { cb.checked = checked; cb.indeterminate = false; });
              sectionsEl.querySelectorAll('.plan-check').forEach(syncPlanFromMeals);
            }

            function syncPlanFromMeals(planCheck) {
              const plan = planCheck.closest('.plan');
              const meals = [...plan.querySelectorAll('.meal-check')];
              planCheck.checked = meals.length > 0 && meals.every(m => m.checked);
              planCheck.indeterminate = meals.some(m => m.checked) && !planCheck.checked;
            }

            function renderSections(sessions) {
              if (!sessions.length) {
                sectionsEl.innerHTML = '<p class="hint">No menu sections found for this school.</p>';
                return;
              }
              sectionsEl.innerHTML = sessions.map((session, si) => {
                const checkedDefault = isDefaultSession(session.servingSession);
                const time = String(session.defaultDisplayTimeHhmm ?? 1130).padStart(4, '0');
                const plans = session.plans.map((plan, pi) => {
                  const meals = plan.mealNames.map(meal =>
                    `<label><input class="meal-check" type="checkbox"${checkedDefault ? ' checked' : ''} data-session="${escapeAttr(session.servingSession)}" data-plan="${escapeAttr(plan.planName)}" value="${escapeAttr(meal)}"> ${escapeHtml(meal)}</label>`
                  ).join('');
                  return `<div class="plan">
                    <label><input class="plan-check" type="checkbox"${checkedDefault ? ' checked' : ''} data-session-index="${si}" data-plan-index="${pi}"> ${escapeHtml(plan.planName)}</label>
                    <div class="meals">${meals}</div>
                  </div>`;
                }).join('');
                return `<section class="session" data-session="${escapeAttr(session.servingSession)}">
                  <div class="session-head">
                    <h2>${escapeHtml(session.servingSession)}</h2>
                    <label class="session-time">Start (HHmm)
                      <input class="session-time-input" type="text" inputmode="numeric" maxlength="4" value="${escapeAttr(time)}" data-session="${escapeAttr(session.servingSession)}">
                    </label>
                  </div>
                  ${plans}
                </section>`;
              }).join('');

              sectionsEl.querySelectorAll('.plan-check').forEach(planCheck => {
                planCheck.addEventListener('change', () => {
                  const plan = planCheck.closest('.plan');
                  plan.querySelectorAll('.meal-check').forEach(m => { m.checked = planCheck.checked; });
                  planCheck.indeterminate = false;
                });
                syncPlanFromMeals(planCheck);
              });
              sectionsEl.querySelectorAll('.meal-check').forEach(mealCheck => {
                mealCheck.addEventListener('change', () => {
                  syncPlanFromMeals(mealCheck.closest('.plan').querySelector('.plan-check'));
                });
              });
            }

            async function loadSchools() {
              const res = await fetch('/api/subscribe/schools');
              if (!res.ok) throw new Error('Failed to load schools');
              const schools = await res.json();
              schoolEl.innerHTML = schools.map(s => {
                const selected =
                  (preferredBuildingId && s.buildingId === preferredBuildingId) ||
                  (preferredSchoolCode && s.shortcut && s.shortcut.toLowerCase() === preferredSchoolCode.toLowerCase())
                    ? ' selected' : '';
                return `<option value="${escapeAttr(s.buildingId)}" data-district="${escapeAttr(s.districtId)}" data-name="${escapeAttr(s.name)}"${selected}>${escapeHtml(s.name)}</option>`;
              }).join('');
              if (!schoolEl.value && schools.length) schoolEl.selectedIndex = 0;
              await loadSections();
            }

            async function loadSections() {
              const opt = schoolEl.selectedOptions[0];
              if (!opt) return;
              sectionsEl.innerHTML = '<p class="hint">Loading sections…</p>';
              statusEl.textContent = '';
              resultEl.classList.remove('show');
              const buildingId = opt.value;
              const districtId = opt.getAttribute('data-district');
              const res = await fetch(`/api/subscribe/sections?buildingId=${encodeURIComponent(buildingId)}&districtId=${encodeURIComponent(districtId)}`);
              if (!res.ok) {
                sectionsEl.innerHTML = '<p class="hint">Could not load sections.</p>';
                return;
              }
              renderSections(await res.json());
            }

            function collectPlans() {
              const byKey = new Map();
              sectionsEl.querySelectorAll('.meal-check:checked').forEach(cb => {
                const servingSession = cb.getAttribute('data-session');
                const planName = cb.getAttribute('data-plan');
                const key = servingSession + '\0' + planName;
                if (!byKey.has(key)) {
                  const timeInput = cb.closest('.session')?.querySelector('.session-time-input');
                  const displayTimeHhmm = parseInt(timeInput ? timeInput.value : '1130', 10);
                  byKey.set(key, {
                    servingSession,
                    planName,
                    displayTimeHhmm: Number.isInteger(displayTimeHhmm) ? displayTimeHhmm : 1130,
                    mealNames: []
                  });
                }
                byKey.get(key).mealNames.push(cb.value);
              });
              return [...byKey.values()];
            }

            async function createLink() {
              const opt = schoolEl.selectedOptions[0];
              if (!opt) return;
              const plans = collectPlans();
              if (!plans.length) {
                statusEl.textContent = 'Select at least one meal section.';
                return;
              }
              for (const plan of plans) {
                if (!Number.isInteger(plan.displayTimeHhmm) || plan.displayTimeHhmm < 0 || plan.displayTimeHhmm > 2359) {
                  statusEl.textContent = `Enter a valid HHmm time for ${plan.servingSession}.`;
                  return;
                }
              }
              const lunchPlan = plans.find(p => String(p.servingSession).toLowerCase().includes('lunch'));
              const displayTimeHhmm = lunchPlan ? lunchPlan.displayTimeHhmm : (parseInt(lunchDefaultTime, 10) || 1130);
              generateBtn.disabled = true;
              statusEl.textContent = 'Creating link…';
              try {
                const res = await fetch('/api/subscribe/create', {
                  method: 'POST',
                  headers: { 'Content-Type': 'application/json' },
                  body: JSON.stringify({
                    buildingId: opt.value,
                    districtId: opt.getAttribute('data-district'),
                    schoolName: opt.getAttribute('data-name'),
                    displayTimeHhmm,
                    plans
                  })
                });
                const body = await res.json();
                if (!res.ok) throw new Error(body.error || 'Create failed');
                document.getElementById('resultUrl').href = body.url;
                document.getElementById('resultUrl').textContent = body.url;
                document.getElementById('resultIcs').href = body.icsUrl;
                document.getElementById('resultIcs').textContent = body.icsUrl;
                resultEl.classList.add('show');
                statusEl.textContent = 'Fastlink ready.';
              } catch (err) {
                statusEl.textContent = err.message || 'Create failed';
              } finally {
                generateBtn.disabled = false;
              }
            }

            function escapeHtml(value) {
              return String(value)
                .replaceAll('&', '&amp;')
                .replaceAll('<', '&lt;')
                .replaceAll('>', '&gt;')
                .replaceAll('"', '&quot;');
            }
            function escapeAttr(value) { return escapeHtml(value); }

            loadSchools().catch(err => {
              schoolEl.innerHTML = '<option value="">Failed to load schools</option>';
              statusEl.textContent = err.message || 'Failed to load schools';
            });
              </script>
            </body>
            </html>
            """);

        return sb.ToString();
    }

    private static string Escape(string value) => WebUtility.HtmlEncode(value);

    private static string ToJsString(string value) =>
        "\"" + value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal) + "\"";
}
