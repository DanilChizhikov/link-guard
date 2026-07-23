# Changelog

## [1.4.0] - 2026-07-23

### Added
- link.xml sync: a **Sync** toolbar button and the public build-time API `DTech.LinkGuard.Editor.LinkXmlSync.Sync(apply, throwOnError)` that add entries for project code the current `Assets/link.xml` does not cover yet, so types written after the file was generated are not stripped. Sync only adds — existing entries, attributes, comments, and formatting are never removed, narrowed, or reordered
- Sync covers every namespace of every project assembly, including assemblies missing from `link.xml` entirely: they are added as `<assembly fullname="..."/>` with one collapsed `<namespace fullname="..." preserve="all"/>` entry per namespace. Namespaces with deliberately narrowed entries (`preserve="fields"`, `preserve="methods"`) get explicit `<type preserve="all"/>` entries for the missing types instead, and the global namespace always uses explicit type entries
- Sync is limited to project code (asmdefs under `Assets`) by default. Plugins, UPM packages, SDKs, and Unity assemblies are never expanded on their own, so a hand-written entry for a third-party SDK cannot silently grow the build; opt in with `LinkXmlSync.Sync(..., includeExternalAssemblies: true)` or cover them explicitly with scope patterns
- Duplicate `<assembly fullname="...">` elements are aggregated: `preserve="all"` on any of them wins, an assembly counts as narrowed only when every duplicate is narrowed, and new entries are never appended to a narrowed element
- An `<assembly>` element with an explicit `preserve` other than `all` and no children opts that assembly out of sync; such assemblies are listed in `LinkXmlSyncReport.SkippedAssemblies`
- Optional glob scope patterns (`LinkXmlSync.Sync(new[] { "Game.*" }, ...)`) matched against assembly and namespace names; a pattern is an explicit opt-in and also covers non-project assemblies
- `LinkXmlSyncReport` lists added assemblies, namespaces, and types grouped by assembly, plus the skipped assemblies


## [1.3.1] - 2026-07-20

### Fixed
- Added missing meta files for new scripts


## [1.3.0] - 2026-07-20

### Added
- Namespace-level preservation: a fully selected namespace collapses to a single `<namespace fullname="..." preserve="all"/>` entry instead of listing every type. The assembly tree is now a hierarchical, segmented namespace tree with namespace-level selection, and import, merge, and preservation understand `<namespace>` elements and `namespace.*` wildcard type patterns
- Precompiled (DLL) assembly scanning: user precompiled assemblies from `CompilationPipeline.GetPrecompiledAssemblyPaths` are scanned, filtered to those actually included in the player build (via `PluginImporter` platform compatibility), resolved to package-relative paths, and collected by reflection
- ProGuard scanning of registered UPM packages: `AndroidArtifactScanner` now collects search roots from non-embedded UPM packages in addition to `Assets`/`Packages`, resolves stable `Packages/<name>/...` origins, and skips artifacts not included in the Android build
- Brace-depth-aware Java/Kotlin type parser (`JavaSourceTypeExtractor`) that strips line, block, and nested Kotlin comments, string/char literals, and Kotlin raw strings before detecting top-level types and their inner classes, replacing the previous package/type regex

### Changed
- Validation is more conservative: `PlayerBuildMembershipOracle` distinguishes player-exact assemblies (precompiled player paths / player output) from merely-loaded ones. A type missing from a non-exact assembly now reports `Unknown` instead of `Missing`, preventing false removals
- Split one type per file across the Zenject, Profiles, and ProGuard modules; no public API changes

### Fixed
- System-assembly filter: the `System` prefix is now matched as `System.` (bare `System` moved to exact-name exclusion), so assemblies merely starting with "System" are no longer wrongly excluded
- ProGuard writer only toggles `PlayerSettings.Android.useCustomProguardFile` when writing to the default path; a custom target path no longer flips the player setting
- ProGuard scanning skips `META-INF/` entries
- Zenject patcher no longer overwrites a malformed existing `link.xml`: if the root is not `<linker>` or the file cannot be parsed, it aborts and reports the failure instead of losing data
- `link.xml` import no longer force-selects types that already carry a non-`all` `preserve` attribute

### Removed
- Legacy `Window/DTech/Link XML Generator` menu alias; the window is opened only via `Window/DTech/Link Guard`

## [1.2.1] - 2026-07-16

### Fixed
- Added the missing Unity meta file for `Editor/ProGuard/ProGuardBaseRulesStore.cs` so package import and version control keep the ProGuard base rules asset script metadata intact

## [1.2.0] - 2026-06-28

### Added
- link.xml validation: a **Validate** toolbar button in the generator window checks the current `Assets/link.xml` and removes `<assembly>`/`<type>` entries that will not be in the player build. The window shows a report of the stale entries and removes them only after confirmation
- Public build-time API `DTech.LinkGuard.Editor.LinkXmlValidator.Validate(apply, throwOnError)` for invocation from external build scripts; returns `LinkXmlValidationReport` with removed/kept entries, and with `throwOnError: true` a parse failure throws `BuildFailedException`
- Conservative removal policy: entries are removed only when the assembly/type is confidently absent. Editor-only and `UnityEditor.*` assemblies are removed; BCL, `UnityEngine.*`, precompiled, and unresolvable entries are kept. `ignoreIfMissing="true"` assemblies and wildcard type patterns are always kept
- ProGuard/R8 support: new **ProGuard** tab in the generator window that scans Android artifacts (`.aar`, `.androidlib`, `.jar`, and Java/Kotlin sources) into an artifact → package → class tree and generates `-keep` rules
- Generated rules are written to `Assets/Plugins/Android/proguard-user.txt` and `PlayerSettings.Android.useCustomProguardFile` is enabled automatically
- The **Generate ProGuard** button is shown only when the active build target is Android; a notice is displayed when minification (R8) is disabled
- Public build-time API `DTech.LinkGuard.Editor.ProGuard.ProGuardPatcher.Patch(path)` for invocation from `IPreprocessBuildWithReport`; it keeps all scanned Android classes and skips when minification is disabled
- ProGuard selection profiles (save/load JSON), mirroring the link.xml profiles
- Public build-time API `DTech.LinkGuard.Editor.LinkXmlPatcher.Patch(throwOnError)` that runs every discovered `ILinkXmlMergeProvider`, merges their output, and writes `Assets/link.xml` without dialogs; returns `LinkXmlPatchReport` with per-provider outcomes, and with `throwOnError: true` a provider failure throws `BuildFailedException` before anything is written

### Changed
- The editor window is now a modular tab host (`Window/DTech/Link Guard`); `link.xml` and `ProGuard` are tabs discovered through `IGeneratorTab` / `TypeCache`. The legacy `Window/DTech/Link XML Generator` menu opens the same window
- ProGuard lives in a separate optional assembly (`com.dtech.linkguard.editor.proguard`); the core window has no Android or ProGuard dependencies
- Merge provider discovery silently skips `ILinkXmlMergeProvider` implementations without a parameterless constructor instead of logging an instantiation warning

## [1.1.0] - 2026-05-13

### Added
- Modular merge-provider system: implement `ILinkXmlMergeProvider` to add a custom toolbar button next to "Merge link.xml"
- Optional Zenject extension assembly (`com.dtech.linkguard.editor.zenject`) gated by `LINKGUARD_ZENJECT_ENABLED` (defined automatically when Extenject or Modesttree Zenject is in the manifest)
- "Merge from Zenject Installers" toolbar button: scans `SceneContext`, `ProjectContext`, and `GameObjectContext`, walks `Container.Install<T>()` transitively, extracts bound types via Mono.Cecil IL analysis (handles constructor injection without `[Inject]`), and adds supplementary `[Inject]`-annotated types
- Optional Addressable scenes coverage via `LINKGUARD_ADDRESSABLES_ENABLED` (defined automatically when `com.unity.addressables` is in the manifest)
- Public build-time API `DTech.LinkGuard.Editor.Zenject.ZenjectLinkXmlPatcher.Patch(linkXmlPath)` for invocation from `IPreprocessBuildWithReport`

### Changed
- Selection in the generator window stops at the type level — methods/constructors are no longer individually selectable
- Existing `<method>` entries in an imported `link.xml` automatically promote the parent type to `preserve="all"` with a Console warning
- Profile schema bumped to v3 (Methods array dropped); v2 profiles with method selections are loaded by promoting the affected types to whole-type selection

### Removed
- `MethodEntry` model, the per-method tree row, and the `<method signature="..."/>` writer

## [1.0.0] - 2026-05-11

### Added
- Initial Link Guard release
- Assembly scanner for project assemblies, plugins, UPM packages, known SDKs, and Unity built-in modules
- Grouped tree UI for selecting assemblies, namespaces, types, and methods to preserve
- Search support across assemblies, namespaces, types, method names, and method signatures
- `link.xml` generation flow targeting `Assets/link.xml`
- Optional XML preview before writing generated output
- Save and load JSON profiles for reusable preservation selections
- Automatic import of existing `Assets/link.xml` when the generator window opens
- Merge flow for existing `link.xml` files from `Assets` and `Packages`
- Duplicate collapse and custom attribute preservation during import and merge
- SDK grouping extension point through `DTech.LinkGuard.IKnownSdkProvider`
- `ignoreIfMissing` support for assembly entries
