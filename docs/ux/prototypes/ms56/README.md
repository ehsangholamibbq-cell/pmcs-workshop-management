# UX2-MS56 — Project duplication selection and preview prototype

`index.html` is an isolated RTL design example for the duplication Wizard's
selection, conflict policy, preview rows, exclusion list and local review.
All projects, rows and versions are fictional. It never creates a draft,
executes transfer or sends a network request.

Changing a category, policy or scenario invalidates the prior preview and
review. The policy is chosen before generating a preview: `FailOnConflict`
blocks a conflict, while `SkipConflicts` labels the row Skipped; neither can
override a Blocked permission row. Loading, Empty, Error, Offline,
NoPermission and Expired hide the result, and confirmation remains locked.
An eligible sample still requires explicit review of rows, exclusions,
policy and version. A native dialog confirms only a local review; the
operational Execute control stays disabled in every state.

Browser checks cover the transitions, focus/Escape return, action locks,
state truth, 320/390/desktop widths and no horizontal overflow. SHA-indexed
screenshots form a separate CI artifact. This is prototype evidence for one
component family, not active Wizard migration or `VX-G3` acceptance.
