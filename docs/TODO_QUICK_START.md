# Quick Start: Creating GitHub Issues from TODOs

**🎯 Goal:** Convert the 13 TODOs in the codebase into 11 trackable GitHub issues.

## ⚡ Fast Track (5 minutes per issue)

### Step 1: Open the Template

Open [ISSUES_TO_CREATE.md](ISSUES_TO_CREATE.md) in your browser or editor.

### Step 2: Create Issues in Order

For each issue template:

1. **Copy** the entire template block (title + description + criteria)
2. **Go to** [GitHub Issues](https://github.com/bassetthomas-design/Virgil/issues/new)
3. **Paste** the template
4. **Add labels** as specified in the template
5. **Click** "Submit new issue"
6. **Check off** in [TODO_ISSUES_CHECKLIST.md](TODO_ISSUES_CHECKLIST.md)

### Step 3: Link Related Issues

After creating each issue, add a comment linking to related phase issues (e.g., "Related to #74").

## 📝 Recommended Order

Create issues in this order for best dependency management:

### First (Foundation):
1. Issue 1: SystemMonitorService - Complete metrics
2. Issue 2: AdvancedMonitoringService - Disk temperature
3. Issue 5: MonitoringService - RescanAsync

### Second (Configuration):
4. Issue 4: Configuration - Dynamic reload
5. Issue 6: MainShell - Settings button
6. Issue 9: SettingsWindow - ViewModel

### Third (Features):
7. Issue 3: Chat - Persistence and purge
8. Issue 8: ChatViewModel - Thanos effect

### Fourth (UI Polish):
9. Issue 7: MainWindow - Metrics display
10. Issue 10: MainShell - HUD toggle
11. Issue 11: MainViewModel - Progress indicator

## 🏷️ Labels to Use

Make sure these labels exist in your repository:
- `enhancement`
- `phase-1`
- `phase-2`
- `monitoring`
- `configuration`
- `chat`
- `ui`
- `settings`
- `thanos-effect`
- `hardware`

## ✅ Checklist After Completion

- [ ] All 11 issues created
- [ ] Issue numbers recorded in [TODO_ISSUES_CHECKLIST.md](TODO_ISSUES_CHECKLIST.md)
- [ ] Milestone "Phase 1 - Core Features" created and assigned
- [ ] Issues prioritized in project board (if used)
- [ ] Team notified of new issues

## 🔗 Quick Reference

- **Full details**: [TODO_TRACKING.md](TODO_TRACKING.md)
- **All templates**: [ISSUES_TO_CREATE.md](ISSUES_TO_CREATE.md)
- **Progress tracking**: [TODO_ISSUES_CHECKLIST.md](TODO_ISSUES_CHECKLIST.md)
- **Documentation index**: [README.md](README.md)

---

**💡 Tip:** You can create all issues in ~1 hour by following this guide systematically. Each issue is fully documented with clear acceptance criteria ready to be worked on.
