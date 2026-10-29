import { Injectable, computed, signal } from '@angular/core';

export type NotificationType = 'error' | 'warning' | 'success';

export interface NotificationMessage {
  type: NotificationType;
  text: string;
}

/** Aviso compartido: se presenta en la página o en el diálogo activo. */
@Injectable({ providedIn: 'root' })
export class Notification {
  readonly message = signal<NotificationMessage | null>(null);
  private readonly dialogActive = signal(false);
  readonly pageMessage = computed(() => this.dialogActive() ? null : this.message());

  beginDialog(): void {
    this.clear();
    this.dialogActive.set(true);
  }

  endDialog(): void {
    this.clear();
    this.dialogActive.set(false);
  }

  show(type: NotificationType, text: string): void {
    this.message.set({ type, text });
  }

  clear(): void {
    this.message.set(null);
  }
}
