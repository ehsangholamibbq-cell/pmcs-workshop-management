# UX2-MS57 — Attachment and Evidence provenance prototype

`index.html` is an isolated RTL review surface for project-group message
attachments and their possible Evidence reference. The message, file and
version are fictional. There is no file input, upload, download, attachment,
conversion or network request.

The selector covers local queue, pending scan, quarantine, released but not
attached, rejected, offline, no permission, status error, message revision
conflict and no file. Only the Released state enables a local provenance
review dialog. It asks for exact source message revision, file version/hash,
ownership and permission checks in the real product. Operational attach and
download controls remain disabled for every state. Permission hides filename
and metadata; rejected and quarantined files cannot become Evidence sources.

Browser checks cover these boundaries, dialog focus/Escape return, all
controls, 320/390/desktop widths and no horizontal overflow. The CI artifact
indexes screenshots and source hashes. This provides one component-family
prototype for `VX-G3`; it does not qualify the active upload/attachment or
formal Evidence conversion flows and cannot accept the Gate by itself.
