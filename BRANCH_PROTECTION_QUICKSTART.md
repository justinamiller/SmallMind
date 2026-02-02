# Quick Start: Enabling Branch Protection

This is a quick reference guide for the repository maintainer to enable branch protection for the `main` branch.

## ⚡ 5-Minute Setup

### Step 1: Go to Branch Protection Settings (1 minute)

1. Navigate to: https://github.com/justinamiller/SmallMind/settings/branches
2. Click **"Add branch protection rule"**
3. Enter `main` in the "Branch name pattern" field

### Step 2: Enable Core Protection (2 minutes)

Check these essential boxes:

```
✅ Require a pull request before merging
  ✅ Require approvals: 1
  ✅ Dismiss stale pull request approvals when new commits are pushed

✅ Require status checks to pass before merging
  ✅ Require branches to be up to date before merging
  Search for and add these status checks:
    - build-and-test (from build.yml)
    - check-pr-requirements (from pr-requirements.yml)

✅ Require conversation resolution before merging
```

### Step 3: Lockdown (1 minute)

Scroll down and ensure these are **DISABLED**:

```
❌ Allow force pushes (keep unchecked)
❌ Allow deletions (keep unchecked)
```

### Step 4: Apply to Everyone (30 seconds)

```
✅ Do not allow bypassing the above settings (check this box)
```

OR

```
✅ Include administrators (recommended - even admins follow the rules)
```

### Step 5: Save (30 seconds)

Click **"Create"** or **"Save changes"** at the bottom.

---

## ✅ Verification (2 minutes)

Test that it works:

```bash
# This should FAIL:
git checkout main
git commit --allow-empty -m "Test commit"
git push origin main

# Expected error:
# remote: error: GH006: Protected branch update failed for refs/heads/main.
```

If you see that error, **SUCCESS!** ✅ Branch protection is working.

---

## 📋 Status Check Names

If the status checks don't appear in the dropdown, they need to run at least once. They are:

1. **`build-and-test`** - From `.github/workflows/build.yml`
   - Runs on every PR automatically
   
2. **`check-pr-requirements`** - From `.github/workflows/pr-requirements.yml`
   - NEW workflow added in this PR
   - Will run on the next PR

**Note:** After merging this PR, the status checks will be available in the dropdown for future PRs.

---

## 🎯 What This Achieves

After enabling these settings:

- ✅ No one (including you) can push directly to `main`
- ✅ All changes require a pull request
- ✅ All PRs must pass automated tests
- ✅ All PRs require at least 1 approval
- ✅ All PR conversations must be resolved
- ✅ Branches must be up-to-date before merge
- ✅ No force pushes or branch deletions allowed

---

## 📚 Need More Details?

See the comprehensive guide: [docs/BRANCH_PROTECTION_SETUP.md](docs/BRANCH_PROTECTION_SETUP.md)

---

## 🆘 Troubleshooting

### Problem: "Status checks not found"
**Solution:** Status checks need to run at least once. Merge this PR first, then the checks will appear.

### Problem: "I can't find the settings page"
**Solution:** You need admin access. Go to: Repository → Settings (top menu) → Branches (left sidebar)

### Problem: "I locked myself out"
**Solution:** As an admin, you can temporarily disable the protection rule, make your change, then re-enable it. But consider: is the change important enough to bypass the process?

### Problem: "The checks are taking too long"
**Solution:** The build and test workflow can take 5-10 minutes. This is normal. You can see progress in the PR's "Checks" tab.

---

## 💡 Pro Tips

1. **Start with minimal protection**, then add more rules as needed
2. **Communicate the change** to your team before enabling
3. **Lead by example** - even as the owner, use PRs for your changes
4. **Review the PR template** - consider adding one to guide contributors

---

## 🚀 What's Next?

After enabling branch protection:

1. ✅ Test it with a dummy PR
2. ✅ Update your team/contributors about the new workflow
3. ✅ Consider setting up PR templates (optional)
4. ✅ Monitor PR turnaround time and adjust as needed

---

**Time to enable: ~5 minutes**  
**Impact: Huge improvement in code quality and stability** 🎉
