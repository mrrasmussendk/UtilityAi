# Release Process

This document describes how to create releases for the UtilityAI framework.

## Overview

The project uses GitHub Actions to automate building, testing, and releasing. The workflow supports four release methods:

1. **Automatic Release on Push to Main** (recommended) — bump the version in csproj and push
2. **Tag-Based Release** (traditional) — push a `v*` tag
3. **Manual Release via Workflow Dispatch** — trigger from GitHub Actions UI
4. **Preview Builds** on every push to main (when version hasn't changed)

## Release Methods

### 1. Automatic Release on Push to Main (recommended)

When you bump the `<Version>` in `src/UtilityAi/UtilityAi.csproj` and push to `main`, the pipeline automatically:
- ✅ Builds and tests the project
- ✅ Detects the new version (no existing tag for that version)
- ✅ Creates NuGet packages with the csproj version
- ✅ Creates a git tag (e.g., `v1.3.0`)
- ✅ Creates a GitHub release with auto-generated notes
- ✅ Publishes to NuGet.org

**How to release:**
1. Bump the `<Version>` in `src/UtilityAi/UtilityAi.csproj`
2. Commit and push to `main` (or merge a PR)
3. The pipeline handles the rest automatically

### 2. Tag-Based Release

Push a version tag to trigger a release:

```bash
# Create and push a version tag
git tag v1.2.3
git push origin v1.2.3
```

This will:
- ✅ Build and test the project
- ✅ Create a NuGet package with version `1.2.3`
- ✅ Upload artifacts
- ✅ Create a GitHub release with auto-generated notes
- ✅ Publish to NuGet.org

### 3. Manual Release via Workflow Dispatch

You can now trigger a release manually without creating a tag first:

1. Go to **Actions** → **Build and Release** → **Run workflow**
2. Enter the version (e.g., `1.2.3`)
3. Check **"Create GitHub release and publish to NuGet"** if you want to publish
4. Click **Run workflow**

This will:
- ✅ Build and test the project
- ✅ Create a NuGet package with your specified version
- ✅ Upload artifacts
- ✅ Optionally create a GitHub release and publish to NuGet (if checkbox is checked)

**Use cases:**
- Creating a release without pushing a tag
- Testing the release process
- Creating a package for local testing (leave checkbox unchecked)
- Retroactively releasing a specific commit with a version

### 4. Preview Builds

Every push to the `main` branch automatically creates a preview package:

```
Version format: {csproj-version}-preview.{timestamp}
Example: 1.1.4-preview.20260218120000
```

These preview packages:
- ✅ Are built and tested automatically
- ✅ Have artifacts uploaded for download
- ❌ Are NOT published to NuGet
- ❌ Do NOT create GitHub releases

**Use cases:**
- Testing changes before release
- Sharing pre-release builds with collaborators

## Current Version

The current versions are stored in each project's `.csproj`:

- `src/UtilityAi/UtilityAi.csproj`:
  ```xml
  <Version>1.2.4</Version>
  ```
- `integrations/UtilityAi.Maf/UtilityAi.Maf.csproj`:
  ```xml
  <Version>1.0.0</Version>
  ```

Both packages are built, packed, and published together. The same version override is applied to both during release.

## Versioning Guidelines

Follow [Semantic Versioning](https://semver.org/):

- **MAJOR** version (1.x.x) - Incompatible API changes
- **MINOR** version (x.1.x) - Backward-compatible functionality additions
- **PATCH** version (x.x.1) - Backward-compatible bug fixes

## Workflow Features

### Always Runs
- ✅ Restore dependencies
- ✅ Build (Release configuration)
- ✅ Run tests

### Conditional Steps
- **Pack** - Runs on all pushes to main (not PRs)
- **Upload artifacts** - Runs on all pushes to main (not PRs)
- **Release** - Only runs for:
  - Push to main with a new version in csproj (auto-release)
  - Tag pushes (v*)
  - Manual workflow dispatch with "create_release" checked

## Troubleshooting

### "NuGet API key not found"
Ensure the `NUGET_API_KEY` secret is configured in repository settings.

### "Version already exists"
The workflow uses `--skip-duplicate` flag, so this is usually safe to ignore.

### Want to test packing without publishing?
Use manual workflow dispatch and leave the "Create GitHub release" checkbox unchecked.

## Examples

### Example 1: Regular Release
```bash
# Update version in csproj
# Commit changes
git add src/UtilityAi/UtilityAi.csproj
git commit -m "Bump version to 1.2.0"
git push

# Create and push tag
git tag v1.2.0
git push origin v1.2.0
```

### Example 2: Manual Release
1. Navigate to Actions → Build and Release
2. Click "Run workflow"
3. Enter version: `1.2.0`
4. Check "Create GitHub release and publish to NuGet"
5. Click "Run workflow"

### Example 3: Testing Package Creation
1. Navigate to Actions → Build and Release
2. Click "Run workflow"
3. Enter version: `1.2.0-test`
4. Leave "Create GitHub release" UNCHECKED
5. Click "Run workflow"
6. Download artifacts from the workflow run
