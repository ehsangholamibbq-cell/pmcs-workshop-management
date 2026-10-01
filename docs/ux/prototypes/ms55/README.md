# UX2-MS55 — Profile image and privacy state prototype

`index.html` is an isolated, clickable RTL example for AvatarFallback,
ProfilePhotoCrop and PrivacyLabel. It uses the pinned Vazirmatn and official
mark. The member name and portrait are fictional; the portrait is a CSS
illustration. There is no file input, upload, save, or network request.

The selector covers fallback, existing sample image, loading, validation
error, offline, no permission, revision conflict and a locally confirmed crop
preview. A native dialog exposes two labeled crop controls, Escape, focus
return and explicit local confirmation. Loading, error and no permission hide
the image rather than implying an accepted file or authorized view. An offline
or conflicted sample image is visibly framed as a local preview, with editing
locked. The privacy text explains project access and retention limits.

Browser checks cover state truth, locked controls, native dialog behavior,
keyboard focus, 320/390/desktop widths and no horizontal overflow. Source and
PNG hashes are recorded in a separate CI artifact. This is prototype evidence
for one V1.1 component family; it does not migrate the active profile route,
qualify the upload/crop backend, or accept `VX-G3`.
