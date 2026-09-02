# Installer

There is no MSI here, and that is deliberate.

AutoCAD has its own deployment mechanism for plugins: a folder ending in `.bundle`, containing a
`PackageContents.xml` manifest, placed in one of the `ApplicationPlugins` locations. AutoCAD reads
the manifest at start-up and loads the build matching its own release. Wrapping that in an installer
would add a thing to trust, a thing to sign and a thing to go wrong, in exchange for saving a copy
and paste.

So:

- **`build/build.ps1`** produces the bundle.
- **`installer/install.ps1`** copies it into place, checking the mistakes that are easy to make by
  hand — pointing at the wrong folder level, a missing manifest, or AutoCAD still running and
  holding the old assemblies open.
- **Uninstalling** is deleting the folder.

```powershell
./build/build.ps1
./installer/install.ps1              # current user
./installer/install.ps1 -AllUsers    # every user, needs administrator rights
```

Doing it by hand is equally fine, and `docs/INSTALLATION.md` describes exactly that, along with what
to check when the command is not recognised.
