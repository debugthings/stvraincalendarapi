using System.Text;

namespace StVrainToICSFunctionApp.Services;

public static class SettingsPageRenderer
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
              <title>Settings — School Menu</title>
              <style>
            """);
        LandingPrefsScript.AppendSharedStyles(sb);
        sb.Append("""
              </style>
            </head>
            <body>
              <div class="wrap">
                <div class="topnav">
                  <a href="/">← Back to menu</a>
                  <a href="/subscribe">Subscribe to calendar</a>
                </div>
                <h1>Settings</h1>
                <p class="lede">Choose which schools and meal lines appear on your home page. Preferences stay in this browser.</p>

                <div class="card" style="margin-bottom:1.25rem;">
                  <h2 style="margin:0 0 0.75rem;font-size:1.15rem;">Your schools</h2>
                  <div id="schoolList"></div>
                  <div class="actions">
                    <button type="button" class="btn btn-danger" id="resetPrefs">Reset preferences</button>
                  </div>
                </div>

                <div class="card">
                  <h2 style="margin:0 0 0.75rem;font-size:1.15rem;" id="editorTitle">Add a school</h2>
                  <label for="settingsSchool">School</label>
                  <select id="settingsSchool"><option value="">Loading…</option></select>
                  <div id="settingsSections"></div>
                  <div class="actions">
                    <button type="button" class="btn" id="saveSchool">Save school</button>
                    <button type="button" class="btn btn-secondary hidden" id="cancelEdit">Cancel edit</button>
                  </div>
                  <div class="status" id="settingsStatus"></div>
                </div>
              </div>
              <script>
            """);
        LandingPrefsScript.AppendSharedHelpers(sb);
        sb.Append("""
            const schoolList = document.getElementById('schoolList');
            const settingsSchool = document.getElementById('settingsSchool');
            const settingsSections = document.getElementById('settingsSections');
            const settingsStatus = document.getElementById('settingsStatus');
            const editorTitle = document.getElementById('editorTitle');
            const cancelEdit = document.getElementById('cancelEdit');
            let editingBuildingId = null;
            let allDirectory = [];

            function renderList() {
              const prefs = loadPrefs();
              if (!prefs.schools.length) {
                schoolList.innerHTML = '<p class="hint">No schools configured. Add one below, or <a href="/">start onboarding</a>.</p>';
                return;
              }
              schoolList.innerHTML = '<ul class="school-list">' + prefs.schools.map(s =>
                `<li>
                  <span><strong>${escapeHtml(s.name)}</strong><br><span class="hint">${(s.plans||[]).length} menu section(s)</span></span>
                  <span style="display:flex;gap:0.4rem;flex-wrap:wrap;">
                    <button type="button" class="btn btn-secondary" data-edit="${escapeAttr(s.buildingId)}">Edit</button>
                    <button type="button" class="btn btn-danger" data-remove="${escapeAttr(s.buildingId)}">Remove</button>
                  </span>
                </li>`
              ).join('') + '</ul>';

              schoolList.querySelectorAll('[data-edit]').forEach(btn => {
                btn.addEventListener('click', () => startEdit(btn.getAttribute('data-edit')));
              });
              schoolList.querySelectorAll('[data-remove]').forEach(btn => {
                btn.addEventListener('click', () => {
                  const prefs = loadPrefs();
                  prefs.schools = prefs.schools.filter(s => s.buildingId !== btn.getAttribute('data-remove'));
                  savePrefs(prefs);
                  if (!prefs.schools.length) {
                    location.href = '/';
                    return;
                  }
                  renderList();
                  fillSchoolSelect();
                  settingsStatus.textContent = 'School removed.';
                });
              });
            }

            function fillSchoolSelect() {
              const prefs = loadPrefs();
              const taken = new Set(prefs.schools.map(s => s.buildingId));
              if (editingBuildingId) taken.delete(editingBuildingId);
              settingsSchool.innerHTML = allDirectory
                .filter(s => !taken.has(s.buildingId) || s.buildingId === editingBuildingId)
                .map(s => {
                  const selected = s.buildingId === editingBuildingId ? ' selected' : '';
                  return `<option value="${escapeAttr(s.buildingId)}" data-district="${escapeAttr(s.districtId)}" data-name="${escapeAttr(s.name)}"${selected}>${escapeHtml(s.name)}</option>`;
                }).join('') || '<option value="">No schools available</option>';
            }

            async function loadSectionsForSelection(preselected) {
              const opt = settingsSchool.selectedOptions[0];
              if (!opt?.value) {
                settingsSections.innerHTML = '';
                return;
              }
              settingsSections.innerHTML = '<p class="hint">Loading sections…</p>';
              try {
                const sessions = await fetchSections(opt.value, opt.getAttribute('data-district'));
                renderSessionSections(settingsSections, sessions, preselected || null);
              } catch (err) {
                settingsSections.innerHTML = `<p class="hint">${escapeHtml(err.message)}</p>`;
              }
            }

            async function startEdit(buildingId) {
              const prefs = loadPrefs();
              const school = prefs.schools.find(s => s.buildingId === buildingId);
              if (!school) return;
              editingBuildingId = buildingId;
              editorTitle.textContent = 'Edit school';
              cancelEdit.classList.remove('hidden');
              fillSchoolSelect();
              settingsSchool.value = buildingId;
              settingsSchool.disabled = true;
              await loadSectionsForSelection(school.plans);
              settingsStatus.textContent = `Editing ${school.name}.`;
            }

            function resetEditor() {
              editingBuildingId = null;
              editorTitle.textContent = 'Add a school';
              cancelEdit.classList.add('hidden');
              settingsSchool.disabled = false;
              fillSchoolSelect();
              loadSectionsForSelection(null);
            }

            settingsSchool.addEventListener('change', () => loadSectionsForSelection(null));
            cancelEdit.addEventListener('click', resetEditor);

            document.getElementById('saveSchool').addEventListener('click', () => {
              const opt = settingsSchool.selectedOptions[0];
              if (!opt?.value) return;
              const plans = collectPlansFrom(settingsSections);
              if (!plans.length) {
                settingsStatus.textContent = 'Select at least one meal.';
                return;
              }
              const prefs = loadPrefs();
              const entry = {
                buildingId: opt.value,
                districtId: opt.getAttribute('data-district'),
                name: opt.getAttribute('data-name'),
                plans
              };
              const idx = prefs.schools.findIndex(s => s.buildingId === entry.buildingId);
              if (idx >= 0) prefs.schools[idx] = entry;
              else prefs.schools.push(entry);
              savePrefs(prefs);
              settingsStatus.textContent = 'Saved.';
              resetEditor();
              renderList();
            });

            document.getElementById('resetPrefs').addEventListener('click', () => {
              if (!confirm('Clear all home-page preferences and start over?')) return;
              clearPrefs();
              location.href = '/';
            });

            (async () => {
              try {
                allDirectory = await fetchSchools();
                renderList();
                fillSchoolSelect();
                await loadSectionsForSelection(null);
              } catch (err) {
                settingsStatus.textContent = err.message || 'Failed to load';
              }
            })();
              </script>
            </body>
            </html>
            """);
        return sb.ToString();
    }
}
