# UX2-MS50 — VX-G3 visual review desk

`index.html` assembles a reviewable, static, RTL comparison of the current
active UI and the independent MS44/MS47/MS49 prototypes. Open it from this
repository checkout in a browser; its links resolve to the adjacent prototype
HTML files. The page makes no operational request and includes no live data.

The ten PNGs in `images/` are byte-for-byte copies from the four GitHub CI
artifacts named in `images.json`. That manifest records each source commit,
artifact ID, archive digest, original path, SHA-256, byte count and PNG size.
The active UI frames are from source Run 425; the font, foundation and system
frames are from corrected Runs 412, 419 and 429 respectively. The prototypes
use fictional data. No image has been retouched or treated as a new UI baseline.

Review the active Login, project mobile view, Reporting and A4 browser print
alongside the desktop/mobile font direction, foundation states and mobile
navigation/dialog. Use the linked MS44 prototype to inspect its ten scenarios
and seven states, MS47 for nine component states, and MS49 for feedback,
navigation and confirmation. The comparison surfaces known gaps rather than
claiming migration: mobile navigation, shared state consumers, and print
qualification still need `VX-G4/G5` work.

`src/web/e2e/ms50-vx-g3-review.spec.ts` checks manifest integrity, brand/font
loading, all images and links, absence of external requests, and desktop/mobile
rendering with a 320px overflow check. CI publishes a separate rendered desk
artifact. A successful source/documentation CI makes this review pack a safe
checkpoint; the owner visual decision for `VX-G3` remains a distinct gate.
