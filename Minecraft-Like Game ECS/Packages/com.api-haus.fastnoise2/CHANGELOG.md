## [1.4.2](https://github.com/api-haus/com.api-haus.fastnoise2/compare/v1.4.1...v1.4.2) (2026-03-23)


### Bug Fixes

* nodeEditor rights ([7560103](https://github.com/api-haus/com.api-haus.fastnoise2/commit/756010353b88a90be6ca59d8a82315de03e0133b))

## [1.4.1](https://github.com/api-haus/com.api-haus.fastnoise2/compare/v1.4.0...v1.4.1) (2026-03-22)


### Bug Fixes

* stop CI from deleting .meta files, remove cmake/lib build artifacts ([28f9ac6](https://github.com/api-haus/com.api-haus.fastnoise2/commit/28f9ac6402aba754d746cc734763dea954c98ec2))

# [1.4.0](https://github.com/api-haus/com.api-haus.fastnoise2/compare/v1.3.0...v1.4.0) (2026-03-22)


### Bug Fixes

* restore CI workflows accidentally deleted in GraphToolkit removal ([b862686](https://github.com/api-haus/com.api-haus.fastnoise2/commit/b862686aaf89169e5a764eb9d3cc15f8b84eee4c))


### Features

* add NoiseMuseum sample showcasing full builder API ([dc31510](https://github.com/api-haus/com.api-haus.fastnoise2/commit/dc3151098c8b50f0423d4a98e68187e704e4e195))

# [1.3.0](https://github.com/api-haus/com.api-haus.fastnoise2/compare/v1.2.0...v1.3.0) (2026-03-20)


### Bug Fixes

* detect dead NodeEditor in poll loop to clear stale session immediately ([b0b3c29](https://github.com/api-haus/com.api-haus.fastnoise2/commit/b0b3c29120f37f7907e5947f251632b17a5bc525))
* suppress SIGKILL (137) exit log when we intentionally kill NodeEditor ([5d9f76e](https://github.com/api-haus/com.api-haus.fastnoise2/commit/5d9f76eb17688719a36ac65e0392b43e2200105b))


### Features

* persistent session ↔ asset association via GlobalObjectId ([d2d21ef](https://github.com/api-haus/com.api-haus.fastnoise2/commit/d2d21eff2f5aa18aaf555e54cac3e6fb0af16050))

# [1.2.0](https://github.com/api-haus/com.api-haus.fastnoise2/compare/v1.1.0...v1.2.0) (2026-03-20)


### Bug Fixes

* IPC session resilience — survive domain reloads, clean canvas, null-safe polling ([8fa031b](https://github.com/api-haus/com.api-haus.fastnoise2/commit/8fa031bdf114a4451b3eb297da3eb04b4545532f))


### Features

* NodeEditor IPC integration — P/Invoke bindings, session manager, live property updates ([2147b17](https://github.com/api-haus/com.api-haus.fastnoise2/commit/2147b179d035af4141579069712ff60ca9a146f9))

# [1.1.0](https://github.com/api-haus/com.api-haus.fastnoise2/compare/v1.0.0...v1.1.0) (2026-03-19)


### Features

* update native libs to FastNoise2 v1.1.1 ([0dee725](https://github.com/api-haus/com.api-haus.fastnoise2/commit/0dee725f5a53ce80f2601c260fef20d5d8909fc3))

# 1.0.0 (2026-03-19)


### Bug Fixes

* assign default value for encoded graph ([1f0aca3](https://github.com/api-haus/com.api-haus.fastnoise2/commit/1f0aca35328b3e5c153ad0642d73b0b5df7b15b9))
* atomic safety in build ([c6b3157](https://github.com/api-haus/com.api-haus.fastnoise2/commit/c6b315709f8061f8dbbc671d5f7132a2dae41f34))
* **bindings:** Fix IsCreated property returning true for default FastNoise ([f31bc96](https://github.com/api-haus/com.api-haus.fastnoise2/commit/f31bc96553732502de2e582659f4a0d689e444cd))
* **ci:** add wayland dev packages for GLFW on Linux ([d6cc72d](https://github.com/api-haus/com.api-haus.fastnoise2/commit/d6cc72d0b9c04f080ce3d1df4ea954c60e5d26f6))
* clear main preview encoded cache on reattach ([5abe37f](https://github.com/api-haus/com.api-haus.fastnoise2/commit/5abe37f8aaa19ebea3fcf5af35747c9f620024f1))
* consistent terrain vertical scale across zoom levels ([3e0d9cc](https://github.com/api-haus/com.api-haus.fastnoise2/commit/3e0d9cc76da417a356cb2dc7a6656dfb14d36b27))
* issues ([1102ec7](https://github.com/api-haus/com.api-haus.fastnoise2/commit/1102ec70b4c4faa76accedc232b3a2252bf6fe08))
* line endings ([f4c85d5](https://github.com/api-haus/com.api-haus.fastnoise2/commit/f4c85d581eb0821a2d53e3a53b48cc6ebacaa27a))
* native safety marker in logs; feat: fn2 native dispose ([6ec9d8f](https://github.com/api-haus/com.api-haus.fastnoise2/commit/6ec9d8fc4e063580fe8c3d0f1a1b8177a97523fb))
* node previews survive maximize + unified preview frequency ([75ec30c](https://github.com/api-haus/com.api-haus.fastnoise2/commit/75ec30c671a51042f88f75a9406947e0bc7012a0))
* noise graphs ([69de335](https://github.com/api-haus/com.api-haus.fastnoise2/commit/69de335401c30f0cb55576f69d27ec200060d0fa))
* noise tool proxy invocation ([bdb7aba](https://github.com/api-haus/com.api-haus.fastnoise2/commit/bdb7aba17e3065b380d552d30424a7e9b9128cde))
* normalize job ([cfb4792](https://github.com/api-haus/com.api-haus.fastnoise2/commit/cfb4792d2cd33fc5e421421f942c8a1e4df91354))
* override default GTK tooltips on inner port/field elements ([a2dc0db](https://github.com/api-haus/com.api-haus.fastnoise2/commit/a2dc0dbd18fedd0ca57becf3e1e0c61d343bf4d7))
* prevent double dispose fn2 ([1cc531a](https://github.com/api-haus/com.api-haus.fastnoise2/commit/1cc531ad0f588ce8f5b27d8c64dece854ef5cb56))
* readme ([129edcb](https://github.com/api-haus/com.api-haus.fastnoise2/commit/129edcbfc74097b6a211d850e5e4525d675a9dc7))
* remove unused preview texture from graph asset import ([09f8b95](https://github.com/api-haus/com.api-haus.fastnoise2/commit/09f8b95c55a1153a4e37dd0a5784e93b8c1ad6c9))
* resolve node values displaying as zero after reload ([fc93047](https://github.com/api-haus/com.api-haus.fastnoise2/commit/fc930473659c064e9e47a74ed50c6476c0899d9d))
* set chmod on noisetool bins ([f73f79d](https://github.com/api-haus/com.api-haus.fastnoise2/commit/f73f79d99397b9cb27882d9156e32fd073e20ab6))
* unsafe array read/write utility ([5770f8d](https://github.com/api-haus/com.api-haus.fastnoise2/commit/5770f8d6658c1dd2c6c838281eca9eee6dde6a85))
* untracked content push ([459f0d6](https://github.com/api-haus/com.api-haus.fastnoise2/commit/459f0d67199438e781b3d466355d6afb2cac36e0))
* update native submodule pointer ([07a06a1](https://github.com/api-haus/com.api-haus.fastnoise2/commit/07a06a118a5b29b11c6f1d8066691379b351ceba))
* use asterisk instead of (Default) for hybrid option labels ([f4ac16b](https://github.com/api-haus/com.api-haus.fastnoise2/commit/f4ac16b1e848ada168de67fb0d7a355da8902951))
* use HTTPS submodule URL, fix absolute path in .gitmodules ([4a7fd23](https://github.com/api-haus/com.api-haus.fastnoise2/commit/4a7fd23472de60b613df5e5b4ed51b533f49f343))
* use ray-AABB intersection for terrain raymarch bounds ([526fab6](https://github.com/api-haus/com.api-haus.fastnoise2/commit/526fab67bddde795d6b38874309c50ad91376a9f))
* wire rendering chaos at zoom by propagating culling to ports ([fefbd81](https://github.com/api-haus/com.api-haus.fastnoise2/commit/fefbd81e55c75ff58e5b3a1f51301af7d028da8e))


### Features

* add editor icons for noise graph and FastNoise2 branding ([4641365](https://github.com/api-haus/com.api-haus.fastnoise2/commit/464136533e942c1465aefdd9acd0a8f2662e2d82))
* add FN2_USER_SIGNED compile-time guard for native P/Invoke ([5b8103f](https://github.com/api-haus/com.api-haus.fastnoise2/commit/5b8103f82e4972051d2853e5d06d2e280e26e602)), closes [#if](https://github.com/api-haus/com.api-haus.fastnoise2/issues/if)
* add graph editor bridge UI with wire auto-refresh ([b1c769a](https://github.com/api-haus/com.api-haus.fastnoise2/commit/b1c769a7544cfeb3f17aac9aaa6655a086320e4a))
* add GraphToolkit-based noise graph editor ([834cf16](https://github.com/api-haus/com.api-haus.fastnoise2/commit/834cf1627b962377216bc644cfe8d8ca6cb8459c))
* add node category coloring, categorized add-node menu, and search synonyms ([c94b919](https://github.com/api-haus/com.api-haus.fastnoise2/commit/c94b9193269e870d7defdfad5294ab668204e418))
* add preview pan, orbit, zoom-to-cursor, and persist state ([5254839](https://github.com/api-haus/com.api-haus.fastnoise2/commit/52548397e465c973d7704329d4aac5dd9622c7e7))
* add pure C# node registry, replace FN2MetadataCache ([f0d2ed4](https://github.com/api-haus/com.api-haus.fastnoise2/commit/f0d2ed4b4aca3f4145020161a38417bef6ea80f6))
* add raymarched terrain background to node editor ([8f2926b](https://github.com/api-haus/com.api-haus.fastnoise2/commit/8f2926b23d15a2ac1a7aa6b3ca09f4afedb631b3))
* add scroll-wheel camera zoom for 3D heightfield preview ([07d2381](https://github.com/api-haus/com.api-haus.fastnoise2/commit/07d23811ebbf76f21f2631e4b5271a6c376723b1))
* add type-safe builder API for fluent noise graph composition ([1392d80](https://github.com/api-haus/com.api-haus.fastnoise2/commit/1392d80edf13276cf9af0894770ccefe557fa1aa))
* clarify readme ([43c3cb0](https://github.com/api-haus/com.api-haus.fastnoise2/commit/43c3cb0e2ab24671a7230ba45013e9cf89e220c1))
* clearer api ([26b5240](https://github.com/api-haus/com.api-haus.fastnoise2/commit/26b5240b8aa4b49ca83a9373963fe7be478bb83e))
* comprehensive member tooltips for all field types ([0e77ef9](https://github.com/api-haus/com.api-haus.fastnoise2/commit/0e77ef9f6faefe66590650af26c0488bab48a1bb))
* datatypes - signed short, byte ([d6c46cc](https://github.com/api-haus/com.api-haus.fastnoise2/commit/d6c46cc9ef1b775a117f825f2a077b278d4be395))
* dirty-driven preview rendering with event-based main preview and terrain ([fa75ccd](https://github.com/api-haus/com.api-haus.fastnoise2/commit/fa75ccd84247298f43b25831bda3ee780d765dad))
* enum dropdowns and node tooltips for graph editor ([fb681be](https://github.com/api-haus/com.api-haus.fastnoise2/commit/fb681beae685aa2211ebc428e037d36fb898ed34))
* metadata-driven node system, swap preview orbit/pan buttons ([ba0e6ea](https://github.com/api-haus/com.api-haus.fastnoise2/commit/ba0e6eab0d7ead2116cd738d59b975fbfcced4d2))
* native texture extensions - contains, swap ([92d92af](https://github.com/api-haus/com.api-haus.fastnoise2/commit/92d92af32d702da8c3e579f2a6052b7f7f092ea3))
* OpenUPM semantic-release CI, rename package to com.api-haus.fastnoise2 ([b67d061](https://github.com/api-haus/com.api-haus.fastnoise2/commit/b67d0612470b9cc5c35b0f4170b7ac2403a29dae))
* replace closure-based builder with NodeDescriptor IR, add encode/decode ([1fe4b68](https://github.com/api-haus/com.api-haus.fastnoise2/commit/1fe4b68e0312cc94b54a21c5a18201837da050bd))
* replace FastNoiseEditorNode with per-type node classes ([d81aa31](https://github.com/api-haus/com.api-haus.fastnoise2/commit/d81aa31ddfc0fa13a3b220e59cdeb111d2b3c1ea))
* resizable preview, height scale control, auto-framing terrain ([d60b0c1](https://github.com/api-haus/com.api-haus.fastnoise2/commit/d60b0c10de047b3d348bc15f45b119068266c916))
* some support for native container safety ([abed2a4](https://github.com/api-haus/com.api-haus.fastnoise2/commit/abed2a48a90c385eab82eafdb21ac940b993e176))
* unity fastnoise ([c421400](https://github.com/api-haus/com.api-haus.fastnoise2/commit/c421400a45a86764119b9fb2b58bbac7fbe2610b))
* update bindings to v1.0.1, extract NativeTexture, add native tools ([c7bdcfd](https://github.com/api-haus/com.api-haus.fastnoise2/commit/c7bdcfdab51188f5b7ec3afe874c43dc61d7c74d))
* update documentation ([95fe1f2](https://github.com/api-haus/com.api-haus.fastnoise2/commit/95fe1f222e8670c3d883d3259b29237e0aae5963))
* update readme to reflect component set ([d3e451c](https://github.com/api-haus/com.api-haus.fastnoise2/commit/d3e451cec1db64edf30578957cb5a399082956d7))
* utilities ([ea1db5a](https://github.com/api-haus/com.api-haus.fastnoise2/commit/ea1db5a0208b1bb2705ec50849e464348492863a))
* vector types ([a4cd508](https://github.com/api-haus/com.api-haus.fastnoise2/commit/a4cd5086d2275161e0a4fb4ca4050eb768819cb6))
* visual polish pass for graph editor ([e30a0e3](https://github.com/api-haus/com.api-haus.fastnoise2/commit/e30a0e308cd3b2001f75cc7ccdc2c0cba9ff1c55))
