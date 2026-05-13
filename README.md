# Link Guard
[![Unity Version](https://img.shields.io/badge/unity-6000.0+-000.svg)](https://unity.com/releases/editor/archive)

## Overview
Link Guard is a Unity editor tool for building `link.xml` files used by managed code stripping and IL2CPP builds.
It scans project assemblies, plugins, UPM packages, known SDKs, and Unity modules, then lets you choose which
assemblies or types should be preserved.

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
    - [Custom SDK Groups](#custom-sdk-groups)
    - [Custom Merge Providers](#custom-merge-providers)
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

If you want to set a target version, Link Guard uses the `v*.*.*` release tag so you can specify a version like #v1.0.0.

For example `https://github.com/DanilChizhikov/link-guard.git#v1.0.0`.

## Features
- Assembly scanning for project code, plugins, UPM packages, known SDKs, and Unity modules
- Grouped tree view with assembly, namespace, and type selection
- Search by assembly, namespace, or type name
- `link.xml` generation to `Assets/link.xml`
- Optional preview of generated XML before writing
- Save and load selection profiles
- Import the current `Assets/link.xml` when the window opens (legacy method-level entries are promoted to whole-type `preserve="all"` with a warning in the Console)
- Merge existing `link.xml` files from `Assets` and `Packages`
- Preserve unknown entries and custom XML attributes when importing or merging
- `ignoreIfMissing` support for assembly entries
- Custom SDK grouping through `IKnownSdkProvider`

## Usage

### Open the Window
Open `Window/DTech/Link XML Generator`.

Press `Refresh` to scan assemblies. The tree is grouped by source:

- Project assemblies
- Plugins folder
- UPM packages
- SDKs
- Unity built-in modules
- Merged `link.xml` entries

### Generate link.xml
1. Use the search field to find assemblies, namespaces, or types.
2. Select the entries that must be preserved. The smallest selectable unit is a type — once selected, the type is written with `preserve="all"`.
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

## License
This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
