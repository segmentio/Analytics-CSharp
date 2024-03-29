Update Version
==========
* update `<Version>` value in `Analytics-CSharp.csproj`
* update `SegmentVersion` value in `Segment/Analytics/Version.cs`

Release to Nuget
==========
1. Create a new branch called `release/X.Y.Z`
2. `git checkout -b release/X.Y.Z`
3. Change the version in `Analytics-CSharp.csproj` to your desired release version (see `Update Version`)
4. `git commit -am "Create release X.Y.Z."` (where X.Y.Z is the new version)
5. `git tag -a X.Y.Z -m "Version X.Y.Z"` (where X.Y.Z is the new version)
6. The CI pipeline will recognize the tag and upload the artifacts to nuget and generate changelog automatically
7. Push to github with `git push && git push --tags`
8. Create a PR to merge to main

Release to OpenUPM
==========
follow the instruction above to `Release to Nuget`. once the new version is available in Nuget and PR merged to main, run the following command in the root of the project:
```bash
sh upm_release.sh <directory>
```
NOTE: `<directory>` is a required folder to setup sandbox for release. it should be **outside** the project folder.

the script will setup a sandbox to pack the artifacts and create a `unity/<version>` tag on github. OpenUPM checks the `unity/<version>` tag periodically and create a release automatically.

Pre-release
==========
Pre-release is useful when testing code compatibility on Unity. To make a pre-release, update the version tag with a suffix of `-alpha.<v>` where `<v>` is the version number of this alpha release. The following is a list of valid pre-release versions:
* `2.0.0-alpha.1`
* `2.0.0-alpha.2`
* `2.0.0-alpha.12`

The rest of the pre-release progress is the same as a regular release.
