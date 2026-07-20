# Link Guard
[![Unity Version](https://img.shields.io/badge/unity-6000.0+-000.svg)](https://unity.com/releases/editor/archive)
![Unity Tests](https://github.com/DanilChizhikov/link-guard/actions/workflows/tests.yml/badge.svg?branch=master)
[![openupm](https://img.shields.io/npm/v/com.dtech.link-guard?label=openupm&registry_uri=https://package.openupm.com)](https://openupm.com/packages/com.dtech.link-guard/)

## Overview
Link Guard is a Unity editor tool for building `link.xml` files used by managed code stripping and IL2CPP builds.
It scans project assemblies, precompiled plugin DLLs, UPM packages, known SDKs, and Unity modules, then lets you
choose which assemblies, namespaces, or types should be preserved. A fully selected namespace collapses to a single
`<namespace preserve="all"/>` entry instead of listing every type.

The editor window is a modular tab host. Alongside the `link.xml` tab, a **ProGuard** tab scans Android artifacts
(`.aar`, `.androidlib`, `.jar`, and Java/Kotlin sources) and generates `-keep` rules for the Android R8/ProGuard
shrinker — the native-side counterpart of `link.xml`.

## Table of Contents
- [Getting Started](#getting-started)
    - [Prerequisites](#prerequisites)
    - [Manual Installation](#manual-installation)
    - [UPM Installation](#upm-installation)
- [Features](#features)
- [Usage](#usage)
    - [Open the Window](#open-the-window)
    - [Generate link.xml](#generate-linkxml)
    - [Preview](#preview)
    - [Profiles](#profiles)
    - [Merge Existing link.xml Files](#merge-existing-linkxml-files)
    - [Validate link.xml](#validate-linkxml)
    - [Custom SDK Groups](#custom-sdk-groups)
    - [Custom Merge Providers](#custom-merge-providers)
    - [Zenject Module](#zenject-module)
    - [ProGuard Rules](#proguard-rules)
- [License](#license)

## Getting Started

### Prerequisites
- [GIT](https://git-scm.com/downloads)
- [Unity](https://unity.com/releases/editor/archive) 6000.0+

### Manual Installation
1. Download the .unitypackage from the [releases](https://github.com/DanilChizhikov/link-guard/releases/) page.
2. Import com.dtech.link-guard.x.x.x.unitypackage into your project.

### UPM Installation
1. Open the manifest.json file in your project's Packages folder.
2. Add the following line to the dependencies section:
    ```json
    "com.dtech.link-guard": "https://github.com/DanilChizhikov/link-guard.git",
    ```
3. Unity will automatically import the package.

If you want to set a target version, Link Guard uses the `v*.*.*` release tag so you can specify a version like #v1.3.1.

For example `https://github.com/DanilChizhikov/link-guard.git#v1.3.1`.

## Features
- Assembly scanning for project code, plugins, precompiled DLLs, UPM packages, known SDKs, and Unity modules (precompiled assemblies filtered to those included in the player build)
- Grouped, hierarchical tree view with assembly, namespace, and type selection
- Search by assembly, namespace, or type name
- `link.xml` generation to `Assets/link.xml`
- Namespace-level preservation: a fully selected namespace is written as a single `<namespace fullname="..." preserve="all"/>` entry; import and merge understand `<namespace>` elements and `namespace.*` wildcard patterns
- Optional preview of generated XML before writing
- Save and load selection profiles
- Import the current `Assets/link.xml` when the window opens (legacy method-level entries are promoted to whole-type `preserve="all"` with a warning in the Console)
- Merge existing `link.xml` files from `Assets` and `Packages`
- Validate the current `Assets/link.xml` against assemblies and types that will be present in the player build
- Preserve unknown entries and custom XML attributes when importing or merging
- `ignoreIfMissing` support for assembly entries
- Custom SDK grouping through `IKnownSdkProvider`
- ProGuard/R8 keep-rule generation from scanned Android artifacts (`.aar`, `.androidlib`, `.jar`, Java/Kotlin sources) across `Assets`, `Packages`, and registered UPM packages, filtered to artifacts included in the Android build
- Modular tabs discovered through `IGeneratorTab` / `TypeCache`

## Usage

### Open the Window
Open `Window/DTech/Link Guard`. The window hosts two tabs: **link.xml** and **ProGuard**.

On the `link.xml` tab, press `Refresh` to scan assemblies. The tree is grouped by source:

- Project assemblies
- Plugins folder
- UPM packages
- SDKs
- Unity built-in modules
- Merged `link.xml` entries

### Generate link.xml
1. Use the search field to find assemblies, namespaces, or types.
2. Select the entries that must be preserved. The smallest selectable unit is a type (written with `preserve="all"`); selecting a whole namespace collapses to a single `<namespace preserve="all"/>` entry.
3. Use `Select All` or `None` for quick bulk selection when needed.
4. Press `Generate link.xml`.

The generated file is written to `Assets/link.xml`. If a file already exists, Link Guard asks for confirmation before
overwriting it.

### Preview
Enable `Preview` to see the generated XML before writing the file.
The preview updates as the selection changes.

### Profiles
Use `Save Profile` to store the current selection as a JSON profile.
Use `Load Profile` to restore a saved selection later.

Profiles are useful when a project needs a stable stripping configuration that should be regenerated after assemblies
or SDKs change.

### Merge Existing link.xml Files
Press `Merge link.xml` to scan `Assets` and `Packages` for existing `link.xml` files.

Link Guard merges valid files into the current selection, collapses duplicate entries, preserves custom attributes,
and reports invalid files. After the merge, review the preview and press `Generate link.xml` to write the final
`Assets/link.xml`.

### Validate link.xml
Press `Validate` to check the current `Assets/link.xml` against the assemblies and types that will be included in
the player build.

Link Guard reports stale `<assembly>` and `<type>` entries, then removes them only after confirmation. Entries are
removed only when they are confidently absent from the build. Editor-only and `UnityEditor.*` entries are removed;
BCL, `UnityEngine.*`, precompiled, unresolvable entries, wildcard type patterns, and `ignoreIfMissing="true"`
assemblies are kept.

#### Build-time API (validation)
For automated pipelines, call the validator from a custom build hook or before starting a player build:

```csharp
using DTech.LinkGuard.Editor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

internal sealed class LinkXmlValidationBuildHook : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        LinkXmlValidationReport validationReport = LinkXmlValidator.Validate(apply: true, throwOnError: true);
        UnityEngine.Debug.Log(validationReport);
    }
}
```

`LinkXmlValidator.Validate(apply, throwOnError)` reads the current `Assets/link.xml` and returns a
`LinkXmlValidationReport` with removed and kept entries. With `apply: true`, stale entries are written back to the
file. With `throwOnError: true`, a parse failure throws `BuildFailedException`, which aborts the build when called
from a build callback.

### Custom SDK Groups
Link Guard includes built-in SDK patterns for common Unity SDKs. You can add project-specific SDK grouping by
implementing `IKnownSdkProvider`.

```csharp
using System.Collections.Generic;
using System.Text.RegularExpressions;
using DTech.LinkGuard;

public sealed class CustomSdkProvider : IKnownSdkProvider
{
    public IEnumerable<Regex> GetSdkPatterns()
    {
        yield return new Regex("^Company\\.Sdk(\\..+)?$", RegexOptions.Compiled);
    }
}
```

Assemblies matching custom patterns appear in the SDKs group.

### Custom Merge Providers
Beyond the built-in file merge, the toolbar can host any number of custom merge sources. Implement
`DTech.LinkGuard.Editor.ILinkXmlMergeProvider` in any editor assembly and Link Guard discovers it through
`TypeCache` when the window opens — a button is added automatically.

```csharp
using System;
using DTech.LinkGuard.Editor;

internal sealed class MyCustomMergeProvider : ILinkXmlMergeProvider
{
    public string Id => "my-source";
    public string ButtonLabel => "Merge from My Source";
    public string Tooltip => "Pull preserved types from My Source.";

    public LinkXmlProviderResult Provide()
    {
        // Build a <linker> XML string however your source dictates.
        string xml = "<linker><assembly fullname=\"MyAsm\"><type fullname=\"MyAsm.MyType\" preserve=\"all\"/></assembly></linker>";
        return new LinkXmlProviderResult(xml, "Added MyAsm.MyType.", Array.Empty<string>(), success: true);
    }
}
```

The returned XML is merged into the window's tree like any other `link.xml` import; press `Generate link.xml`
afterwards to write the result.

#### Build-time API (all merge providers)
For automated pipelines, run every discovered merge provider at once and write the combined result to
`Assets/link.xml` from a custom build hook:

```csharp
using DTech.LinkGuard.Editor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

internal sealed class LinkXmlBuildHook : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        LinkXmlPatchReport patchReport = LinkXmlPatcher.Patch(throwOnError: true);
        UnityEngine.Debug.Log(patchReport);
    }
}
```

`LinkXmlPatcher.Patch(throwOnError)` discovers all `ILinkXmlMergeProvider` implementations (the built-in file
merge, the Zenject provider when enabled, and any custom providers), merges their output into a single document,
and writes it to `Assets/link.xml` without dialogs. It does not register itself as a build callback — opt in from
your own build script.

Behavior details:
- With `throwOnError: false` (default), a failed provider is logged and skipped while the remaining providers are
  merged and written; inspect `LinkXmlPatchReport.Success` and `Providers` for per-provider outcomes.
- With `throwOnError: true`, any provider failure throws `BuildFailedException` before anything is written, which
  aborts the build when called from a build callback.
- When no provider produces content, `Assets/link.xml` is left untouched (`Written` is `false`).
- The call is idempotent: the file provider re-includes the existing `Assets/link.xml`, so prior content is
  preserved and duplicates are collapsed on every run.
- Call it from `IPreprocessBuildWithReport` (or before `BuildPipeline.BuildPlayer`): managed stripping collects
  `link.xml` files later in the same build, while post-build callbacks are too late.

### Zenject Module
When either `com.svermeulen.extenject` or `com.modesttree.zenject` is present in the project manifest, the
optional Zenject extension is compiled in and adds a **Merge from Zenject Installers** button to the toolbar.

What the scan covers:
- Rooted installers from every `SceneContext` in `EditorBuildSettings.scenes` (enabled scenes only).
- Rooted installers from any addressable scene if the `com.unity.addressables` package is also installed.
- The `Resources/ProjectContext` prefab.
- Any prefab that carries a `GameObjectContext`.
- Transitive `Container.Install<T>()` and `Container.InstallSubContainer<T>()` calls discovered through Mono.Cecil
  IL analysis of `InstallBindings`.
- Concrete types supplied to `Bind<T>()`, `BindInterfacesTo<T>()`, `BindFactory<...>`, `FromInstance(...)` and
  similar binding helpers — including constructor-injected types whose constructor has no `[Inject]` attribute,
  because the IL analyzer reads the actual binding regardless of attribute presence.
- Supplementary `[Inject]`-annotated classes from the same set of reachable assemblies.

Installers that are not referenced from any context are intentionally excluded so that dead-code types do not
leak into `link.xml`.

To exclude a referenced installer from Zenject discovery, mark the installer class with `LinkGuardIgnoreAttribute`.
Ignored installers are not preserved, their bindings are not analyzed, and transitive `Install<T>()` calls from them
are not followed.

```csharp
using DTech.LinkGuard;

[LinkGuardIgnore]
public sealed class DevOnlyInstaller : Zenject.MonoInstaller
{
    public override void InstallBindings()
    {
    }
}
```

#### Build-time API
For automated pipelines, call the patcher from a custom build hook:

```csharp
using DTech.LinkGuard.Editor.Zenject;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

internal sealed class ZenjectLinkXmlBuildHook : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        ZenjectPatchReport patchReport = ZenjectLinkXmlPatcher.Patch();
        UnityEngine.Debug.Log(patchReport);
    }
}
```

`ZenjectLinkXmlPatcher.Patch(linkXmlPath)` loads the existing `link.xml` (or creates one), runs the same scan
as the toolbar button, and writes the merged document back. It does not show any dialogs and does not register
itself as a build callback — opt in from your own build script.

### ProGuard Rules
Switch to the **ProGuard** tab to generate Android R8/ProGuard `-keep` rules. ProGuard shrinks Java/Kotlin code, so
this tab scans Android artifacts rather than managed assemblies:

- `.aar` plugins (including their nested `classes.jar` and `libs/*.jar`)
- `.androidlib` folders
- standalone `.jar` plugins
- loose Java/Kotlin sources under Android plugin folders

Press `Refresh`, select artifacts, packages, or classes to keep, then press `Generate ProGuard`. A class selection
becomes `-keep class <fqcn> { *; }`, a package becomes `-keep class <package>.** { *; }`, and a whole artifact
collapses to one rule per root package.

The rules are written to `Assets/Plugins/Android/proguard-user.txt` and `PlayerSettings.Android.useCustomProguardFile`
is enabled so Unity feeds the file to the build automatically.

The `Generate ProGuard` button is shown only when the **active build target is Android**. A notice is displayed when
Android minification (R8) is disabled in Player Settings — rules are still generated but are not applied until you
enable Minify.

#### Build-time API (ProGuard)
For automated pipelines, call the patcher from a custom build hook:

```csharp
using DTech.LinkGuard.Editor.ProGuard;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

internal sealed class ProGuardBuildHook : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        ProGuardPatchReport patchReport = ProGuardPatcher.Patch();
        UnityEngine.Debug.Log(patchReport);
    }
}
```

`ProGuardPatcher.Patch(path)` scans every Android artifact, keeps all discovered classes, and writes the rules file.
It does not show any dialogs, does not register itself as a build callback, and skips with a log when Android
minification is disabled.

## License
This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
