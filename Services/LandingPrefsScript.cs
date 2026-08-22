using System.Text;

namespace StVrainToICSFunctionApp.Services;

/// <summary>Shared localStorage prefs + school section editor used by landing and settings.</summary>
public static class LandingPrefsScript
{
    public const string StorageKey = "stvrain.landingPrefs.v1";

    public static void AppendSharedHelpers(StringBuilder sb)
    {
        sb.Append("""
            const PREFS_KEY = 'stvrain.landingPrefs.v1';

            function loadPrefs() {
              try {
                const raw = localStorage.getItem(PREFS_KEY);
                if (!raw) return { version: 1, schools: [] };
                const parsed = JSON.parse(raw);
                if (!parsed || !Array.isArray(parsed.schools)) return { version: 1, schools: [] };
                return { version: 1, schools: parsed.schools };
              } catch {
                return { version: 1, schools: [] };
              }
            }

            function savePrefs(prefs) {
              localStorage.setItem(PREFS_KEY, JSON.stringify({ version: 1, schools: prefs.schools || [] }));
            }

            function clearPrefs() {
              localStorage.removeItem(PREFS_KEY);
            }

            function escapeHtml(value) {
              return String(value)
                .replaceAll('&', '&amp;')
                .replaceAll('<', '&lt;')
                .replaceAll('>', '&gt;')
                .replaceAll('"', '&quot;');
            }
            function escapeAttr(value) { return escapeHtml(value); }

            function isDefaultSession(name) {
              const n = String(name || '').toLowerCase();
              return n.includes('breakfast') || n.includes('lunch');
            }

            function formatTime(hhmm) {
              const n = Number(hhmm);
              if (!Number.isInteger(n)) return '';
              const h = Math.floor(n / 100);
              const m = n % 100;
              if (h < 0 || h > 23 || m < 0 || m > 59) return String(hhmm);
              const d = new Date(2000, 0, 1, h, m, 0);
              return d.toLocaleTimeString('en-US', { hour: 'numeric', minute: '2-digit' });
            }

            function syncPlanFromMeals(planCheck) {
              const plan = planCheck.closest('.plan');
              const meals = [...plan.querySelectorAll('.meal-check')];
              planCheck.checked = meals.length > 0 && meals.every(m => m.checked);
              planCheck.indeterminate = meals.some(m => m.checked) && !planCheck.checked;
            }

            function wireSectionChecks(root) {
              root.querySelectorAll('.plan-check').forEach(planCheck => {
                planCheck.addEventListener('change', () => {
                  const plan = planCheck.closest('.plan');
                  plan.querySelectorAll('.meal-check').forEach(m => { m.checked = planCheck.checked; });
                  planCheck.indeterminate = false;
                });
                syncPlanFromMeals(planCheck);
              });
              root.querySelectorAll('.meal-check').forEach(mealCheck => {
                mealCheck.addEventListener('change', () => {
                  syncPlanFromMeals(mealCheck.closest('.plan').querySelector('.plan-check'));
                });
              });
            }

            function renderSessionSections(container, sessions, preselectedPlans) {
              const selected = new Map();
              (preselectedPlans || []).forEach(p => {
                selected.set((p.servingSession || '') + '\0' + (p.planName || ''), new Set(p.mealNames || []));
              });
              const timeBySession = new Map();
              (preselectedPlans || []).forEach(p => {
                if (p.displayTimeHhmm != null) timeBySession.set(p.servingSession, p.displayTimeHhmm);
              });

              if (!sessions.length) {
                container.innerHTML = '<p class="hint">No menu sections found for this school.</p>';
                return;
              }

              container.innerHTML = sessions.map(session => {
                const hasPref = [...selected.keys()].some(k => k.startsWith(session.servingSession + '\0'));
                const checkedDefault = hasPref || (!preselectedPlans?.length && isDefaultSession(session.servingSession));
                const time = String(timeBySession.get(session.servingSession) ?? session.defaultDisplayTimeHhmm ?? 1130).padStart(4, '0');
                const plans = session.plans.map(plan => {
                  const key = session.servingSession + '\0' + plan.planName;
                  const mealSet = selected.get(key);
                  const meals = plan.mealNames.map(meal => {
                    const on = mealSet ? mealSet.has(meal) : checkedDefault;
                    return `<label><input class="meal-check" type="checkbox"${on ? ' checked' : ''} data-session="${escapeAttr(session.servingSession)}" data-plan="${escapeAttr(plan.planName)}" value="${escapeAttr(meal)}"> ${escapeHtml(meal)}</label>`;
                  }).join('');
                  return `<div class="plan">
                    <label><input class="plan-check" type="checkbox"> ${escapeHtml(plan.planName)}</label>
                    <div class="meals">${meals}</div>
                  </div>`;
                }).join('');
                return `<section class="session" data-session="${escapeAttr(session.servingSession)}">
                  <div class="session-head">
                    <h3>${escapeHtml(session.servingSession)}</h3>
                    <label class="session-time">Start (HHmm)
                      <input class="session-time-input" type="text" inputmode="numeric" maxlength="4" value="${escapeAttr(time)}">
                    </label>
                  </div>
                  ${plans}
                </section>`;
              }).join('');
              wireSectionChecks(container);
            }

            function collectPlansFrom(container) {
              const byKey = new Map();
              container.querySelectorAll('.meal-check:checked').forEach(cb => {
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

            function mealAllowed(prefsSchool, meal) {
              if (!prefsSchool?.plans?.length) return true;
              return prefsSchool.plans.some(p =>
                String(p.servingSession).toLowerCase() === String(meal.servingSession).toLowerCase()
                && String(p.planName).toLowerCase() === String(meal.planName).toLowerCase()
                && (p.mealNames || []).some(n => String(n).toLowerCase() === String(meal.title).toLowerCase())
              );
            }

            function filterSchoolDay(schoolDay, prefsSchool) {
              return {
                ...schoolDay,
                meals: (schoolDay.meals || []).filter(m => mealAllowed(prefsSchool, m))
              };
            }

            async function fetchSchools() {
              const res = await fetch('/api/subscribe/schools');
              if (!res.ok) throw new Error('Failed to load schools');
              return res.json();
            }

            async function fetchSections(buildingId, districtId) {
              const res = await fetch(`/api/subscribe/sections?buildingId=${encodeURIComponent(buildingId)}&districtId=${encodeURIComponent(districtId)}`);
              if (!res.ok) throw new Error('Failed to load sections');
              return res.json();
            }

            async function fetchLandingDay(date, schools) {
              const res = await fetch('/api/landing/day', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                  date: date || null,
                  schools: schools.map(s => ({
                    buildingId: s.buildingId,
                    districtId: s.districtId,
                    name: s.name
                  }))
                })
              });
              if (!res.ok) {
                const body = await res.json().catch(() => ({}));
                throw new Error(body.error || 'Failed to load menus');
              }
              return res.json();
            }
            """);
    }

    public static void AppendSharedStyles(StringBuilder sb)
    {
        sb.Append("""
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
            .wrap { max-width: 1100px; margin: 0 auto; padding: 2rem 1.25rem 3rem; }
            h1 { margin: 0 0 0.35rem; font-size: clamp(1.6rem, 4vw, 2.4rem); letter-spacing: -0.02em; }
            .lede { color: var(--muted); margin: 0 0 1.25rem; }
            .topnav { display: flex; gap: 1rem; flex-wrap: wrap; margin-bottom: 1rem; }
            .topnav a { color: var(--accent); font-weight: 600; text-decoration: none; }
            .topnav a:hover { text-decoration: underline; }
            .card {
              background: var(--card);
              border: 1px solid var(--line);
              border-radius: 18px;
              box-shadow: var(--shadow);
              padding: 1.25rem 1.35rem;
            }
            label { display: block; font-weight: 600; margin: 1rem 0 0.4rem; }
            select, input[type="text"] {
              width: 100%;
              padding: 0.65rem 0.75rem;
              border: 1px solid var(--line);
              border-radius: 10px;
              font: inherit;
            }
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
            .session-head h3 { margin: 0; font-size: 1.05rem; }
            .session-time {
              display: flex;
              align-items: center;
              gap: 0.4rem;
              font-weight: 500;
              color: var(--muted);
              font-size: 0.92rem;
              margin: 0;
            }
            .session-time input { width: 5.5rem; padding: 0.4rem 0.5rem; }
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
            .btn-danger {
              background: transparent;
              color: #b91c1c;
              border: 1px solid #b91c1c;
            }
            .hint { color: var(--muted); font-size: 0.9rem; margin: 0.35rem 0 0; }
            .status { margin-top: 0.75rem; color: var(--muted); min-height: 1.2rem; }
            .hidden { display: none !important; }
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
              cursor: pointer;
            }
            .nav-btn:disabled, .nav-btn[aria-disabled="true"] {
              opacity: 0.35;
              pointer-events: none;
            }
            .nav-date { min-width: min(18rem, 70vw); text-align: center; color: var(--muted); }
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
            .school-head h3 { margin: 0 0 0.35rem; font-size: 1.2rem; }
            .school-body { padding: 1rem 1.25rem 1.25rem; flex: 1; }
            .session-block + .session-block { margin-top: 1rem; padding-top: 1rem; border-top: 1px dashed var(--line); }
            .session-block h4 { margin: 0 0 0.5rem; font-size: 0.95rem; color: var(--accent); }
            .meal-group + .meal-group { margin-top: 0.65rem; }
            .meal-group strong { display: block; margin-bottom: 0.25rem; }
            .meal-group ul { margin: 0; padding-left: 1.15rem; }
            .empty { color: var(--muted); font-style: italic; }
            .school-list { list-style: none; padding: 0; margin: 0; }
            .school-list li {
              display: flex;
              flex-wrap: wrap;
              gap: 0.5rem;
              align-items: center;
              justify-content: space-between;
              padding: 0.85rem 0;
              border-bottom: 1px solid var(--line);
            }
            .day-block + .day-block { margin-top: 2rem; }
            .day-heading { display: flex; flex-wrap: wrap; gap: 0.5rem 0.85rem; align-items: baseline; margin: 0 0 1rem; }
            .day-heading h2 { margin: 0; font-size: 1.2rem; }
            .day-heading .when { color: var(--muted); font-size: 0.95rem; }
            """);
    }
}
