# Updating all via winget (if available)
try { winget upgrade --all --silent } catch { Write-Warning 'winget not available' }
