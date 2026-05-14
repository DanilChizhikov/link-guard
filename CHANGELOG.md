# Changelog

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
