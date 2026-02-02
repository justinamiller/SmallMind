# GitHub Actions Workflows

This directory contains GitHub Actions workflow files that automate various tasks for the SmallMind repository.

## Available Workflows

### 1. Build and Test (`build.yml`)
**Triggers:**
- Push to `main` or `develop` branches
- Pull requests to `main` or `develop` branches
- Scheduled daily at 2:00 AM UTC

**Purpose:** Runs comprehensive testing including unit tests, integration tests, and performance tests.

**Key Features:**
- .NET 10.x setup
- Dependency restoration
- Build in Release configuration
- Unit and integration test execution
- Performance tests (on schedule or with label)
- SIMD benchmarks (on schedule or with label)
- Test result publishing

---

### 2. Release to NuGet (`release.yml`)
**Triggers:**
- GitHub release published
- Manual workflow dispatch

**Purpose:** Publishes SmallMind packages to NuGet.

**Key Features:**
- Multi-project packaging (SmallMind, Core, Transformers, Tokenizers, Runtime, Rag, Quantization)
- Automated version handling from release tags
- NuGet push with API key
- Package artifact upload

**Required Secrets:**
- `NUGET_API_KEY`: Your NuGet API key for publishing

---

### 3. Cleanup Merged Branches (`cleanup-merged-branches.yml`) 🆕
**Triggers:**
- Scheduled daily at 3:00 AM UTC
- Manual workflow dispatch

**Purpose:** Automatically deletes merged branches that are older than 1 day to keep the repository clean.

**Key Features:**
- ✅ Deletes only merged branches (merged into `main`)
- ✅ Only deletes branches older than 1 day
- ✅ **Always protects the `main` branch**
- ✅ Uses GitHub API for reliable deletion
- ✅ Provides detailed logging and summary
- ✅ Can be manually triggered for testing

**How It Works:**
1. Fetches all remote branches
2. Identifies branches merged into `main`
3. Checks the last commit date of each branch
4. Deletes branches with commits older than 1 day
5. Skips `main` branch (protected)
6. Generates a summary report

**Safety Features:**
- Double-checks to never delete `main`
- Only deletes branches that have been merged
- Only deletes branches with no activity for >24 hours
- Uses authenticated GitHub API calls
- Provides detailed logging for audit trail

**Manual Triggering:**
You can manually trigger this workflow from the Actions tab:
1. Go to Actions → Cleanup Merged Branches
2. Click "Run workflow"
3. Select the branch (usually `main`)
4. Click "Run workflow"

**Permissions:**
- `contents: write` - Required to delete branches via GitHub API

**Example Output:**
```
🔍 Scanning for merged branches older than 1 day...
📅 Reference date (1 day ago): 2026-02-01 03:00:00

🔍 Merged branches found:
  - feature/old-feature
  - bugfix/old-bugfix

📊 Branch analysis:
  🗑️  Deleting branch: feature/old-feature (last commit: 2026-01-30 15:30:00)
  ✅ Successfully deleted branch: feature/old-feature
  
  ⏭️  Skipping branch (too recent): bugfix/old-bugfix (last commit: 2026-02-01 10:00:00)

✅ Branch cleanup completed successfully!
📊 Summary:
  - Deleted: 1 branches
  - Skipped: 1 branches
```

---

## Workflow Permissions

All workflows use the `GITHUB_TOKEN` secret which is automatically provided by GitHub Actions. The token has different permissions based on workflow needs:

- **Build and Test:** `checks: write`, `contents: read`, `pull-requests: write`
- **Release:** Default permissions (configurable at repository level)
- **Cleanup Merged Branches:** `contents: write`

## Best Practices

1. **Test workflows manually** before relying on scheduled runs
2. **Monitor workflow runs** regularly in the Actions tab
3. **Review logs** if a workflow fails
4. **Keep secrets secure** - never commit API keys or tokens
5. **Update action versions** periodically for security patches

## Troubleshooting

### Cleanup Merged Branches Not Running
- Check that the workflow file is in the `main` branch
- Verify GitHub Actions are enabled for the repository
- Check the Actions tab for any error messages
- Manually trigger the workflow to test

### Cleanup Deleting Wrong Branches
The workflow has multiple safety checks:
- Only deletes merged branches
- Only deletes branches older than 1 day
- Always skips `main`
- Provides detailed logs of what would be deleted

If you notice issues:
1. Check the workflow logs to see what branches were analyzed
2. Verify the branch was actually merged into `main`
3. Check the last commit date on the branch

### Permission Errors
If you see "Resource not accessible by integration" errors:
1. Check repository Settings → Actions → General
2. Ensure "Workflow permissions" is set to "Read and write permissions"
3. Or explicitly grant `contents: write` permission in the workflow file (already configured)

## Contributing

When adding new workflows:
1. Place them in `.github/workflows/`
2. Use descriptive names (e.g., `verb-noun.yml`)
3. Add comprehensive comments
4. Test with `workflow_dispatch` trigger first
5. Update this README with workflow documentation
6. Consider security implications of permissions
7. Use existing action versions as reference

## Resources

- [GitHub Actions Documentation](https://docs.github.com/en/actions)
- [Workflow Syntax Reference](https://docs.github.com/en/actions/reference/workflow-syntax-for-github-actions)
- [GitHub Actions Permissions](https://docs.github.com/en/actions/security-guides/automatic-token-authentication#permissions-for-the-github_token)
