# Changelog

## [1.2.0] - 2026-06-09

### Added
- ProGuard/R8 support: new **ProGuard** tab in the generator window that scans Android artifacts (`.aar`, `.androidlib`, `.jar`, and Java/Kotlin sources) into an artifact → package → class tree and generates `-keep` rules
- Generated rules are written to `Assets/Plugins/Android/proguard-user.txt` and `PlayerSettings.Android.useCustomProguardFile` is enabled automatically
- The **Generate ProGuard** button is shown only when the active build target is Android; a notice is displayed when minification (R8) is disabled
- Public build-time API `DTech.LinkGuard.Editor.ProGuard.ProGuardPatcher.Patch(path)` for invocation from `IPreprocessBuildWithReport`; it keeps all scanned Android classes and skips when minification is disabled
- ProGuard selection profiles (save/load JSON), mirroring the link.xml profiles

### Changed
- The editor window is now a modular tab host (`Window/DTech/Link Guard`); `link.xml` and `ProGuard` are tabs discovered through `IGeneratorTab` / `TypeCache`. The legacy `Window/DTech/Link XML Generator` menu opens the same window
- ProGuard lives in a separate optional assembly (`com.dtech.linkguard.editor.proguard`); the core window has no Android or ProGuard dependencies

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
