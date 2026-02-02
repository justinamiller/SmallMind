# Branch Protection Setup Guide

This guide explains how to configure branch protection rules to ensure that only approved changes can be merged to the `main` branch.

## Why Branch Protection?

Branch protection rules help maintain code quality and stability by:
- Preventing direct pushes to the main branch
- Requiring pull request reviews before merging
- Ensuring CI/CD tests pass before merging
- Preventing accidental deletions of important branches
- Enforcing code review and approval workflows

## Setting Up Branch Protection Rules

### Step 1: Navigate to Branch Protection Settings

1. Go to your GitHub repository: https://github.com/justinamiller/SmallMind
2. Click on **Settings** (in the repository menu)
3. Click on **Branches** (in the left sidebar)
4. Under "Branch protection rules", click **Add rule** or **Add branch protection rule**

### Step 2: Configure the Rule for `main` Branch

#### Branch Name Pattern
- Enter `main` in the "Branch name pattern" field

#### Protection Settings (Recommended)

Check the following options:

##### ✅ **Require a pull request before merging**
This prevents anyone from pushing directly to `main`.

- ✅ **Require approvals**: Set to at least 1 (or more for larger teams)
- ✅ **Dismiss stale pull request approvals when new commits are pushed**: Ensures reviews are current
- ⚪ **Require review from Code Owners**: Optional, requires CODEOWNERS file (see below)
- ⚪ **Restrict who can dismiss pull request reviews**: Optional for larger teams
- ⚪ **Allow specified actors to bypass required pull requests**: Use sparingly for emergency fixes

##### ✅ **Require status checks to pass before merging**
This ensures CI/CD builds and tests pass.

- ✅ **Require branches to be up to date before merging**: Prevents merge conflicts
- Add status checks:
  - `build-and-test` (from build.yml workflow)
  - Any other critical checks you want to enforce

##### ✅ **Require conversation resolution before merging**
Ensures all review comments are addressed.

##### ✅ **Require signed commits** (Optional but recommended)
Enhances security by requiring commit signatures.

##### ✅ **Require linear history** (Optional)
Prevents merge commits, enforcing a cleaner git history.

##### ✅ **Include administrators**
Applies rules to repository administrators (recommended for consistency).

##### ⚪ **Allow force pushes** 
**NOT RECOMMENDED** - Keep this disabled to prevent history rewriting.

##### ⚪ **Allow deletions**
**NOT RECOMMENDED** - Keep this disabled to prevent accidental branch deletion.

### Step 3: Save Changes

Click **Create** or **Save changes** at the bottom of the page.

## Recommended Configuration

Here's the recommended minimal setup for SmallMind:

```yaml
Branch: main

Required Settings:
✅ Require a pull request before merging
  ✅ Require approvals: 1
  ✅ Dismiss stale pull request approvals when new commits are pushed
  
✅ Require status checks to pass before merging
  ✅ Require branches to be up to date before merging
  Status checks required:
    - build-and-test
    
✅ Require conversation resolution before merging

❌ Allow force pushes: DISABLED
❌ Allow deletions: DISABLED
```

## Code Owners (Optional Enhancement)

For additional control, create a `CODEOWNERS` file in the repository root or `.github/` directory:

```
# Default owner for everything
* @justinamiller

# Core library requires review
/src/SmallMind.Core/ @justinamiller
/src/SmallMind.Transformers/ @justinamiller

# CI/CD requires review
/.github/ @justinamiller
```

When "Require review from Code Owners" is enabled, designated code owners must approve PRs affecting their areas.

## Workflow Integration

The existing `build.yml` workflow automatically runs on pull requests:

```yaml
on:
  pull_request:
    branches: [ main, develop ]
```

This ensures that:
1. All tests pass before merging
2. Code builds successfully
3. Integration tests validate functionality

## Verifying Protection Rules

After setup, verify the rules work:

1. Try to push directly to `main`:
   ```bash
   git checkout main
   git commit --allow-empty -m "Test commit"
   git push origin main
   ```
   This should be **rejected** with a message like:
   ```
   remote: error: GH006: Protected branch update failed
   ```

2. Create a PR and verify:
   - ✅ Status checks run automatically
   - ✅ At least one approval is required
   - ✅ Cannot merge until checks pass

## Workflow for Contributors

With branch protection enabled, the workflow becomes:

1. **Fork or branch**: Create a feature branch
   ```bash
   git checkout -b feature/my-feature
   ```

2. **Develop**: Make changes and commit
   ```bash
   git add .
   git commit -m "Add my feature"
   ```

3. **Push**: Push to your branch
   ```bash
   git push origin feature/my-feature
   ```

4. **Create PR**: Open a pull request to `main`

5. **CI/CD**: Automated checks run (build, test)

6. **Review**: Get approval from reviewer(s)

7. **Merge**: Merge via GitHub UI once approved and checks pass

## Emergency Procedures

In case of urgent fixes:

### Option 1: Fast-track PR
- Create emergency branch
- Open PR with `[URGENT]` prefix
- Request immediate review
- Merge once approved (still requires checks to pass)

### Option 2: Temporarily Disable Protection (NOT RECOMMENDED)
Only for critical production issues:
1. Repository admin temporarily disables rule
2. Makes emergency fix
3. Immediately re-enables rule
4. Creates post-mortem PR to document change

**Always prefer Option 1** - it maintains audit trail and accountability.

## Troubleshooting

### Problem: Status checks not showing up
**Solution**: Ensure the workflow has run at least once on a PR. GitHub needs to see the check before it can be required.

### Problem: Cannot require certain status checks
**Solution**: The status check must have been reported in a previous PR. Create a test PR to trigger the workflow first.

### Problem: Accidentally locked yourself out
**Solution**: Repository admins can temporarily disable protection rules, make necessary changes, then re-enable.

### Problem: PR approved but still can't merge
**Solution**: Check that:
- All required status checks have passed (green ✓)
- All conversations are resolved
- Branch is up to date with base branch

## Best Practices

1. **Start Simple**: Begin with basic protection (PR required, 1 approval)
2. **Add Gradually**: Add more checks as team grows
3. **Document Exceptions**: If you bypass rules, document why
4. **Regular Audits**: Review protection rules quarterly
5. **Educate Team**: Ensure all contributors understand the workflow
6. **Monitor Metrics**: Track PR turnaround time and merge frequency

## Additional Resources

- [GitHub Branch Protection Documentation](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-protected-branches/about-protected-branches)
- [Managing Code Owners](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/about-code-owners)
- [GitHub Actions Status Checks](https://docs.github.com/en/pull-requests/collaborating-with-pull-requests/collaborating-on-repositories-with-code-quality-features/about-status-checks)

## Summary

Branch protection for `main` ensures:
- ✅ No direct pushes to main
- ✅ All changes reviewed via pull requests
- ✅ Automated tests pass before merge
- ✅ Code quality standards maintained
- ✅ Audit trail for all changes

Follow this guide to set up branch protection and maintain high code quality in your SmallMind repository!
