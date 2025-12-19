# Refactoring Notes and Technical Debt

This document tracks architectural decisions, known duplicates, and areas requiring future cleanup.

## Current Architecture Status (Dec 2025)

### Active UI: MainShell (Preferred)

**Status**: ✅ **ACTIVE AND WIRED**

- Location: `src/Virgil.App/Views/MainShell.xaml`
- Code-behind: `src/Virgil.App/Views/MainShell.xaml.cs`
- ViewModel: `src/Virgil.App/ViewModels/MainViewModel.cs`
- **DataContext is properly initialized** with all required services
- MVVM bindings working: Actions, Monitoring, Chat, StatusText

### Legacy UI: MainWindow

**Status**: ⚠️ **LEGACY - Keep for reference but not actively used**

- Location: `src/Virgil.App/MainWindow.xaml`
- Code-behind: `src/Virgil.App/MainWindow.xaml.cs`
- This was the original window but MainShell is now the official entry point
- App.xaml.cs starts MainShell, not MainWindow
- Reason for keeping: Historical reference and potential fallback

**Decision**: Keep MainWindow in the codebase for now as a reference implementation, but MainShell is the canonical UI.

## Known Duplicates and Technical Debt

### 1. Duplicate Action Services

**Issue**: Two services doing essentially the same thing

- `ActionsService` (src/Virgil.App/Services/ActionsService.cs)
  - Returns `ProcessResult?`
  - Has surveillance toggle functionality
- `SystemActionsService` (src/Virgil.App/Services/SystemActionsService.cs)
  - Returns `Task` (void)
  - Simpler interface

**Impact**: Medium - Both work, but causes confusion

**Recommendation**: Consolidate into a single service. Prefer `ActionsService` since it returns ProcessResult for better error handling.

**Action Required**: Create issue to merge these services in a future refactoring pass.

### 2. Duplicate Monitoring Services in Core

**Issue**: Multiple monitoring implementations in Virgil.Core

- `Virgil.Core/MonitoringService.cs` (PerformanceCounters)
- `Virgil.Core/Monitoring/AdvancedMonitoringService.cs` (snapshot via counters, namespace: Virgil.Core.Monitoring)
- `Virgil.Core/Services/AdvancedMonitoringService.cs` (temp via WMI + nvidia-smi, namespace: Virgil.Core.Services)
- Two classes named `HardwareSnapshot` in different namespaces

**Current Active Implementation**: 
- `Virgil.App/Services/MonitoringService.cs` using LibreHardwareMonitor (this is the one actually used by the UI)

**Impact**: Low - The Core monitoring services are not currently used, but add confusion

**Recommendation**: Since Virgil.App uses LibreHardwareMonitor directly, consider:
1. Keep Virgil.Core monitoring as a fallback/portable option
2. Document which one is "official" 
3. Remove or consolidate the duplicate AdvancedMonitoringService implementations

**Action Required**: Create issue to clean up Core monitoring duplicates.

### 3. Chat Service Split Personality (FIXED ✅)

**Issue**: Chat service had two incompatible mechanisms
- List-based storage (_messages)
- Event-based notification (MessagePosted)
- Methods didn't update both consistently

**Status**: ✅ **FIXED** in current PR
- PostSystemMessage now triggers MessagePosted event
- All Post() methods add to _messages AND trigger event
- Single source of truth maintained

## Scripts Deployment (FIXED ✅)

**Issue**: PowerShell scripts were not being copied to output directory

**Status**: ✅ **FIXED** in current PR
- Added Content Include in Virgil.App.csproj
- Scripts now copied to output/scripts folder
- Script name mismatch fixed (smart_cleanup.ps1 -> cleanup_smart.ps1)

## Future Architecture Considerations

### Orchestrator Pattern (Not Yet Integrated)

The codebase contains a "clean architecture" implementation with:
- `Virgil.Services/ActionOrchestrator.cs`
- `Virgil.Services.Abstractions.*` interfaces
- `Virgil.Domain` actions

**Status**: ⏸️ Present but not wired to UI

**Recommendation**: This is a good design for future expansion, but should be integrated gradually:
1. Current pragmatic approach (PowerShell scripts via services) works
2. Orchestrator can be phased in action-by-action
3. Don't force a "big bang" refactor

### Dependency Injection

**Current**: Manual instantiation in MainShell.xaml.cs

**Future**: Consider a lightweight DI container (e.g., Microsoft.Extensions.DependencyInjection) when the number of services grows.

## Maintenance Guidelines

1. **Adding New Actions**: 
   - Add PowerShell script to `src/Virgil.App/scripts/`
   - Add method to `ActionsService` (preferred) or `SystemActionsService`
   - Scripts auto-copy to output via csproj Content Include

2. **Monitoring Changes**:
   - Modify `Virgil.App/Services/MonitoringService.cs` (LibreHardwareMonitor-based)
   - Update `MonitoringViewModel` if new metrics added
   - Core monitoring services are not in use

3. **Chat/UI Integration**:
   - Use MainShell as the primary window
   - MainViewModel is the root ViewModel
   - ChatService maintains _messages list and fires MessagePosted events

## Next Steps

- [ ] Create issue: Consolidate ActionsService and SystemActionsService
- [ ] Create issue: Document or clean up Core monitoring duplicates
- [ ] Create issue: Evaluate Orchestrator integration path
- [ ] Create issue: Add DI container when service count justifies it
