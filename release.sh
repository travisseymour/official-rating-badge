#!/bin/bash
# Auto-increment patch version and push a new release tag

set -e

# Get the latest tag
LATEST_TAG=$(git describe --tags --abbrev=0 2>/dev/null || echo "v1.0.0")

# Extract version numbers
VERSION=${LATEST_TAG#v}
MAJOR=$(echo "$VERSION" | cut -d. -f1)
MINOR=$(echo "$VERSION" | cut -d. -f2)
PATCH=$(echo "$VERSION" | cut -d. -f3)

# Increment patch version
NEW_PATCH=$((PATCH + 1))
NEW_TAG="v${MAJOR}.${MINOR}.${NEW_PATCH}"

echo "Latest tag: $LATEST_TAG"
echo "New tag:    $NEW_TAG"
echo ""

read -p "Create and push $NEW_TAG? [y/N] " -n 1 -r
echo ""

if [[ $REPLY =~ ^[Yy]$ ]]; then
    git tag "$NEW_TAG"
    git push origin "$NEW_TAG"
    echo ""
    echo "Done! $NEW_TAG pushed. Check GitHub Actions for build status."
else
    echo "Aborted."
fi
