using System.Text;

namespace StVrainToICSFunctionApp.Services;

public static class LandingPageRenderer
{
    public static string Render()
    {
        var sb = new StringBuilder();
        sb.Append("""
            <!DOCTYPE html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>School Menu</title>
              <style>
            """);
        LandingPrefsScript.AppendSharedStyles(sb);
        sb.Append("""
              </style>
            </head>
            <body>
              <div class="wrap">
                <div class="topnav">
                  <a href="/settings" id="settingsLink" class="hidden">Settings</a>
                  <a href="/subscribe">Subscribe to calendar</a>
                </div>

                <section id="onboarding" class="hidden">
                  <h1>Let’s get started</h1>
                  <p class="lede">Add the schools you care about and choose which menu sessions and meal lines to show on your home page.</p>
                  <div class="card">
                    <div id="onboardAdded"></div>
                    <label for="onboardSchool">Add a school</label>
                    <select id="onboardSchool"><option value="">Loading schools…</option></select>
                    <div id="onboardSections" class="sections"></div>
                    <div class="actions">
                      <button type="button" class="btn" id="onboardAdd">Add school</button>
                      <button type="button" class="btn btn-secondary" id="onboardFinish" disabled>Finish</button>
                    </div>
                    <div class="status" id="onboardStatus"></div>
                  </div>
                </section>

                <section id="board" class="hidden">
                  <header style="text-align:center;margin-bottom:1.5rem;">
                    <h1>School Menu</h1>
                    <div class="nav" aria-label="Menu day navigation">
                      <button type="button" class="nav-btn" id="prevDay" aria-label="Previous day">←</button>
                      <div class="nav-date" id="navDate"></div>
                      <button type="button" class="nav-btn" id="nextDay" aria-label="Next day">→</button>
                    </div>
                    <p class="hint hidden" id="skipNote"></p>
                  </header>
                  <div id="boardContent"></div>
                  <div class="status" id="boardStatus"></div>
                </section>
              </div>
              <script>
            """);
        LandingPrefsScript.AppendSharedHelpers(sb);
        sb.Append("""
            const onboardEl = document.getElementById('onboarding');
            const boardEl = document.getElementById('board');
            const settingsLink = document.getElementById('settingsLink');
            const onboardSchool = document.getElementById('onboardSchool');
            const onboardSections = document.getElementById('onboardSections');
            const onboardAdded = document.getElementById('onboardAdded');
            const onboardStatus = document.getElementById('onboardStatus');
            const onboardFinish = document.getElementById('onboardFinish');
            const boardContent = document.getElementById('boardContent');
            const boardStatus = document.getElementById('boardStatus');
            const navDate = document.getElementById('navDate');
            const skipNote = document.getElementById('skipNote');
            const prevDay = document.getElementById('prevDay');
            const nextDay = document.getElementById('nextDay');

            let draftSchools = [];
            let currentDate = new URLSearchParams(location.search).get('date') || null;
            let lastResponse = null;

            function showOnboarding() {
              onboardEl.classList.remove('hidden');
              boardEl.classList.add('hidden');
              settingsLink.classList.add('hidden');
              draftSchools = [];
              renderDraftList();
              loadSchoolOptions();
            }

            function showBoard() {
              onboardEl.classList.add('hidden');
              boardEl.classList.remove('hidden');
              settingsLink.classList.remove('hidden');
              loadBoard();
            }

            function renderDraftList() {
              if (!draftSchools.length) {
                onboardAdded.innerHTML = '<p class="hint">No schools added yet.</p>';
                onboardFinish.disabled = true;
                return;
              }
              onboardAdded.innerHTML = '<ul class="school-list">' + draftSchools.map((s, i) =>
                `<li><span><strong>${escapeHtml(s.name)}</strong> · ${s.plans.length} section(s)</span>
                 <button type="button" class="btn btn-secondary" data-remove="${i}">Remove</button></li>`
              ).join('') + '</ul>';
              onboardFinish.disabled = false;
              onboardAdded.querySelectorAll('[data-remove]').forEach(btn => {
                btn.addEventListener('click', () => {
                  draftSchools.splice(Number(btn.getAttribute('data-remove')), 1);
                  renderDraftList();
                });
              });
            }

            async function loadSchoolOptions() {
              try {
                const schools = await fetchSchools();
                const taken = new Set(draftSchools.map(s => s.buildingId));
                onboardSchool.innerHTML = schools
                  .filter(s => !taken.has(s.buildingId))
                  .map(s => `<option value="${escapeAttr(s.buildingId)}" data-district="${escapeAttr(s.districtId)}" data-name="${escapeAttr(s.name)}">${escapeHtml(s.name)}</option>`)
                  .join('') || '<option value="">All selected schools are already added</option>';
                if (onboardSchool.value) await loadOnboardSections();
              } catch (err) {
                onboardStatus.textContent = err.message || 'Failed to load schools';
              }
            }

            async function loadOnboardSections() {
              const opt = onboardSchool.selectedOptions[0];
              if (!opt || !opt.value) {
                onboardSections.innerHTML = '';
                return;
              }
              onboardSections.innerHTML = '<p class="hint">Loading sections…</p>';
              try {
                const sessions = await fetchSections(opt.value, opt.getAttribute('data-district'));
                renderSessionSections(onboardSections, sessions, null);
              } catch (err) {
                onboardSections.innerHTML = `<p class="hint">${escapeHtml(err.message)}</p>`;
              }
            }

            onboardSchool.addEventListener('change', loadOnboardSections);
            document.getElementById('onboardAdd').addEventListener('click', () => {
              const opt = onboardSchool.selectedOptions[0];
              if (!opt?.value) return;
              const plans = collectPlansFrom(onboardSections);
              if (!plans.length) {
                onboardStatus.textContent = 'Select at least one meal for this school.';
                return;
              }
              draftSchools.push({
                buildingId: opt.value,
                districtId: opt.getAttribute('data-district'),
                name: opt.getAttribute('data-name'),
                plans
              });
              onboardStatus.textContent = `Added ${opt.getAttribute('data-name')}.`;
              renderDraftList();
              loadSchoolOptions();
              onboardSections.innerHTML = '';
            });
            onboardFinish.addEventListener('click', () => {
              if (!draftSchools.length) return;
              savePrefs({ version: 1, schools: draftSchools });
              showBoard();
            });

            function renderDayBlock(day, prefs) {
              if (!day) return '';
              const prefsByBuilding = new Map((prefs.schools || []).map(s => [s.buildingId, s]));
              const cards = (day.schools || []).map(school => {
                const filtered = filterSchoolDay(school, prefsByBuilding.get(school.buildingId));
                const bySession = new Map();
                filtered.meals.forEach(m => {
                  if (!bySession.has(m.servingSession)) bySession.set(m.servingSession, []);
                  bySession.get(m.servingSession).push(m);
                });
                let body;
                if (!filtered.meals.length) {
                  body = '<p class="empty">No selected menu items for this day.</p>';
                } else {
                  body = [...bySession.entries()].map(([session, meals]) => {
                    const prefsSchool = prefsByBuilding.get(school.buildingId);
                    const timePlan = (prefsSchool?.plans || []).find(p => p.servingSession === session);
                    const timeLabel = timePlan ? formatTime(timePlan.displayTimeHhmm) : '';
                    const groups = meals.map(m =>
                      `<div class="meal-group"><strong>${escapeHtml(m.title)}</strong><ul>${m.items.map(i => `<li>${escapeHtml(i)}</li>`).join('')}</ul></div>`
                    ).join('');
                    return `<div class="session-block"><h4>${escapeHtml(session)}${timeLabel ? ' · ' + escapeHtml(timeLabel) : ''}</h4>${groups}</div>`;
                  }).join('');
                }
                return `<article class="school-card">
                  <div class="school-head">
                    <h3>${escapeHtml(school.name)}</h3>
                    <a href="/subscribe?buildingId=${encodeURIComponent(school.buildingId)}">Subscribe</a>
                  </div>
                  <div class="school-body">${body}</div>
                </article>`;
              }).join('');
              return `<section class="day-block">
                <div class="day-heading"><h2>${escapeHtml(day.relativeLabel)}</h2><span class="when">${escapeHtml(day.dateLabel)}</span></div>
                <div class="grid">${cards}</div>
              </section>`;
            }

            async function loadBoard() {
              const prefs = loadPrefs();
              if (!prefs.schools.length) {
                showOnboarding();
                return;
              }
              boardStatus.textContent = 'Loading menus…';
              boardContent.innerHTML = '';
              try {
                lastResponse = await fetchLandingDay(currentDate, prefs.schools);
                currentDate = lastResponse.primary?.date || currentDate;
                navDate.textContent = lastResponse.primary?.dateLabel || '';
                skipNote.textContent = lastResponse.isShowingNextAvailable
                  ? 'No menu today — showing the next published day.'
                  : '';
                skipNote.classList.toggle('hidden', !lastResponse.isShowingNextAvailable);
                prevDay.disabled = !lastResponse.previousDate;
                nextDay.disabled = !lastResponse.nextDate;
                boardContent.innerHTML =
                  renderDayBlock(lastResponse.primary, prefs) +
                  renderDayBlock(lastResponse.upcoming, prefs);
                boardStatus.textContent = '';
                const url = new URL(location.href);
                if (currentDate) url.searchParams.set('date', currentDate);
                else url.searchParams.delete('date');
                history.replaceState({}, '', url);
              } catch (err) {
                boardStatus.textContent = (err.message || 'Failed to load') + ' — try Settings.';
              }
            }

            prevDay.addEventListener('click', () => {
              if (!lastResponse?.previousDate) return;
              currentDate = lastResponse.previousDate;
              loadBoard();
            });
            nextDay.addEventListener('click', () => {
              if (!lastResponse?.nextDate) return;
              currentDate = lastResponse.nextDate;
              loadBoard();
            });

            const prefs = loadPrefs();
            if (!prefs.schools.length) showOnboarding();
            else showBoard();
              </script>
            </body>
            </html>
            """);
        return sb.ToString();
    }
}
