import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, DestroyRef, ElementRef, inject, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormsModule, NgForm } from '@angular/forms';
import { BehaviorSubject, EMPTY, Subscription, catchError, finalize, forkJoin, of, switchMap } from 'rxjs';
import { Bonus, BonusFilters, Employee, RegisterBonus } from '../../core/models/bonus';
import { BonusStates, MasterStateCodes } from '../../core/models/catalog';
import { PagedResponse } from '../../core/models/paged-response';
import { BonusApi } from '../../core/services/bonus-api';
import { CatalogApi } from '../../core/services/catalog-api';
import { Notification } from '../../core/services/notification';

const emptyFilters = (): BonusFilters => ({ departmentCode: '', employeeNumber: null, period: '', stateCode: '', pageIndex: 1, pageSize: 10 });

@Component({
  selector: 'app-bonuses',
  imports: [FormsModule, DecimalPipe, DatePipe],
  templateUrl: './bonuses.html',
  styleUrls: ['./bonuses.css', './bonuses-dialog.css'],
})
export class Bonuses {
  private readonly api = inject(BonusApi);
  private readonly catalogApi = inject(CatalogApi);
  protected readonly notification = inject(Notification);
  private readonly destroyRef = inject(DestroyRef);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('registrationDialog');
  private readonly registrationForm = viewChild<NgForm>('registrationForm');
  private employeeRequest?: Subscription;
  private readonly queries = new BehaviorSubject<BonusFilters>(emptyFilters());
  private appliedFilters = emptyFilters();

  protected readonly states = BonusStates;
  protected readonly catalogs = toSignal(forkJoin({
    departments: this.catalogApi.getDepartments(),
    states: this.catalogApi.getStates(MasterStateCodes.Bonus),
    types: this.catalogApi.getBonusTypes(),
    reasons: this.catalogApi.getBonusReasons(),
  }).pipe(catchError(() => of(null))), { initialValue: null });
  protected readonly loading = signal(true);
  protected readonly listFailed = signal(false);
  protected readonly page = signal<PagedResponse<Bonus> | null>(null);
  protected readonly employee = signal<Employee | null>(null);
  protected readonly searchingEmployee = signal(false);
  protected readonly saving = signal(false);
  protected readonly actionId = signal<number | null>(null);
  protected readonly dialogOpen = signal(false);
  protected readonly lookupAttempted = signal(false);
  protected filters = emptyFilters();
  protected readonly currentMonth = (() => {
    const parts = new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Lima', year: 'numeric', month: '2-digit' }).formatToParts(new Date());
    return `${parts.find(p => p.type === 'year')!.value}-${parts.find(p => p.type === 'month')!.value}`;
  })();
  protected draft: RegisterBonus = this.emptyDraft();

  constructor() {
    this.destroyRef.onDestroy(() => {
      if (this.dialogOpen()) this.notification.endDialog();
    });
    this.queries.pipe(
      switchMap(filters => {
        this.loading.set(true);
        this.listFailed.set(false);
        return this.api.list(filters).pipe(
          catchError(() => { this.listFailed.set(true); return EMPTY; }),
          finalize(() => this.loading.set(false)),
        );
      }),
      takeUntilDestroyed(),
    ).subscribe(page => {
      this.page.set(page);
      if (!page.data.length && page.pageNumber > 1) this.goToPage(page.pageNumber - 1);
    });
  }

  protected search() {
    this.appliedFilters = { ...this.filters, pageIndex: 1 };
    this.queries.next({ ...this.appliedFilters });
  }

  protected clear() {
    this.filters = emptyFilters();
    this.search();
  }

  protected goToPage(pageIndex: number) {
    if (pageIndex < 1) return;
    this.appliedFilters = { ...this.appliedFilters, pageIndex };
    this.queries.next({ ...this.appliedFilters });
  }

  protected openRegistration() {
    this.notification.beginDialog();
    this.draft = this.emptyDraft();
    this.registrationForm()?.resetForm({ ...this.draft, bonusPeriod: this.currentMonth });
    this.invalidateEmployee();
    this.dialogOpen.set(true);
    this.dialog().nativeElement.showModal();
  }

  protected closeRegistration() {
    if (this.saving()) return;
    this.invalidateEmployee();
    this.dialogOpen.set(false);
    this.dialog().nativeElement.close();
    this.notification.endDialog();
  }

  protected onDialogCancel(event: Event) {
    event.preventDefault();
    this.closeRegistration();
  }

  protected invalidateEmployee() {
    this.employeeRequest?.unsubscribe();
    this.employee.set(null);
    this.lookupAttempted.set(false);
  }

  protected findEmployee() {
    this.invalidateEmployee();
    this.notification.clear();
    this.lookupAttempted.set(true);
    if (!this.draft.departmentCode || !Number.isInteger(this.draft.employeeNumber) || this.draft.employeeNumber! <= 0) return;
    this.searchingEmployee.set(true);
    this.employeeRequest = this.api.employee(this.draft.departmentCode, this.draft.employeeNumber!).pipe(
      finalize(() => this.searchingEmployee.set(false)),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe({ next: employee => this.employee.set(employee), error: () => {} });
  }

  protected get preview() {
    const employee = this.employee();
    const value = this.draft.value;
    if (!employee || !value || value <= 0 || !Number.isFinite(value) || value > this.valueLimit || Math.round(value * 1e4) / 1e4 !== value) return null;
    // Enteros escalados: mismo redondeo decimal hacia arriba que el backend.
    const scaledValue = BigInt(Math.round(value * 10000));
    const salaryCents = BigInt(Math.round(employee.monthlySalary * 100));
    const round = (n: bigint, d: bigint) => (n + d / 2n) / d;
    const gross = this.draft.bonusTypeId === 1 ? round(scaledValue, 100n) : round(salaryCents * scaledValue, 1000000n);
    const tax = round(gross * BigInt(Math.round(employee.taxRate * 100)), 10000n);
    return { gross: Number(gross) / 100, tax: Number(tax) / 100, net: Number(gross - tax) / 100 };
  }

  protected get valueLimit() {
    return this.draft.bonusTypeId === 2 ? 100 : (this.employee()?.monthlySalary ?? 0);
  }

  protected get validPeriod() {
    return /^\d{4}-(0[1-9]|1[0-2])$/.test(this.draft.period) && this.draft.period.slice(0, 4) !== '0000' && this.draft.period <= this.currentMonth;
  }

  protected save(form: NgForm) {
    if (form.invalid || !this.employee() || !this.preview || !this.validPeriod || this.saving()) return;
    this.saving.set(true);
    this.api.register({ ...this.draft }).pipe(
      finalize(() => this.saving.set(false)),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe({
      next: () => {
        this.saving.set(false);
        this.closeRegistration();
        this.notification.show('success', 'El bono se registró correctamente.');
        this.goToPage(1);
      },
      error: () => {},
    });
  }

  protected act(bonus: Bonus, action: 'cancel' | 'delete') {
    if (this.actionId() !== null || bonus.stateCode !== BonusStates.Registered) return;
    const verb = action === 'cancel' ? 'anular' : 'eliminar';
    if (!window.confirm(`¿Deseas ${verb} el bono de ${bonus.fullName} del periodo ${bonus.period}?`)) return;
    this.actionId.set(bonus.bonusId);
    const request = action === 'cancel' ? this.api.cancel(bonus.bonusId) : this.api.delete(bonus.bonusId);
    request.pipe(finalize(() => this.actionId.set(null)), takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.notification.show('success', action === 'cancel' ? 'El bono fue anulado.' : 'El bono fue eliminado del listado.');
        this.goToPage(this.appliedFilters.pageIndex);
      },
      error: () => {},
    });
  }

  private emptyDraft(): RegisterBonus {
    return { departmentCode: '', employeeNumber: null, period: this.currentMonth, bonusTypeId: 1, value: null, reasonCode: '' };
  }
}
