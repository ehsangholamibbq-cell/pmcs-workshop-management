# UX2-MS52 — Login, Shell and Chart composition

The standalone RTL `index.html` brings the approved official mark and
Vazirmatn 2.0.0 into a bounded visual example: a noninteractive Login
composition, project Shell with mobile disclosure navigation, and a six-point
sample trend with accessible title/description plus a table of the same
fictional values. No authentication, network request or operational data is
used. The form fields are decorative and the sample Login action is inert.

The chart state selector hides **both** the SVG and table for Loading,
NoData, Error, NoPermission and Offline. Loading shows only a neutral
`aria-hidden` skeleton; errors and permission refusal have text and live
roles. Mobile navigation uses a button with `aria-expanded`, Escape and
focus return. Browser E2E covers these semantics, 320px overflow, responsive
screenshots and an A4 print example. The print is a prototype, not the
official Reporting/PDF system or a `VX-G5` golden.

This completes another review example for `VX-G3`; owner review, active
route migration in `VX-G4`, and comprehensive visual qualification in
`VX-G5` are distinct decisions.
