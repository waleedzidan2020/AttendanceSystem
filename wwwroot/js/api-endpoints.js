// Single source of truth for all frontend API routes.
// All calls are same-origin so the same build works locally and in production.
window.ApiEndpoints = Object.freeze({
  attendance: Object.freeze({
    status: employeeCode => `/api/attendance/status?employeeCode=${encodeURIComponent(employeeCode)}`,
    checkIn: '/api/attendance/checkin',
    checkOut: '/api/attendance/checkout'
  }),
  admin: Object.freeze({
    auth: Object.freeze({
      login: '/api/admin/auth/login',
      logout: '/api/admin/auth/logout'
    }),
    dashboard: '/api/admin/dashboard',
    employees: '/api/admin/employees',
    sites: '/api/admin/sites',
    attendance: '/api/admin/attendance',
    rejectedAttempts: '/api/admin/attendance/rejected-attempts',
    attendanceSummary: '/api/admin/reports/attendance-summary'
  })
});
