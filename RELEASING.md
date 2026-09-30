Releasing
=========

Publishing runs in CI. `deploy.yml` triggers on a pushed tag, verifies it against
the version in the source, builds, packs and pushes to NuGet.org, then creates the
GitHub release.

It publishes through NuGet **trusted publishing**: the workflow exchanges a GitHub
OIDC token for an API key that lives one hour, so there is no long-lived key to
store or rotate. nuget.org matches the exchange against a policy naming the
repository, the workflow file (`deploy.yml`) and the environment (`deployment`) —
so renaming any of those breaks publishing until the policy is updated to match.

Update the version in **both** places — the deploy workflow checks both and stops
if either disagrees with the tag:

* `<Version>` in `Analytics-CSharp/Analytics-CSharp.csproj` — the package version
* `SegmentVersion` in `Analytics-CSharp/Segment/Analytics/Version.cs` — what the
  library reports at runtime, through `Analytics.Version` and the library version
  on every event's context

Release to NuGet
================

1. `git checkout -b release/X.Y.Z`
2. Update both versions above.
3. Update `CHANGELOG.md`.
4. `git commit -am "Release X.Y.Z."`
5. Open a PR and merge it to `main`.
6. Tag the merged commit and push it — no `v` prefix:

   ```
   git tag X.Y.Z && git push origin X.Y.Z
   ```

7. Approve the `deployment` environment when the publish job requests review.

Tag after merging, so the tag points at `main` rather than at a branch commit
that may differ from what was reviewed.

Checking the credential path without releasing
==============================================

The publish path only runs at release time, so a broken credential is normally
discovered by a failed release. To check it first, run **Deploy** manually from
the Actions tab.

A manual run exchanges the OIDC token for a nuget.org key, reports whether that
worked, and stops — it does not pack, publish or create a release. Everything
before the exchange still runs, so it also covers Artifactory auth, the build and
the tests.

It has to be this workflow rather than a separate one: the trusted publishing
policy matches on the workflow filename, so a different file fails to match even
when everything else is right.

The optional `nuget_user` input overrides the `NUGET_USER` secret for that run,
which is useful when confirming which nuget.org profile the policy is registered
under. It is a profile name, never an email address.

Release to OpenUPM
==================

Once the new version is live on NuGet and the PR is merged to `main`, run from the
project root:

```bash
sh upm_release.sh <directory>
```

`<directory>` is a scratch folder for the release sandbox and must be **outside**
the project folder. The script packs the artifacts and creates a `unity/<version>`
tag; OpenUPM polls for that tag and publishes automatically.

Pre-release
===========

Useful for testing compatibility on Unity. Use a tag with an `-alpha.<n>` suffix —
`2.0.0-alpha.1`, `2.0.0-alpha.12` — which `deploy.yml` also accepts. The rest of
the process is unchanged.
