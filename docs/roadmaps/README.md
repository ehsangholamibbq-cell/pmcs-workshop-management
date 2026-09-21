# PMCS Roadmap Registry

این فهرست مرجع تشخیص Roadmap فعال است. عبارت «Roadmap فعلی» بدون اشاره به شناسه و نسخهٔ سند مجاز نیست.

مرجع واحد وضعیت و Resume پروژه: [`../PMCS-CANONICAL-PROJECT-REFERENCE.md`](../PMCS-CANONICAL-PROJECT-REFERENCE.md).

| وضعیت | سند | دامنه |
| --- | --- | --- |
| Active | `pmcs-post-v1-product-evolution.md` — `PMCS-RM-POST-V1-001 v1.51.0` | V1.1، V1.2 و V2.x |
| Active program | `pmcs-managerial-agent-seven-stage-roadmap.md` — `PMCS-RM-AGENT-001 v1.0.0` | هفت Stage Agent مدیریتی |
| Active program | `pmcs-visual-excellence-program.md` — `PMCS-RM-VISUAL-001 v1.2.0` | مسیر «مدیریت ممتاز» و بازطراحی سراسری تجربه و ظاهر محصول |
| Completed / Historical | `pmcs-v1-development-and-qualification.md` | تکمیل، Qualification و قفل PMCS V1 |

سیاست لازم‌الاجرای Version و Baseline: `../governance/pmcs-version-and-baseline-policy.md`.

## Current product line status

| مورد | مقدار |
| --- | --- |
| Locked Product Baseline | `PMCS V1` |
| Source baseline commit | `26bf222d44634562ca7f3fc0931f3f8b79ca04a1` |
| Active planning line | `PMCS V1.1` |
| V1.1 state | `Development | UX1/EXT1/DOC1/IAM1/PRJ1 Closed | RPT1 Active` |
| V1.1 branch | `v1.1-development` |
| V1.1 repository start commit | `0389b52cbd3385bdcc9f0e2a94411800389ae2fc` |
| V1.1 product code started | بله |
| V1.1 governance source | `d4ac64ea818c7e48476b650b84dafe31bf1872a4` |
| V1.1 governance CI | Run 71 / `35275795712` / `success` |
| V1.1 DOC1 source | `3fb9f3cb9d14cef5ecbc3de1a3f1f266e88e0e11` |
| V1.1 DOC1 CI | Run 83 / `35338895848` / `success` |
| V1.1 IAM1 source | `86f9f4efd086e4e67823a930fcb3ef1b7249b71f` |
| V1.1 IAM1 CI | Run 88 / `35345558791` / `success` |
| V1.1 PRJ1 source | `e1b5bf6af813af7324065edc1c91eecf2391eccd` |
| V1.1 PRJ1 CI | Run 92 / `35355855215` / `success` |
| V1.1 RPT1 DoR | `PMCS-V1.1-RPT1-DOR1` / Ready for Implementation / no runtime change |
| V1.1 RPT1 Slice 01 | source `43cac1b83ac7764fe6005fee108029597091a238` / local structural candidate / unqualified |
| V1.1 RPT1 Slice 02 | source `ef5d68e5d35b7f2b58ebd3da87b3b35dadf19173` / tree `4deccade8899a2438485fb1304cd918115af2654` / Run 99 core connected regression passed / RPT1 exit gates open |
| V1.1 RPT1 Slice 03 | source `b4da1e951debf76e1ba3b398bde2ccf60fbde5de` / tree `aa4063214ad1dea8fac19685a81818623296c24c` / Run 102 cancel-security connected regression passed / RPT1 exit gates open |
| V1.1 RPT1 Slice 04 | source `e1ac3263df53a245b1aefb338a015be4854d367b` / tree `f58881f7e0a77bf89f65b872d4f988bd154a809f` / Run 104 two-worker/crash recovery connected regression passed / RPT1 exit gates open |
| V1.1 RPT1 Slice 05 | source `167133fc1985c5b57c3dac90535f7a962dfd03b7` / tree `34fb70aee9a62a434a8446444d7c6d5c6c9819bd` / Run 108 worker-revocation/object-integrity connected regression passed / RPT1 exit gates open |
| V1.1 RPT1 Slice 06 MS01 | source `d085c44f9ed8b3c085af62de6009fa1dafc9ed8e` / tree `9f8afd35b54fe7eacd38f202128538cc571c651f` / Run 110 worker-capacity Core full CI passed / connected load-poison-fairness qualification pending |
| V1.1 RPT1 Slice 06 MS02 | source `346fbb778aa5c4475fd48df3241b700341e96d83` / tree `98b25e2dcd109356bdea08de138995f271260cfc` / Run 113 connected capacity-poison-fairness passed / RPT1 exit gates open |
| V1.1 RPT1 Slice 06 MS03-C1 | source `83f13cf43679b23a6a169cc0912985b391b1c017` / tree `1705d184bd494e80e50d8a85b723f0bc63e20abc` / Run 117 bounded signal and connected queue-age health passed / MS03-C2 open |
| V1.1 RPT1 Slice 06 MS03-C2 | source `9bb7ede9b89da2078e165cccb2927e0449116909` / tree `a960cddb5264b3de8857812906b7595db0664ba5` / Run 120 OTLP scrape/rules and connected queue-age alert delivery passed / MS03 closed, RPT1 active |
| V1.1 RPT1 Slice 06 MS04 | source `4ff44c96104ee1df87d267ca9a530d19b9248ba3` / tree `d4c320e7917121f64a70dea1251169bef4b516ce` / Run 123 safe orphan inventory/dry-run/remediation passed / MS04 closed, RPT1 active |
| V1.1 RPT1 Slice 06 MS05 | source `38a03f33f4747d0b6a76696705877633acd17678` / tree `eb9369c9e32eb3f523c4faa22487d6428c2d7e34` / Run 130 semantic cutoff and deterministic XLSX Golden passed / MS05 closed, RPT1 active |
| V1.1 RPT1 Slice 06 MS06 | source `b8f21492a4f44c7c412e5b7eda0b164e7f256758` / tree `e94b6ba3753e67b42ea0ec99e998761fdad0bcc3` / Run 133 Community decision and pinned PDF Golden/visual/performance passed / MS06 closed, catalog decision open, RPT1 active |
| V1.1 RPT1 Slice 07 MS01 | source `d81ecc00762145210e1c688f8f5843f46d62fc04` / tree `5f40383ad506d94520c741eb69fcd00086283734` / Run 135 ten-family catalog decision passed / F02–F10 open, RPT1 active |
| V1.1 RPT1 Slice 07 MS02 | source `b4a59fa966320a1da4b53759814224e21893c01e` / tree `6b5b486dace3c07b0b4e0385413bf1add5aee7a3` / Run 137 F02 semantic contract passed / Runtime not implemented، RPT1 active |
| V1.1 RPT1 Slice 07 MS03 | source `6fc28cf54a6df820c49a2365eab76e3550ae421a` / tree `5188dac79fe5187b319e6aa727da89163fa37c1b` / Run 139 bounded Runtime Core passed / API/Renderer open، RPT1 active |
| V1.1 RPT1 Slice 07 MS04 | source `4f68f57de2c2a79b654a19128894d9c89878ab65` / tree `f4b592c72ea65974c00b936ca59c0428eb47f981` / Run 141 deterministic PDF/XLSX Renderer/Golden passed / Catalog/API/Worker wiring open، RPT1 active |
| V1.1 RPT1 Slice 07 MS05 | source `7fc55c167ad2159a31c895b32a52d78f47574df9` / tree `d665fe4cdf29369f96ec0875bc6f1535db349d55` / Run 144 F02 Catalog/API/Worker connected qualification passed / F03–F10 open، UI/Production disabled، RPT1 active |
| V1.1 RPT1 Slice 07 MS06 | source `e3218555a38f7ba460558e51b4db3f8bc17fcd9c` / tree `1fe4cc804fdd078a71ff2633201c8c690447900e` / Run 146 F03 semantic contract passed / Runtime not implemented، RPT1 active |
| V1.1 RPT1 Slice 07 MS07 | source `22d5b0f91edf8d733192fae0ba946c8538c63bca` / tree `eb5ea4253a7b80e8ab3320b9ccea747624f89790` / Run 148 bounded F03 Runtime Core passed / Renderer/wiring open، RPT1 active |
| V1.1 RPT1 Slice 07 MS08 | source `d9d7ddb17d222f3b53402f291bf3d0cb8a3f957f` / tree `58fb79b0ee3d7c9cfa11630635b8ebdfbcbce434` / Run 154 deterministic F03 PDF/XLSX Renderer/Golden passed / Catalog/API/Worker wiring open، RPT1 active |
| V1.1 RPT1 Slice 07 MS09 | source `40afeb37d7bf90e97a988cae141901e28d336516` / tree `ae06285bf1a68fe2592dacc76c7d31cb291ab924` / Run 156 F03 Catalog/API/Worker connected qualification passed / F04–F10 open، UI/Production disabled، RPT1 active |
| V1.1 RPT1 Slice 07 MS10 | source `f8829027c2ce073c207cd0e04a49c306b546c6a1` / tree `2b784f135894092ef55bf7c7df201b1f03e0c77f` / Run 158 F04 semantic contract passed / Runtime not implemented، RPT1 active |
| V1.1 RPT1 Slice 07 MS11 | source `deb1571ec66d820868e8f4b77b631471e3c8207c` / tree `9e41495a357480af03f1555ef640962ab863d332` / Run 163 bounded F04 Runtime Core passed / Renderer/Golden و wiring open، RPT1 active |
| V1.1 RPT1 Slice 07 MS12 | source `6a717f10e4bff167ad7e2643313008f5afcc8264` / tree `995c7fae108bbb5265faa036f951036d36e7061e` / Run 167 deterministic F04 PDF/XLSX Renderer/Golden passed / Catalog/API/Worker wiring open، RPT1 active |
| V1.1 RPT1 Slice 07 MS13 | source `4c48c03aad126a594e5328fc7995a72728ba2274` / tree `49f957729fdccb0397dd153b93135ce2eaddd68a` / Run 169 F04 Catalog/API/Worker connected qualification passed / F05–F10 open، UI/Production disabled، RPT1 active |
| V1.1 RPT1 Slice 07 MS14 | source `72fa88349d01edd4c6455eb0af1aebfdeced8c35` / tree `d2722dd8fab797650ed0c9befb80df93fc0be135` / Run 171 F05 semantic contract passed / Runtime not implemented، F06–F10/UI/Production open، RPT1 active |
| V1.1 RPT1 Slice 07 MS15 | source `77ad46cbac12b899116516b0a58665ae888b3bf2` / tree `5c67523b0fbed8d521627fe406f74271a1bbdcfe` / Run 175 bounded F05 Runtime Core passed / Renderer/Golden و wiring/F06–F10/UI/Production open، RPT1 active |
| V1.1 RPT1 Slice 07 MS16 | source `9ddf7f1d96324e7ffb22d2abec83071a6c087ec2` / tree `f873795dcb8893dc28f88d5e5fc8292c5201e1e4` / Run 178 deterministic F05 PDF/XLSX Renderer/Golden passed / Catalog/API/Worker wiring و F06–F10/UI/Production open، RPT1 active |
| V1.1 RPT1 Slice 07 MS17 | source `6de1e9ac3b457426be5e50064d1767106cd50c39` / tree `a4a8e8e655c56d05da2be5d87e7b84a9bb9a7a1f` / Run 185 F05 Catalog/API/Worker connected qualification passed / F06–F10/UI/Production open، RPT1 active |
| Active stage | `V1.1-RPT1 — Reporting Center Phase 1` |
