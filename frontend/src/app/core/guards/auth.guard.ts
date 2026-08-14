import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { EmployeeRole } from '../models';

/**
 * On a hard page refresh, AuthService.restoreSession() (kicked off by the
 * APP_INITIALIZER in app.config.ts) may still be resolving when the router
 * evaluates guards for the initial route. Waiting here for it to finish
 * avoids a false "not authenticated" redirect mid-restore.
 */
async function awaitSessionRestore(auth: AuthService): Promise<void> {
  while (auth.restoring()) {
    await new Promise((resolve) => setTimeout(resolve, 25));
  }
}

export const staffAuthGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  await awaitSessionRestore(auth);
  if (!auth.isStaffAuthenticated()) return router.parseUrl('/login');
  // The change-password screen is the only thing reachable until it's done.
  if (auth.staffMustChangePassword()) return router.parseUrl('/admin/change-password');
  return true;
};

export const clientAuthGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  await awaitSessionRestore(auth);
  if (!auth.isClientAuthenticated()) return router.parseUrl('/login');
  if (auth.clientMustChangePassword()) return router.parseUrl('/portal/change-password');
  return true;
};

/** Guards the change-password screen itself: must be logged in, but redirect away once the change is already done. */
export const staffMustChangePasswordGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  await awaitSessionRestore(auth);
  if (!auth.isStaffAuthenticated()) return router.parseUrl('/login');
  if (!auth.staffMustChangePassword()) return router.parseUrl('/admin/dashboard');
  return true;
};

export const clientMustChangePasswordGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  await awaitSessionRestore(auth);
  if (!auth.isClientAuthenticated()) return router.parseUrl('/login');
  if (!auth.clientMustChangePassword()) return router.parseUrl('/portal/dashboard');
  return true;
};

export const adminRoleGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  await awaitSessionRestore(auth);
  const emp = auth.currentEmployee();
  if (emp?.roles.includes('Admin')) return true;
  return router.parseUrl('/admin/dashboard');
};

/**
 * Generic role gate for staff pages restricted to specific roles (in
 * addition to Admin, which the sidebar and route config always assume can
 * reach everything). Use as canActivate: [roleGuard(['EmployeeTechnician'])] etc.
 * A signed-in employee without any of the listed roles is bounced to their
 * own dashboard rather than shown a blank/broken page.
 */
export function roleGuard(roles: EmployeeRole[]): CanActivateFn {
  return async () => {
    const auth = inject(AuthService);
    const router = inject(Router);
    await awaitSessionRestore(auth);
    const emp = auth.currentEmployee();
    if (emp?.roles.includes('Admin')) return true;
    if (emp && roles.some(r => emp.roles.includes(r))) return true;
    return router.parseUrl('/admin/dashboard');
  };
}
