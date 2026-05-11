# Changelog

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
