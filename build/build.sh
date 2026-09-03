#!/usr/bin/env bash
#
# Builds and tests the platform-neutral half of StructureGen.
#
# The AutoCAD and WPF layers cannot be built here: both require Windows, and the AutoCAD layer
# additionally needs the Autodesk reference assemblies, which restore from nuget.org but only
# compile on Windows. Use build/build.ps1 on Windows to produce the installable bundle.
#
# What this script does cover is every layer that decides what gets drawn, what it weighs and how
# it is reported, which is where the logic worth testing lives - plus a compile check that puts the
# AutoCAD and user-interface sources through the compiler against stand-in AutoCAD and WPF types,
# so their calls into StructureGen's own code are verified here too. See
# tests/StructureGen.CompileCheck/README.md.

set -euo pipefail

repository="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
configuration="${1:-Release}"

echo "==> Restoring"
dotnet restore "$repository/StructureGen.Portable.slnf"

echo
echo "==> Building ($configuration)"
dotnet build "$repository/StructureGen.Portable.slnf" -c "$configuration" --no-restore --nologo

echo
echo "==> Testing"
dotnet test "$repository/tests/StructureGen.Tests" -c "$configuration" --no-build --nologo

echo
echo "==> Validating the shipped data files"
for database in structural-sections aisc-sections structural-sections-imperial-demo; do
    dotnet run --project "$repository/src/StructureGen.Tools.DataImport" -c "$configuration" --no-build -- \
        validate --in "$repository/data/$database.json"
done

echo
echo "Done. To produce the installable AutoCAD bundle, run build/build.ps1 on Windows."
