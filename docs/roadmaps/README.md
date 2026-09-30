# PMCS Roadmap Registry

این فهرست مرجع تشخیص Roadmap فعال است. عبارت «Roadmap فعلی» بدون اشاره به شناسه و نسخهٔ سند مجاز نیست.

مرجع واحد وضعیت و Resume پروژه: [`../PMCS-CANONICAL-PROJECT-REFERENCE.md`](../PMCS-CANONICAL-PROJECT-REFERENCE.md).

| وضعیت | سند | دامنه |
| --- | --- | --- |
| Active | `pmcs-post-v1-product-evolution.md` — `PMCS-RM-POST-V1-001 v1.187.0` | V1.1، V1.2 و V2.x |
| Active program | `pmcs-managerial-agent-seven-stage-roadmap.md` — `PMCS-RM-AGENT-001 v1.3.0` | هفت Stage Agent مدیریتی؛ انتخاب مدل طبق ADR 0032 |
| Active program | `pmcs-visual-excellence-program.md` — `PMCS-RM-VISUAL-001 v1.2.0` | مسیر «مدیریت ممتاز» و بازطراحی سراسری تجربه و ظاهر محصول |
| Completed / Historical | `pmcs-v1-development-and-qualification.md` | تکمیل، Qualification و قفل PMCS V1 |

سیاست لازم‌الاجرای Version و Baseline: `../governance/pmcs-version-and-baseline-policy.md`.

تصمیم انتخاب مدل INT1 در `../adr/0032-int1-controlled-multi-provider-selection.md` ثبت شده است.
ردیف‌های Checkpoint پایین، تاریخچهٔ اجرا در زمان ثبت هستند؛ وضعیت فعلی در جدول بالا و
مرجع Canonical آمده است.

## Current product line status

| مورد | مقدار |
| --- | --- |
| Locked Product Baseline | `PMCS V1` |
| Source baseline commit | `26bf222d44634562ca7f3fc0931f3f8b79ca04a1` |
| Active planning line | `PMCS V1.1` |
| V1.1 state | `Development | UX2 Closed | INT1 in progress | QA1 open` |
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
| V1.1 RPT1 Slice 07 MS18 | source `4c5d026466cb3f76f351221297540a0936337335` / tree `d9e8febc5f220f8d00eaff80926b00dec2ea0926` / Run 188 F06 semantic contract passed / Runtime not implemented، F07–F10/UI/Production open، RPT1 active |
| V1.1 RPT1 Slice 07 MS19 | source `177a1d89a07c23b2ae446218e98556cfbcf57a21` / tree `165cd1d451935f3cb94db7b9f5718678de00aca2` / Run 192 bounded F06 Runtime Core passed / Renderer/Golden و wiring/F07–F10/UI/Production open، RPT1 active |
| V1.1 RPT1 Slice 07 MS20 | source `fb88b94d6949e7780f5f40aa567e2ea3f187a6e8` / tree `212c1193d249cf1120297920c59b4ea15cb80c07` / Run 196 deterministic F06 PDF/XLSX Renderer/Golden passed / Catalog/API/Worker wiring و F07–F10/UI/Production open، RPT1 active |
| V1.1 RPT1 Slice 07 MS21 | source `df3879dd8b17403787154a398cc114b27c7172bc` / tree `5483e684aaa220a32b3135ea0b2bb3b2136023be` / Run 202 F06 Catalog/API/Worker connected qualification passed / F07–F10/UI/Production open، RPT1 active |
| V1.1 RPT1 Slice 07 MS22 | source `11168534372487bff3cc798861ca036489b3dea0` / tree `af7e9e48c706c23f50521dbf715e6027e1512194` / Run 205 F07 semantic contract passed / F07 Runtime و F08–F10/UI/Production open، RPT1 active |
| V1.1 RPT1 Slice 07 MS23 | source `7f66113d3fe091829747d1f5059eb8f16c82cb88` / tree `88ba4957b76c6803893afa04d618b22c47c919e9` / Run 211 bounded F07 Runtime Core passed / Renderer/Golden، historical producer، wiring و F08–F10/UI/Production open، RPT1 active |
| V1.1 RPT1 Slice 07 MS24 | source `8a08d3a0876cb6307613cb3eb51d918ff0269564` / tree `ffa44deb661c4055f06fd32064bdfa8f61de425f` / Run 215 bounded F07 PDF/XLSX Renderer/Golden passed / historical producer، wiring و F08–F10/UI/Production open، RPT1 active |
| V1.1 RPT1 Slice 07 MS25 | source `b2cc811e9202b49dd643972bde547c105fd9dc02` / tree `1673d1b48ca41fd425199da9235ec87c712d81b2` / Run 219 F07 historical transition producer passed / Catalog/API/Worker wiring و F08–F10/UI/Production open، RPT1 active |
| V1.1 RPT1 Slice 07 MS26 | source `b7a44b35eb7f498bf4382990324e3253033c0284` / tree `bb6ebad2d3435caf4e085a65e08a085a2271761f` / Run 222 F07 Catalog/API/Worker connected qualification passed / F08–F10/UI/Production open، RPT1 active |
| V1.1 RPT1 Slice 07 MS27 | source `b72ab1c09376e0ec45b6b52a460660f4b807b15a` / tree `14334896b2155f6ebefe612a29123bd73bce3760` / Run 224 F08 semantic contract passed / Runtime/Renderer/wiring و F09–F10/UI/Production open، RPT1 active |
| V1.1 RPT1 Slice 07 MS28 | source `36682da970d0489569ed62a1d02684b5d6c588e9` / tree `2f44542a094a86d5782b01a31bc8092a97b71b39` / Run 231 F08 owner Source/Runtime Core passed / Renderer/Golden/wiring و F09–F10/UI/Production open، RPT1 active |
| V1.1 RPT1 Slice 07 MS29 | source `68a8c49311d05480824f3c7ec58933510877c8e6` / tree `14fe30b65d983c553cf3675dc202b682d90f5c9d` / Run 235 F08 Renderer/Golden passed / Catalog/API/Worker و F09–F10/UI/Production open، RPT1 active |
| V1.1 RPT1 Slice 07 MS30 | source `786f032e5ce91ceffa80599c4ce02ba23f30bb1d` / tree `9adcf1558c1bcab8a40a242c49efdf4df816a2dc` / Run 237 F08 End-to-End connected passed / F09–F10/UI/Production open، RPT1 active |
| V1.1 RPT1 Slice 07 MS31 | source `67b60c00779d51ea9479dee4d887c810d572d485` / tree `400add67f58dc03b559c3f1420d186fb6cf5ec15` / Run 239 F09 semantic contract passed / Runtime/Renderer/wiring F09 و F10/UI/Production open، RPT1 active |
| V1.1 RPT1 Slice 07 MS32 | source `2000b965dc31e6a83f0b1366c9a203ae809cee03` / tree `14e9b0c6bcc4c4a1b785e761c678e18f0d216a8c` / Run 242 F09 owner Source/Runtime Core passed / Renderer/producer/wiring F09 و F10/UI/Production open، RPT1 active |
| V1.1 RPT1 Slice 07 MS33 | source `0701a44155d1955a1b8e27db01d561c370b6be6e` / tree `db7347e04344394f219cd68257a23c27159fd200` / Run 245 F09 PDF/XLSX Renderer/Golden passed / producer/wiring F09 و F10/UI/Production open، RPT1 active |
| V1.1 RPT1 Slice 07 MS34 | source `79bc3c62f17711eaa61281f90d995852f9128147` / tree `e32fb658426a74fcfb0f61a463ab9748d1b8b686` / Run 248 F09 owner producer passed / selector/wiring F09 و F10/UI/Production open، RPT1 active |
| V1.1 RPT1 Slice 07 MS35 | source `44bd46689bcb795534bcf059f42591cecdaa35ba` / tree `d849b915a0157dd10812acb8271c8b64dd108c29` / Run 250 F09 historical selector passed / wiring F09 و F10/UI/Production open، RPT1 active |
| V1.1 RPT1 Slice 07 MS36 | source `2bb925b8cf8531442c5136e55811a9f8c31db655` / tree `3085dc51931b2b7965521b7b18fe0b9bfa55ec70` / Run 252 F09 connected qualification passed / F10/UI/Production open، RPT1 active |
| V1.1 RPT1 Slice 07 MS37 | source `bb17a37fa19b06d111443fad178c2589e376bb25` / tree `82eba0ffebc1c631281a78727f4b2325a50f0ecb` / Run 254 F10 semantic contract passed / MS38 Tenant-scope and Runtime/Renderer/wiring F10 open، RPT1 active |
| V1.1 RPT1 Slice 07 MS38 | source `ea5215e26ba382bdecd756adec1083dd3ef28d06` / tree `63e5542679e9d0427c6ec96ad7fa3404810f340d` / Run 256 F10 tenant-scope infrastructure passed / MS39 Source/Runtime and Renderer/wiring F10 open، RPT1 active |
| V1.1 RPT1 Slice 07 MS39 | source `c45b72be7bc913a6dc65b656bea0188f7da0dc8c` / tree `00ad037a2eec41c2d5dbacfe0c186e04c4584cce` / Run 261 F10 Source/Runtime Core passed / MS40 Renderer and wiring F10 open، RPT1 active |
| V1.1 RPT1 Slice 07 MS40 | source `d57f73611e647d352ec5c59a19cd996e589d0fe2` / tree `52daac29eff755e3a95c132d581fa7de37ef6387` / Run 264 F10 PDF/XLSX Renderer/Golden passed / MS41 Catalog/Tenant API and Worker/OutputAccess open، RPT1 active |
| V1.1 RPT1 Slice 07 MS41 | source `c22f08778a4c9b523d926f1aedc9f8d9142655d4` / tree `3e5c6f93fae75200f3abba18b292eac4a17d3069` / Run 266 F10 Portfolio Catalog/Tenant API passed / MS42 Worker and MS43 OutputAccess open، RPT1 active |
| V1.1 RPT1 Slice 07 MS42 | source `7a8a560fec6da560aa6982f1b9f11fc253dafef4` / tree `0683b1cbe844a321862e3e25fe2aa4e01e93174c` / Run 272 F10 Portfolio Worker passed / MS43 OutputAccess open، RPT1 active |
| V1.1 RPT1 Slice 07 MS43 | source `84e4e76ca6173f834cec5ea481adc475e9dfd22f` / tree `033b2e4cc3652735b60b7fa9b849707906f8acc0` / Run 275 F10 End-to-End passed / COL1 next، UX2/Production open |
| V1.1 COL1 MS01 | source `a09f52506158eaa69c8aa6692cc057c9704900b0` / tree `850ddf4e940e3da440b877115d4cb53e5ca14fcf` / Run 281 eight jobs green / MS02 next |
| V1.1 COL1 MS04 | source `7f3a09cb5ca5e305b9d20fdd4cb7a10079803d5d` / tree `f0f1e526043bda8f4689f64be9fb86a7db0b8ef9` / Run 294 eight jobs green / MS05 next |
| V1.1 COL1 MS05 | source `d9e327a3e04fb9d2ca22a403b6e84d612abfbaf3` / tree `5ab89f36cc93f832bbc752f3ce0c8416e18b1689` / Run 301 eight jobs green / MS06 next |
| V1.1 COL1 MS06 | source `856089b4070ef4c8720aa01a4289139a9a0e4adc` / tree `f98685e0dc45acdff075420d9d0a92408e3a7593` / Run 304 eight jobs green / UX2 next |
| V1.1 UX2 MS03 | source `282fc1726b332e4d060d4aae0c028017c00345ab` / tree `90351f1e7fb1691cb63366924ea648acfac4a2ec` / Run 313 eight jobs green / MS04 next |
| V1.1 UX2 MS04 | source `c09fd37ecd674fe888ca52c6e369d504d19d4802` / tree `b0fd1056967d91e052c16c6157e1d8aa0f34e6ed` / Run 315 eight jobs green / MS05 next |
| V1.1 UX2 MS05 | source `324e4f930e71fa072ded7b190eea935d468de74f` / tree `04a22d38ee4fe2181e58def8a7e61e2677c9af43` / Run 317 eight jobs green / MS06 next |
| V1.1 UX2 MS06 | source `5914b2184a15cbec0cc8c7224e05fecf8098fb19` / tree `094d693ddfdb01d8cc30d068cf5928d31ca667ef` / Run 319 eight jobs green / MS07 next |
| V1.1 UX2 MS07 | source `70c547c2993c521246ff61e688b9ba98e28f70eb` / tree `ebf6ddd75ad5727f4924438861c70378318a2696` / Run 321 eight jobs green / MS08 next |
| V1.1 UX2 MS08 | source `89b6e378f25d5e7c470896c6c35013ccc8bb2dec` / tree `19841f9f92ac013bb8efb4199047473ec2d2a8a0` / Run 323 eight jobs green / MS09 output access next |
| V1.1 UX2 MS09 | source `73b170aa011dc61f78a4de6f45c7a8747eb14721` / tree `140b9c99bb69b5c81a9dd7705f5364249f4588d9` / Run 326 eight jobs green / MS10 portfolio UI next |
| V1.1 UX2 MS10 | source `de22569ca12659fc96e3e3065d938a11dc5e3def` / tree `e4add5830df07ddc749b35fdc5a0599ee46667a3` / Run 328 eight jobs green / MS11 request next |
| V1.1 UX2 MS11 | source `37cefe7bd76f2a1f4fa87fab03ecf07dfc05a69b` / tree `2178071f82d7d9a8c7d356ff76ff7b8b74c7da25` / Run 331 eight jobs green / MS12 output next |
| V1.1 UX2 MS12 | source `6a5b0649d1709eb3d483331ffcec0ee52f998920` / tree `273511d67bc428d31e149e48e4630c45f465a3e3` / Run 334 eight jobs green / MS13 Chat interactions next |
| Active stage | `V1.1-UX2 — Product UI Implementation and Migration` |
