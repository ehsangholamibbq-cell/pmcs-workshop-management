# UX2-MS51 — Overlay and mobile row fallback prototype

`index.html` is an isolated RTL design example for the remaining overlay and
responsive row states in `PMCS-UX-COMPONENT-STATES-001`. It uses the official
mark and pinned Vazirmatn asset. The two sample rows are fictional and cannot
be mistaken for an official snapshot. No network or operational request occurs.

The state selector distinguishes Default, Loading, NoData, Error, NoPermission,
Offline, Conflict and Success. Loading hides the rows and uses an `aria-hidden`
neutral skeleton. Permission/error/offline also hide rows and lock the sample
action. The desktop table has a labeled mobile card fallback at 720px and
below. A Popover exposes `aria-expanded`, keyboard focus and Escape return;
a native modal Drawer retains focus and returns it on close; a persistent
Toast has a close control and live text. The prior MS49 prototype covers
version confirmation separately.

The Browser E2E checks state truth, keyboard/locked action, focus, all
viewports and absence of horizontal overflow at 320px, with SHA-indexed
screenshots in a separate CI artifact. This adds prototype evidence, not
active route migration or Gate acceptance by itself.
