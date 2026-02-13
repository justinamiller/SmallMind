# CodeQL Alerts - How They Work and Auto-Remediation

## Understanding CodeQL Alert Lifecycle

### How CodeQL Alerts are Created and Resolved

CodeQL alerts on GitHub **do go away automatically** when you fix the code, but not instantly - they close on the **next successful scan** after your fix is merged. Here's how the lifecycle works:

#### 1. **Alert Creation**
- CodeQL scans run on every push to `main` (or on a schedule)
- When CodeQL finds an issue, it creates an **alert** in the Security tab
- Each alert is assigned a unique ID and fingerprint based on the code location and issue type

#### 2. **Alert Resolution** - Three Ways:

##### a) **Automatic Resolution** (Code Fix)
When you fix the code that caused the alert:
1. Push the fix to the main branch
2. CodeQL runs again on the next push/schedule
3. CodeQL **compares the current scan** with previous scans
4. If the vulnerable code is **no longer present**, the alert is automatically marked as **"Fixed"**
5. The alert status changes from "Open" → "Fixed by commit [SHA]"

**Timeline**: Alerts are closed automatically on the **next successful CodeQL scan** after the fix is merged to main.

##### b) **Manual Dismissal** (False Positives)
For false positives or accepted risks:
1. Navigate to Security → Code scanning → Click the alert
2. Click "Dismiss alert" button
3. Select a reason:
   - False positive
   - Won't fix
   - Used in tests
4. Add optional comment explaining why
5. Alert is marked as "Dismissed"

##### c) **Automatic Closure** (Code Removed)
If the entire file or function containing the alert is deleted:
- CodeQL automatically closes the alert as "Fixed"
- This happens on the next scan after deletion

### Your Current Situation

Based on your description, you've:
1. ✅ Addressed the code issues
2. ✅ Pushed fixes to the main branch
3. ❓ Wondering why alerts are still showing

### Why Alerts May Still Be Visible

There are several reasons alerts might still appear:

#### 1. **CodeQL Scan Hasn't Completed Yet**
- Check if the latest CodeQL workflow on main has completed successfully
- Go to: `Actions` → Filter by `CodeQL` workflow
- Verify the most recent run for the main branch shows "success"

#### 2. **CodeQL Detected New Issues**
- Your fixes might have resolved some issues but introduced new ones
- Check the alert dates - newer alerts mean new issues were found

#### 3. **Alerts Need Manual Dismissal**
- If the issues are false positives, you need to manually dismiss them
- CodeQL won't auto-dismiss alerts you consider invalid

#### 4. **Code Still Contains the Issue**
- The fix might not have fully addressed the vulnerability
- Review the specific alerts to ensure your changes targeted the exact code location

#### 5. **Workflow Configuration Issues**
- The CodeQL workflow might have errors or timeout issues
- Check the workflow logs for any failures

## How to Investigate Your Current Alerts

### Step 1: Check Recent CodeQL Runs
```bash
# View recent CodeQL workflow runs
gh run list --workflow=codeql.yml --limit 10

# View specific run details
gh run view <run-id>
```

Or visit: `https://github.com/justinamiller/SmallMind/actions/workflows/codeql.yml`

### Step 2: View Current Alerts
Visit: `https://github.com/justinamiller/SmallMind/security/code-scanning`

Look for:
- **Open** alerts (still need attention)
- **Fixed** alerts (successfully resolved)
- **Dismissed** alerts (manually closed)

### Step 3: Compare Alert Details
For each open alert:
1. Note the file path and line number
2. Check if that code still exists in main
3. Verify if your fix actually addressed that specific issue
4. Check the alert creation date vs. your fix commit date

## Checking Your Repository Status

Based on the workflow runs I can see:

```
Recent CodeQL runs on main branch:
- Run #116: 2026-02-13 23:02:19 - ✅ SUCCESS
- Run #115: 2026-02-13 22:50:41 - ✅ SUCCESS
- Run #114: 2026-02-13 22:42:55 - ✅ SUCCESS
- Run #113: 2026-02-13 22:30:26 - ✅ SUCCESS
```

The CodeQL scans are running successfully on main! This means:
1. ✅ CodeQL is actively scanning your main branch
2. ✅ Any code fixes you've pushed should be analyzed
3. ✅ Alerts should be auto-closing if the code is truly fixed

## What to Do Next

### Option 1: Review Specific Alerts (RECOMMENDED)
I cannot directly access your Security tab due to API permissions, but you can:

1. Go to: `https://github.com/justinamiller/SmallMind/security/code-scanning`
2. Filter by "Open" alerts
3. For each alert:
   - Check if the vulnerable code still exists
   - If it's fixed, wait for the next CodeQL scan
   - If it's a false positive, dismiss it with a reason
   - If it's still an issue, let me know the details

### Option 2: Share Specific Alerts
If you can share the details of specific alerts that are still showing, I can:
- Analyze the code to see if it's truly fixed
- Help determine if it's a false positive
- Suggest the appropriate fix or dismissal reason

### Option 3: Run CodeQL Locally
You can run CodeQL locally to verify:
```bash
# This would require CodeQL CLI setup
# But the GitHub Actions are already doing this automatically
```

## Summary

**To answer your question:**

> "Do they go away automatically or is this something I have to manage manually?"

**Answer**: CodeQL alerts go away **automatically** when:
1. You fix the underlying code issue
2. Push the fix to the main branch
3. CodeQL runs and scans the updated code
4. CodeQL detects the issue is resolved

**However**, you **may need to manually dismiss** alerts that are:
- False positives
- Intentional design decisions
- Test code (acceptable risk)
- Won't fix (accepted technical debt)

**Current Status**: Your CodeQL workflows are running successfully. If alerts are still showing, they either:
1. Haven't been scanned yet (unlikely given successful runs)
2. Are genuinely still present in the code
3. Need manual dismissal as false positives

## Recommendations

1. **Check the Security Tab**: Visit the code scanning alerts page to see current status
2. **Verify Your Fixes**: Ensure your code changes actually addressed the flagged lines
3. **Manual Dismissal**: For false positives, use the dismiss feature with proper justification
4. **Share Details**: If you'd like help with specific alerts, share the alert details with me

---

**Note**: I attempted to access your code scanning alerts via the GitHub API but don't have sufficient permissions. You'll need to check the Security tab directly in the GitHub web interface.
