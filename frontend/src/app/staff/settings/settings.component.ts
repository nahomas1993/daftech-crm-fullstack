import { Component, computed, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { AuthService } from '../../core/services/auth.service';
import { SystemConfigurationService } from '../../core/services/system-configuration.service';
import { LocationService } from '../../core/services/location.service';
import { FailureTypeService } from '../../core/services/failure-type.service';
import { SupportTypeService } from '../../core/services/support-type.service';
import { ProductCatalogService } from '../../core/services/product-catalog.service';
import { SurveyQuestionService } from '../../core/services/survey-question.service';
import { LocationType, DurationUnit, TicketCategory, TICKET_CATEGORY_LABELS } from '../../core/models';
import { PASSWORD_STRENGTH_HINT, passwordStrengthError } from '../../core/password-strength';

type SettingsTab = 'password' | 'configuration' | 'appearance' | 'locations' | 'failureTypes' | 'productCatalog' | 'satisfactionSurvey';

@Component({
  selector: 'app-staff-settings',
  standalone: true,
  imports: [FormsModule, DatePipe],
  template: `
    <h1>Settings</h1>
    <p class="text-muted" style="margin-top:0.3rem;">Your account, and — if you're an Admin — how the system behaves.</p>

    <div class="tabs">
      <button class="tab" [class.active]="tab() === 'password'" (click)="tab.set('password')">Change Password</button>
      <button class="tab" [class.active]="tab() === 'appearance'" (click)="tab.set('appearance')">Appearance</button>
      @if (isAdmin()) {
        <button class="tab" [class.active]="tab() === 'configuration'" (click)="tab.set('configuration')">Configuration</button>
        <button class="tab" [class.active]="tab() === 'locations'" (click)="tab.set('locations')">Locations</button>
        <button class="tab" [class.active]="tab() === 'failureTypes'" (click)="tab.set('failureTypes')">Failure Types &amp; Pricing</button>
        <button class="tab" [class.active]="tab() === 'productCatalog'" (click)="tab.set('productCatalog')">Systems/Products</button>
        <button class="tab" [class.active]="tab() === 'satisfactionSurvey'" (click)="tab.set('satisfactionSurvey')">Satisfaction Survey</button>
      }
    </div>

    @if (tab() === 'password') {
      <div class="panel panel-pad section">
        <h3>Change Password</h3>
        <p class="text-muted hint">Update the password you use to sign in.</p>

        <label class="lbl">Current password</label>
        <input type="password" [ngModel]="currentPassword()" (ngModelChange)="currentPassword.set($event)" autocomplete="current-password" />

        <label class="lbl" style="margin-top:0.8rem;">New password</label>
        <input type="password" [ngModel]="newPassword()" (ngModelChange)="newPassword.set($event)" autocomplete="new-password" />

        <label class="lbl" style="margin-top:0.8rem;">Confirm new password</label>
        <input type="password" [ngModel]="confirmPassword()" (ngModelChange)="confirmPassword.set($event)" autocomplete="new-password" (keydown.enter)="savePassword()" />

        <p class="text-muted hint">{{ passwordHint }}</p>

        @if (passwordError()) { <div class="err">{{ passwordError() }}</div> }
        @if (passwordSuccess()) { <div class="ok">Password changed successfully.</div> }

        <button class="btn btn-primary" style="margin-top:1rem;" [disabled]="savingPassword()" (click)="savePassword()">
          {{ savingPassword() ? 'Saving…' : 'Save New Password' }}
        </button>
      </div>
    }

    @if (tab() === 'appearance') {
      <div class="panel panel-pad section">
        <h3>Appearance</h3>
        <p class="text-muted hint">Dark mode is coming soon.</p>
      </div>
    }

    @if (tab() === 'configuration' && isAdmin()) {
      <div class="section">
        @if (loadingConfig()) {
          <p class="text-muted">Loading configuration…</p>
        } @else {
          @for (group of config.byCategory(); track group.category) {
            <div class="panel panel-pad section">
              <h3>{{ group.category }}</h3>
              @for (setting of group.settings; track setting.key) {
                <div class="setting-row">
                  <div class="setting-info">
                    <div class="setting-label">{{ setting.label }}</div>
                    <div class="text-muted setting-desc">{{ setting.description }}</div>
                    @if (setting.updatedAt) {
                      <div class="text-muted setting-meta">
                        Last changed {{ setting.updatedAt | date:'medium' }}{{ setting.updatedByName ? ' by ' + setting.updatedByName : '' }}
                      </div>
                    }
                  </div>
                  <div class="setting-control">
                    @if (setting.valueType === 'bool') {
                      <select [ngModel]="draft(setting.key)" (ngModelChange)="setDraft(setting.key, $event)">
                        <option value="true">On</option>
                        <option value="false">Off</option>
                      </select>
                    } @else {
                      <input
                        type="number"
                        min="0"
                        [ngModel]="draft(setting.key)"
                        (ngModelChange)="setDraft(setting.key, $event)"
                      />
                    }
                  </div>
                </div>
              }
              <button class="btn btn-primary btn-sm" style="margin-top:0.9rem;" [disabled]="savingConfig() || !isDirty(group.category)" (click)="saveCategory(group.category)">
                {{ savingConfig() ? 'Saving…' : 'Save Changes' }}
              </button>
            </div>
          }
          @if (configError()) { <div class="err" style="margin-top:0.5rem;">{{ configError() }}</div> }
          @if (configSuccess()) { <div class="ok" style="margin-top:0.5rem;">Configuration updated.</div> }
        }
      </div>
    }

    @if (tab() === 'locations' && isAdmin()) {
      <div class="section locations-grid">
        @for (group of locationGroups; track group.type) {
          <div class="panel panel-pad">
            <h3>{{ group.label }}</h3>
            <p class="text-muted hint">Options shown in the {{ group.label }} dropdown on {{ group.context }}.</p>

            <div class="add-row">
              <input
                type="text"
                [placeholder]="'Add ' + group.label + '…'"
                [ngModel]="newEntryName(group.type)"
                (ngModelChange)="setNewEntryName(group.type, $event)"
                (keydown.enter)="addEntry(group.type)"
              />
              <button class="btn btn-primary btn-sm" [disabled]="savingLocations()" (click)="addEntry(group.type)">Add</button>
            </div>

            <ul class="entry-list">
              @for (entry of entriesFor(group.type); track entry.id) {
                <li class="entry-row">
                  @if (editingId() === entry.id) {
                    <input
                      type="text"
                      class="edit-input"
                      [ngModel]="editingName()"
                      (ngModelChange)="editingName.set($event)"
                      (keydown.enter)="saveEdit(entry.id)"
                    />
                    <div class="entry-actions">
                      <button class="btn btn-primary btn-sm" [disabled]="savingLocations()" (click)="saveEdit(entry.id)">Save</button>
                      <button class="btn btn-outline btn-sm" (click)="cancelEdit()">Cancel</button>
                    </div>
                  } @else {
                    <span class="entry-name">{{ entry.name }}</span>
                    <div class="entry-actions">
                      <button class="btn btn-outline btn-sm" (click)="startEdit(entry.id, entry.name)">Edit</button>
                      <button class="btn btn-outline btn-sm btn-danger" [disabled]="savingLocations()" (click)="deleteEntry(entry.id)">Delete</button>
                    </div>
                  }
                </li>
              }
              @empty {
                <li class="text-muted" style="padding: 0.6rem 0; font-size: 0.82rem;">No {{ group.label.toLowerCase() }} added yet.</li>
              }
            </ul>
          </div>
        }
        @if (locationsError()) { <div class="err" style="margin-top:0.5rem;">{{ locationsError() }}</div> }
      </div>
    }

    @if (tab() === 'failureTypes' && isAdmin()) {
      <div class="section" style="max-width: 640px;">
        <div class="panel panel-pad">
          <h3>Failure Types &amp; Expected Resolution Time</h3>
          <p class="text-muted hint">
            Define the kinds of failures clients can report, and how long each should take to resolve.
            Clients pick one when submitting a ticket; the on-time/late report uses that ticket's own target
            instead of the general Ticket Workflow target above once it's assigned to a technician.
            The base price is what a client pays for that failure once their free support period has ended —
            the support type fee below is added on top.
          </p>

          <div class="ft-add-row">
            <select [ngModel]="newFtCategory()" (ngModelChange)="newFtCategory.set($event)">
              @for (c of categories; track c) { <option [value]="c">{{ categoryLabel(c) }}</option> }
            </select>
            <input type="text" placeholder="Failure type name…" [ngModel]="newFtName()" (ngModelChange)="newFtName.set($event)" />
            <input type="text" placeholder="Description (optional)…" [ngModel]="newFtDescription()" (ngModelChange)="newFtDescription.set($event)" />
            <input type="number" min="0" step="0.01" placeholder="Base price (ETB)" [ngModel]="newFtBasePrice()" (ngModelChange)="newFtBasePrice.set($event)" />
            <input type="number" min="1" placeholder="Duration" [ngModel]="newFtValue()" (ngModelChange)="newFtValue.set($event)" />
            <select [ngModel]="newFtUnit()" (ngModelChange)="newFtUnit.set($event)">
              <option value="Hours">Hour(s)</option>
              <option value="Days">Day(s)</option>
              <option value="Months">Month(s)</option>
            </select>
            <button class="btn btn-primary btn-sm" [disabled]="savingFailureTypes()" (click)="addFailureType()">Add</button>
          </div>

          <ul class="entry-list">
            @for (ft of failureTypes.types(); track ft.id) {
              <li class="entry-row">
                @if (editingFtId() === ft.id) {
                  <div class="ft-edit-row">
                    <select [ngModel]="editingFtCategory()" (ngModelChange)="editingFtCategory.set($event)">
                      @for (c of categories; track c) { <option [value]="c">{{ categoryLabel(c) }}</option> }
                    </select>
                    <input type="text" [ngModel]="editingFtName()" (ngModelChange)="editingFtName.set($event)" />
                    <input type="text" placeholder="Description (optional)…" [ngModel]="editingFtDescription()" (ngModelChange)="editingFtDescription.set($event)" />
                    <input type="number" min="0" step="0.01" placeholder="Base price (ETB)" [ngModel]="editingFtBasePrice()" (ngModelChange)="editingFtBasePrice.set($event)" />
                    <input type="number" min="1" [ngModel]="editingFtValue()" (ngModelChange)="editingFtValue.set($event)" />
                    <select [ngModel]="editingFtUnit()" (ngModelChange)="editingFtUnit.set($event)">
                      <option value="Hours">Hour(s)</option>
                      <option value="Days">Day(s)</option>
                      <option value="Months">Month(s)</option>
                    </select>
                  </div>
                  <div class="entry-actions">
                    <button class="btn btn-primary btn-sm" [disabled]="savingFailureTypes()" (click)="saveFtEdit(ft.id)">Save</button>
                    <button class="btn btn-outline btn-sm" (click)="cancelFtEdit()">Cancel</button>
                  </div>
                } @else {
                  <span class="entry-name">
                    {{ categoryLabel(ft.category) }} · {{ ft.name }} — {{ ft.durationValue }} {{ durationUnitLabel(ft.durationValue, ft.durationUnit) }} · {{ ft.basePrice }} ETB base
                    @if (ft.description) { <span class="text-muted"> · {{ ft.description }}</span> }
                  </span>
                  <div class="entry-actions">
                    <button class="btn btn-outline btn-sm" (click)="startFtEdit(ft)">Edit</button>
                    <button class="btn btn-outline btn-sm btn-danger" [disabled]="savingFailureTypes()" (click)="deleteFailureType(ft.id)">Delete</button>
                  </div>
                }
              </li>
            }
            @empty {
              <li class="text-muted" style="padding: 0.6rem 0; font-size: 0.82rem;">No failure types added yet — clients will only see the Category field until you add some.</li>
            }
          </ul>
          @if (failureTypesError()) { <div class="err" style="margin-top:0.5rem;">{{ failureTypesError() }}</div> }
        </div>

        <div class="panel panel-pad" style="margin-top:1.1rem;">
          <h3>Support Types &amp; Additional Fees</h3>
          <p class="text-muted hint">
            The ways you deliver support — remote, on-site, after hours — and what each one adds to the bill.
            On a chargeable ticket the client pays the failure type's base price plus the fee of the support
            type they chose. A zero fee is fine if a support type costs nothing extra.
          </p>

          <div class="ft-add-row">
            <input type="text" placeholder="Support type name…" [ngModel]="newStName()" (ngModelChange)="newStName.set($event)" />
            <input type="text" placeholder="Description (optional)…" [ngModel]="newStDescription()" (ngModelChange)="newStDescription.set($event)" />
            <input type="number" min="0" step="0.01" placeholder="Additional fee (ETB)" [ngModel]="newStFee()" (ngModelChange)="newStFee.set($event)" />
            <button class="btn btn-primary btn-sm" [disabled]="savingSupportTypes()" (click)="addSupportType()">Add</button>
          </div>

          <ul class="entry-list">
            @for (st of supportTypes.types(); track st.id) {
              <li class="entry-row">
                @if (editingStId() === st.id) {
                  <div class="ft-edit-row">
                    <input type="text" [ngModel]="editingStName()" (ngModelChange)="editingStName.set($event)" />
                    <input type="text" placeholder="Description (optional)…" [ngModel]="editingStDescription()" (ngModelChange)="editingStDescription.set($event)" />
                    <input type="number" min="0" step="0.01" [ngModel]="editingStFee()" (ngModelChange)="editingStFee.set($event)" />
                  </div>
                  <div class="entry-actions">
                    <button class="btn btn-primary btn-sm" [disabled]="savingSupportTypes()" (click)="saveStEdit(st.id)">Save</button>
                    <button class="btn btn-outline btn-sm" (click)="cancelStEdit()">Cancel</button>
                  </div>
                } @else {
                  <span class="entry-name">
                    {{ st.name }} — +{{ st.additionalFee }} ETB
                    @if (st.description) { <span class="text-muted"> · {{ st.description }}</span> }
                  </span>
                  <div class="entry-actions">
                    <button class="btn btn-outline btn-sm" (click)="startStEdit(st)">Edit</button>
                    <button class="btn btn-outline btn-sm btn-danger" [disabled]="savingSupportTypes()" (click)="deleteSupportType(st.id)">Delete</button>
                  </div>
                }
              </li>
            }
            @empty {
              <li class="text-muted" style="padding: 0.6rem 0; font-size: 0.82rem;">No support types yet — add some so clients can say how they'd like to be helped.</li>
            }
          </ul>
          @if (supportTypesError()) { <div class="err" style="margin-top:0.5rem;">{{ supportTypesError() }}</div> }
        </div>
      </div>
    }

    @if (tab() === 'productCatalog' && isAdmin()) {
      <div class="section" style="max-width: 640px;">
        <div class="panel panel-pad">
          <h3>Systems/Products</h3>
          <p class="text-muted hint">
            The systems/products DAFTECH deploys for clients (e.g. "Branch POS System", "HR Portal"). Add, rename,
            or retire entries here — no code change needed. Clients pick one when submitting an issue, and
            Admins pick one when adding a system/product to a client's account.
            Removing an entry doesn't delete it outright — it's hidden from new selections but stays intact for
            anything already using it.
          </p>

          <div class="ft-add-row">
            <input type="text" placeholder="System/product name…" [ngModel]="newPcName()" (ngModelChange)="newPcName.set($event)" />
            <input type="text" placeholder="Description (optional)…" [ngModel]="newPcDescription()" (ngModelChange)="newPcDescription.set($event)" />
            <button class="btn btn-primary btn-sm" [disabled]="savingProductCatalog()" (click)="addProductCatalogItem()">Add</button>
          </div>

          <ul class="entry-list">
            @for (p of catalog.adminItems(); track p.id) {
              <li class="entry-row">
                @if (editingPcId() === p.id) {
                  <div class="ft-edit-row">
                    <input type="text" [ngModel]="editingPcName()" (ngModelChange)="editingPcName.set($event)" />
                    <input type="text" placeholder="Description (optional)…" [ngModel]="editingPcDescription()" (ngModelChange)="editingPcDescription.set($event)" />
                    <label style="display:flex; align-items:center; gap:0.4rem; font-weight:600; font-size:0.8rem; color: var(--slate-500);">
                      <input type="checkbox" [ngModel]="editingPcActive()" (ngModelChange)="editingPcActive.set($event)" />
                      <span>Active</span>
                    </label>
                  </div>
                  <div class="entry-actions">
                    <button class="btn btn-primary btn-sm" [disabled]="savingProductCatalog()" (click)="savePcEdit(p.id)">Save</button>
                    <button class="btn btn-outline btn-sm" (click)="cancelPcEdit()">Cancel</button>
                  </div>
                } @else {
                  <span class="entry-name">
                    {{ p.name }}
                    @if (!p.isActive) { <span class="text-muted"> · retired</span> }
                    @if (p.description) { <span class="text-muted"> · {{ p.description }}</span> }
                  </span>
                  <div class="entry-actions">
                    <button class="btn btn-outline btn-sm" (click)="startPcEdit(p)">Edit</button>
                    @if (p.isActive) {
                      <button class="btn btn-outline btn-sm btn-danger" [disabled]="savingProductCatalog()" (click)="deleteProductCatalogItem(p.id)">Remove</button>
                    }
                  </div>
                }
              </li>
            }
            @empty {
              <li class="text-muted" style="padding: 0.6rem 0; font-size: 0.82rem;">No systems/products added yet — clients won't be able to select one until you add some.</li>
            }
          </ul>
          @if (productCatalogError()) { <div class="err" style="margin-top:0.5rem;">{{ productCatalogError() }}</div> }
        </div>
      </div>
    }

    @if (tab() === 'satisfactionSurvey' && isAdmin()) {
      <div class="section" style="max-width: 640px;">
        <div class="panel panel-pad">
          <h3>Satisfaction Survey Questions</h3>
          <p class="text-muted hint">
            Write the questions clients answer after a ticket is resolved. Each is rated on a 1-5 scale
            (1-Poor, 2-Satisfactory, 3-Good, 4-Very good, 5-Excellent). Clients also get a free-text box
            to describe their experience in their own words — that part isn't configurable here.
            Drag using the arrows to reorder; inactive questions stop appearing on new surveys but stay
            on past ones already submitted.
          </p>

          <div class="ft-add-row" style="grid-template-columns: 1fr auto;">
            <input
              type="text"
              placeholder="New survey question…"
              [ngModel]="newQuestionText()"
              (ngModelChange)="newQuestionText.set($event)"
              (keydown.enter)="addQuestion()"
            />
            <button class="btn btn-primary btn-sm" [disabled]="savingQuestions()" (click)="addQuestion()">Add</button>
          </div>

          <ul class="entry-list" style="max-height: none;">
            @for (q of surveyQuestions.questions(); track q.id; let i = $index; let first = $first; let last = $last) {
              <li class="entry-row">
                @if (editingQuestionId() === q.id) {
                  <input
                    type="text"
                    class="edit-input"
                    [ngModel]="editingQuestionText()"
                    (ngModelChange)="editingQuestionText.set($event)"
                    (keydown.enter)="saveQuestionEdit(q.id)"
                  />
                  <div class="entry-actions">
                    <button class="btn btn-primary btn-sm" [disabled]="savingQuestions()" (click)="saveQuestionEdit(q.id)">Save</button>
                    <button class="btn btn-outline btn-sm" (click)="cancelQuestionEdit()">Cancel</button>
                  </div>
                } @else {
                  <span class="entry-name">
                    {{ q.text }}
                    @if (!q.isActive) { <span class="text-muted"> · inactive</span> }
                  </span>
                  <div class="entry-actions">
                    <button class="btn btn-outline btn-sm" [disabled]="savingQuestions() || first" (click)="moveQuestion(i, -1)">↑</button>
                    <button class="btn btn-outline btn-sm" [disabled]="savingQuestions() || last" (click)="moveQuestion(i, 1)">↓</button>
                    <button class="btn btn-outline btn-sm" (click)="startQuestionEdit(q.id, q.text)">Edit</button>
                    <button class="btn btn-outline btn-sm" [disabled]="savingQuestions()" (click)="toggleQuestionActive(q)">{{ q.isActive ? 'Deactivate' : 'Activate' }}</button>
                    <button class="btn btn-outline btn-sm btn-danger" [disabled]="savingQuestions()" (click)="deleteQuestion(q.id)">Delete</button>
                  </div>
                }
              </li>
            }
            @empty {
              <li class="text-muted" style="padding: 0.6rem 0; font-size: 0.82rem;">No survey questions yet — clients won't see a survey until you add at least one.</li>
            }
          </ul>
          @if (questionsError()) { <div class="err" style="margin-top:0.5rem;">{{ questionsError() }}</div> }
        </div>
      </div>
    }
  `,
  styles: [`
    .tabs { display: flex; gap: 0.4rem; margin: 1.25rem 0 1.1rem; border-bottom: 1px solid var(--slate-200); flex-wrap: wrap; }
    .tab {
      background: none; padding: 0.6rem 0.9rem; font-size: 0.85rem; font-weight: 600;
      color: var(--slate-500); border-bottom: 2px solid transparent; border-radius: 0;
    }
    .tab.active { color: var(--accent); border-bottom-color: var(--accent); }
    .section { margin-bottom: 1.1rem; max-width: 640px; }
    .section h3 { margin-bottom: 0.2rem; }
    .lbl { display: block; font-size: 0.78rem; font-weight: 600; color: var(--slate-500); margin-bottom: 0.3rem; }
    input, select { width: 100%; }
    .hint { font-size: 0.78rem; margin: 0.3rem 0 0.8rem; }
    .err { margin-top: 0.9rem; padding: 0.65rem 0.8rem; border-radius: 8px; background: var(--red-bg); color: var(--red); font-size: 0.83rem; }
    .ok { margin-top: 0.9rem; padding: 0.65rem 0.8rem; border-radius: 8px; background: var(--green-bg); color: var(--green); font-size: 0.83rem; }

    .setting-row {
      display: flex; align-items: flex-start; justify-content: space-between; gap: 1rem;
      padding: 0.85rem 0; border-top: 1px solid var(--slate-100);
    }
    .setting-row:first-of-type { border-top: none; padding-top: 0.4rem; }
    .setting-info { flex: 1; min-width: 0; }
    .setting-label { font-size: 0.88rem; font-weight: 600; color: var(--navy-900); }
    .setting-desc { font-size: 0.78rem; margin-top: 0.15rem; line-height: 1.4; }
    .setting-meta { font-size: 0.7rem; margin-top: 0.3rem; }
    .setting-control { width: 110px; flex-shrink: 0; }

    .locations-grid { max-width: none; display: grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); gap: 1rem; }
    .add-row { display: flex; gap: 0.5rem; margin-bottom: 0.8rem; }
    .add-row input { flex: 1; }
    .entry-list { list-style: none; padding: 0; margin: 0; max-height: 320px; overflow-y: auto; }
    .entry-row {
      display: flex; align-items: center; justify-content: space-between; gap: 0.6rem;
      padding: 0.55rem 0; border-top: 1px solid var(--slate-100);
    }
    .entry-row:first-of-type { border-top: none; }
    .entry-name { font-size: 0.85rem; color: var(--navy-900); }
    .entry-actions { display: flex; gap: 0.4rem; flex-shrink: 0; }
    .edit-input { width: auto; flex: 1; }
    .btn-danger { color: var(--red); border-color: var(--red); }

    .ft-add-row { display: grid; grid-template-columns: 1fr 1.4fr 1.6fr 0.8fr 0.8fr auto; gap: 0.5rem; margin-bottom: 0.9rem; }
    .ft-edit-row { display: grid; grid-template-columns: 1fr 1.4fr 1.6fr 0.8fr 0.8fr; gap: 0.4rem; flex: 1; }
  `],
})
export class SettingsComponent implements OnInit {
  tab = signal<SettingsTab>('password');

  // --- Password tab ---
  currentPassword = signal('');
  newPassword = signal('');
  confirmPassword = signal('');
  savingPassword = signal(false);
  passwordError = signal<string | null>(null);
  passwordSuccess = signal(false);
  readonly passwordHint = PASSWORD_STRENGTH_HINT;

  // --- Configuration tab ---
  loadingConfig = signal(true);
  savingConfig = signal(false);
  configError = signal<string | null>(null);
  configSuccess = signal(false);
  private drafts = signal<Record<string, string>>({});

  // --- Locations tab ---
  readonly locationGroups: { type: LocationType; label: string; context: string }[] = [
    { type: 'Region', label: 'Regions', context: 'client registration' },
    { type: 'Zone', label: 'Zones', context: 'client registration' },
    { type: 'City', label: 'Cities', context: 'client registration' },
    { type: 'Woreda', label: 'Woredas', context: 'client registration' },
    { type: 'Specialization', label: 'Specializations', context: 'the employee form' },
    { type: 'CustomRole', label: 'Additional Roles', context: 'the employee form' },
  ];
  savingLocations = signal(false);
  locationsError = signal<string | null>(null);
  private newEntryNames = signal<Record<LocationType, string>>({ Region: '', Zone: '', City: '', Woreda: '', Specialization: '', CustomRole: '' });
  editingId = signal<string | null>(null);
  editingName = signal('');

  isAdmin = computed(() => this.auth.currentEmployee()?.roles.includes('Admin') ?? false);

  constructor(public auth: AuthService, public config: SystemConfigurationService, public locations: LocationService, public failureTypes: FailureTypeService, public supportTypes: SupportTypeService, public catalog: ProductCatalogService, public surveyQuestions: SurveyQuestionService) {}

  async ngOnInit() {
    if (this.isAdmin()) {
      try {
        await this.config.refresh();
        this.resetDrafts();
        await this.surveyQuestions.refresh();
        await this.catalog.refreshForAdmin();
      } finally {
        this.loadingConfig.set(false);
      }
    } else {
      this.loadingConfig.set(false);
    }
  }

  private resetDrafts() {
    const map: Record<string, string> = {};
    for (const s of this.config.settings()) map[s.key] = s.value;
    this.drafts.set(map);
  }

  draft(key: string): string {
    return this.drafts()[key] ?? '';
  }

  setDraft(key: string, value: unknown) {
    // Number inputs hand back a number (or null when cleared) via ngModel.
    // Settings are stored and sent as strings, so normalize here — sending a
    // raw number made the API reject the payload before it ever reached the
    // controller, which is what "Save Changes" silently failing looked like.
    const normalized = value === null || value === undefined ? '' : String(value);
    this.drafts.update(d => ({ ...d, [key]: normalized }));
    this.configSuccess.set(false);
  }

  isDirty(category: string): boolean {
    const original = this.config.settings().filter(s => s.category === category);
    return original.some(s => this.draft(s.key) !== s.value);
  }

  async saveCategory(category: string) {
    this.configError.set(null);
    this.configSuccess.set(false);

    const changed = this.config.settings()
      .filter(s => s.category === category && this.draft(s.key) !== s.value)
      .map(s => ({ key: s.key, value: this.draft(s.key) }));

    if (changed.length === 0) return;

    this.savingConfig.set(true);
    try {
      await this.config.update(changed);
      this.resetDrafts();
      this.configSuccess.set(true);
    } catch (e: any) {
      this.configError.set(this.extractErrorMessage(e, 'Could not save configuration — please try again.'));
    } finally {
      this.savingConfig.set(false);
    }
  }

  async savePassword() {
    this.passwordError.set(null);
    this.passwordSuccess.set(false);

    if (!this.currentPassword() || !this.newPassword() || !this.confirmPassword()) {
      this.passwordError.set('Please fill in all three fields.');
      return;
    }
    if (this.newPassword() !== this.confirmPassword()) {
      this.passwordError.set('New password and confirmation do not match.');
      return;
    }
    const strengthError = passwordStrengthError(this.newPassword());
    if (strengthError) {
      this.passwordError.set(strengthError);
      return;
    }

    this.savingPassword.set(true);
    try {
      await this.auth.changeEmployeePassword(this.currentPassword(), this.newPassword(), this.confirmPassword());
      this.currentPassword.set('');
      this.newPassword.set('');
      this.confirmPassword.set('');
      this.passwordSuccess.set(true);
    } catch (e: any) {
      this.passwordError.set(e?.error?.text ?? e?.error ?? 'Could not change password — check your current password and try again.');
    } finally {
      this.savingPassword.set(false);
    }
  }

  // --- Locations tab ---

  entriesFor(type: LocationType) {
    const opts = this.locations.options();
    if (type === 'Region') return opts.regions;
    if (type === 'Zone') return opts.zones;
    if (type === 'City') return opts.cities;
    if (type === 'Woreda') return opts.woredas;
    if (type === 'Specialization') return opts.specializations;
    return opts.customRoles;
  }

  newEntryName(type: LocationType): string {
    return this.newEntryNames()[type];
  }

  setNewEntryName(type: LocationType, value: string) {
    this.newEntryNames.update(m => ({ ...m, [type]: value }));
  }

  async addEntry(type: LocationType) {
    const name = this.newEntryNames()[type].trim();
    if (!name) return;

    this.locationsError.set(null);
    this.savingLocations.set(true);
    try {
      await this.locations.create(type, name);
      this.setNewEntryName(type, '');
    } catch (e: any) {
      this.locationsError.set(this.extractErrorMessage(e, 'Could not add this entry — it may already exist.'));
    } finally {
      this.savingLocations.set(false);
    }
  }

  startEdit(id: string, currentName: string) {
    this.editingId.set(id);
    this.editingName.set(currentName);
    this.locationsError.set(null);
  }

  cancelEdit() {
    this.editingId.set(null);
    this.editingName.set('');
  }

  async saveEdit(id: string) {
    const name = this.editingName().trim();
    if (!name) return;

    this.locationsError.set(null);
    this.savingLocations.set(true);
    try {
      await this.locations.update(id, name);
      this.cancelEdit();
    } catch (e: any) {
      this.locationsError.set(this.extractErrorMessage(e, 'Could not save this change — the name may already exist.'));
    } finally {
      this.savingLocations.set(false);
    }
  }

  async deleteEntry(id: string) {
    this.locationsError.set(null);
    this.savingLocations.set(true);
    try {
      await this.locations.remove(id);
    } catch (e: any) {
      this.locationsError.set(this.extractErrorMessage(e, 'Could not delete this entry.'));
    } finally {
      this.savingLocations.set(false);
    }
  }

  /**
   * Angular's HttpErrorResponse.error holds the parsed response body, which
   * can be a plain string (controllers here mostly do BadRequest(ex.Message))
   * or an object — e.g. ASP.NET's built-in [ApiController] model validation
   * returns a ProblemDetails object ({ title, status, errors }) before the
   * action method ever runs. Rendering that object directly in the template
   * produced the "[object Object]" bug. This normalizes either shape to a
   * safe display string.
   */
  private extractErrorMessage(e: any, fallback: string): string {
    const body = e?.error;
    if (typeof body === 'string' && body.trim()) return body;
    if (body && typeof body === 'object') {
      if (typeof body.title === 'string' && body.title.trim()) {
        if (body.errors && typeof body.errors === 'object') {
          const firstField = Object.values(body.errors)[0];
          const firstMessage = Array.isArray(firstField) ? firstField[0] : firstField;
          if (typeof firstMessage === 'string' && firstMessage.trim()) return firstMessage;
        }
        return body.title;
      }
      if (typeof body.message === 'string' && body.message.trim()) return body.message;
    }
    return fallback;
  }

  // --- Failure Types tab ---

  categories: TicketCategory[] = ['Frontend', 'Backend', 'Database'];
  newFtCategory = signal<TicketCategory>('Frontend');
  newFtName = signal('');
  newFtDescription = signal('');
  newFtBasePrice = signal<number>(0);
  newFtValue = signal<number>(1);
  newFtUnit = signal<DurationUnit>('Days');
  savingFailureTypes = signal(false);
  failureTypesError = signal<string | null>(null);
  editingFtId = signal<string | null>(null);
  editingFtCategory = signal<TicketCategory>('Frontend');
  editingFtName = signal('');
  editingFtDescription = signal('');
  editingFtBasePrice = signal<number>(0);
  editingFtValue = signal<number>(1);
  editingFtUnit = signal<DurationUnit>('Days');

  categoryLabel(category: TicketCategory): string { return TICKET_CATEGORY_LABELS[category]; }

  /** Singular/plural unit word matching the actual duration value — "1 hour" vs "2 hours". */
  durationUnitLabel(value: number, unit: DurationUnit): string {
    const singular = unit.toLowerCase().replace(/s$/, '');
    return value === 1 ? singular : `${singular}s`;
  }

  async addFailureType() {
    const name = this.newFtName().trim();
    if (!name || !this.newFtValue() || this.newFtValue() <= 0) return;

    this.failureTypesError.set(null);
    this.savingFailureTypes.set(true);
    try {
      await this.failureTypes.create(this.newFtCategory(), name, this.newFtValue(), this.newFtUnit(), this.newFtDescription().trim() || undefined, Number(this.newFtBasePrice()) || 0);
      this.newFtName.set('');
      this.newFtDescription.set('');
      this.newFtValue.set(1);
      this.newFtUnit.set('Days');
      this.newFtBasePrice.set(0);
    } catch (e: any) {
      this.failureTypesError.set(e?.error ?? 'Could not add this failure type — the name may already exist.');
    } finally {
      this.savingFailureTypes.set(false);
    }
  }

  startFtEdit(ft: { id: string; category: TicketCategory; name: string; description?: string; basePrice: number; durationValue: number; durationUnit: DurationUnit }) {
    this.editingFtId.set(ft.id);
    this.editingFtCategory.set(ft.category);
    this.editingFtName.set(ft.name);
    this.editingFtDescription.set(ft.description ?? '');
    this.editingFtBasePrice.set(ft.basePrice ?? 0);
    this.editingFtValue.set(ft.durationValue);
    this.editingFtUnit.set(ft.durationUnit);
    this.failureTypesError.set(null);
  }

  cancelFtEdit() {
    this.editingFtId.set(null);
  }

  async saveFtEdit(id: string) {
    const name = this.editingFtName().trim();
    if (!name || !this.editingFtValue() || this.editingFtValue() <= 0) return;

    this.failureTypesError.set(null);
    this.savingFailureTypes.set(true);
    try {
      await this.failureTypes.update(id, this.editingFtCategory(), name, this.editingFtValue(), this.editingFtUnit(), this.editingFtDescription().trim() || undefined, Number(this.editingFtBasePrice()) || 0);
      this.cancelFtEdit();
    } catch (e: any) {
      this.failureTypesError.set(e?.error ?? 'Could not save this change — the name may already exist.');
    } finally {
      this.savingFailureTypes.set(false);
    }
  }

  async deleteFailureType(id: string) {
    this.failureTypesError.set(null);
    this.savingFailureTypes.set(true);
    try {
      await this.failureTypes.remove(id);
    } catch (e: any) {
      this.failureTypesError.set(e?.error ?? 'Could not delete this failure type.');
    } finally {
      this.savingFailureTypes.set(false);
    }
  }

  // --- Support Types (same tab as failure types) ---

  newStName = signal('');
  newStDescription = signal('');
  newStFee = signal<number>(0);
  savingSupportTypes = signal(false);
  supportTypesError = signal<string | null>(null);
  editingStId = signal<string | null>(null);
  editingStName = signal('');
  editingStDescription = signal('');
  editingStFee = signal<number>(0);

  async addSupportType() {
    const name = this.newStName().trim();
    if (!name) return;

    this.supportTypesError.set(null);
    this.savingSupportTypes.set(true);
    try {
      await this.supportTypes.create(name, Number(this.newStFee()) || 0, this.newStDescription().trim() || undefined);
      this.newStName.set('');
      this.newStDescription.set('');
      this.newStFee.set(0);
    } catch (e: any) {
      this.supportTypesError.set(e?.error ?? 'Could not add this support type — the name may already exist.');
    } finally {
      this.savingSupportTypes.set(false);
    }
  }

  startStEdit(st: { id: string; name: string; description?: string; additionalFee: number }) {
    this.editingStId.set(st.id);
    this.editingStName.set(st.name);
    this.editingStDescription.set(st.description ?? '');
    this.editingStFee.set(st.additionalFee ?? 0);
    this.supportTypesError.set(null);
  }

  cancelStEdit() {
    this.editingStId.set(null);
  }

  async saveStEdit(id: string) {
    const name = this.editingStName().trim();
    if (!name) return;

    this.supportTypesError.set(null);
    this.savingSupportTypes.set(true);
    try {
      await this.supportTypes.update(id, name, Number(this.editingStFee()) || 0, this.editingStDescription().trim() || undefined);
      this.cancelStEdit();
    } catch (e: any) {
      this.supportTypesError.set(e?.error ?? 'Could not save this change — the name may already exist.');
    } finally {
      this.savingSupportTypes.set(false);
    }
  }

  async deleteSupportType(id: string) {
    this.supportTypesError.set(null);
    this.savingSupportTypes.set(true);
    try {
      await this.supportTypes.remove(id);
    } catch (e: any) {
      this.supportTypesError.set(e?.error ?? 'Could not delete this support type — it may be in use by a ticket.');
    } finally {
      this.savingSupportTypes.set(false);
    }
  }

  // --- Systems/Products catalog ---

  newPcName = signal('');
  newPcDescription = signal('');
  savingProductCatalog = signal(false);
  productCatalogError = signal<string | null>(null);
  editingPcId = signal<string | null>(null);
  editingPcName = signal('');
  editingPcDescription = signal('');
  editingPcActive = signal(true);

  async addProductCatalogItem() {
    const name = this.newPcName().trim();
    if (!name) return;

    this.productCatalogError.set(null);
    this.savingProductCatalog.set(true);
    try {
      await this.catalog.create(name, this.newPcDescription().trim() || undefined);
      this.newPcName.set('');
      this.newPcDescription.set('');
    } catch (e: any) {
      this.productCatalogError.set(e?.error ?? 'Could not add this system/product — the name may already exist.');
    } finally {
      this.savingProductCatalog.set(false);
    }
  }

  startPcEdit(p: { id: string; name: string; description?: string; isActive: boolean }) {
    this.editingPcId.set(p.id);
    this.editingPcName.set(p.name);
    this.editingPcDescription.set(p.description ?? '');
    this.editingPcActive.set(p.isActive);
    this.productCatalogError.set(null);
  }

  cancelPcEdit() {
    this.editingPcId.set(null);
  }

  async savePcEdit(id: string) {
    const name = this.editingPcName().trim();
    if (!name) return;

    this.productCatalogError.set(null);
    this.savingProductCatalog.set(true);
    try {
      await this.catalog.update(id, name, this.editingPcDescription().trim() || undefined, this.editingPcActive());
      this.cancelPcEdit();
    } catch (e: any) {
      this.productCatalogError.set(e?.error ?? 'Could not save this change — the name may already exist.');
    } finally {
      this.savingProductCatalog.set(false);
    }
  }

  async deleteProductCatalogItem(id: string) {
    this.productCatalogError.set(null);
    this.savingProductCatalog.set(true);
    try {
      await this.catalog.remove(id);
    } catch (e: any) {
      this.productCatalogError.set(e?.error ?? 'Could not remove this system/product — please try again.');
    } finally {
      this.savingProductCatalog.set(false);
    }
  }

  // --- Satisfaction Survey tab ---

  newQuestionText = signal('');
  savingQuestions = signal(false);
  questionsError = signal<string | null>(null);
  editingQuestionId = signal<string | null>(null);
  editingQuestionText = signal('');

  async addQuestion() {
    const text = this.newQuestionText().trim();
    if (!text) return;

    this.questionsError.set(null);
    this.savingQuestions.set(true);
    try {
      await this.surveyQuestions.create(text);
      this.newQuestionText.set('');
    } catch (e: any) {
      this.questionsError.set(this.extractErrorMessage(e, 'Could not add this question.'));
    } finally {
      this.savingQuestions.set(false);
    }
  }

  startQuestionEdit(id: string, text: string) {
    this.editingQuestionId.set(id);
    this.editingQuestionText.set(text);
    this.questionsError.set(null);
  }

  cancelQuestionEdit() {
    this.editingQuestionId.set(null);
  }

  async saveQuestionEdit(id: string) {
    const text = this.editingQuestionText().trim();
    if (!text) return;

    const current = this.surveyQuestions.questions().find(q => q.id === id);
    if (!current) return;

    this.questionsError.set(null);
    this.savingQuestions.set(true);
    try {
      await this.surveyQuestions.update(id, text, current.isActive);
      this.cancelQuestionEdit();
    } catch (e: any) {
      this.questionsError.set(this.extractErrorMessage(e, 'Could not save this change.'));
    } finally {
      this.savingQuestions.set(false);
    }
  }

  async toggleQuestionActive(q: { id: string; text: string; isActive: boolean }) {
    this.questionsError.set(null);
    this.savingQuestions.set(true);
    try {
      await this.surveyQuestions.update(q.id, q.text, !q.isActive);
    } catch (e: any) {
      this.questionsError.set(this.extractErrorMessage(e, 'Could not update this question.'));
    } finally {
      this.savingQuestions.set(false);
    }
  }

  async deleteQuestion(id: string) {
    this.questionsError.set(null);
    this.savingQuestions.set(true);
    try {
      await this.surveyQuestions.remove(id);
    } catch (e: any) {
      this.questionsError.set(this.extractErrorMessage(e, 'Could not delete this question.'));
    } finally {
      this.savingQuestions.set(false);
    }
  }

  async moveQuestion(index: number, direction: -1 | 1) {
    const list = [...this.surveyQuestions.questions()];
    const target = index + direction;
    if (target < 0 || target >= list.length) return;

    [list[index], list[target]] = [list[target], list[index]];

    this.questionsError.set(null);
    this.savingQuestions.set(true);
    try {
      await this.surveyQuestions.reorder(list.map(q => q.id));
    } catch (e: any) {
      this.questionsError.set(this.extractErrorMessage(e, 'Could not reorder questions.'));
    } finally {
      this.savingQuestions.set(false);
    }
  }
}
