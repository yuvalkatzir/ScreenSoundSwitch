# UI Improvement Checklist

## 1. Interaction Consistency (in progress)
- [x] Add a unified status bar (InfoBar) on the display/device selection page
- [x] Disable the playback device dropdown when no display is selected
- [x] Show visible status messages on binding success/failure
- [x] Unify status bar style across all pages (volume, settings, playback)

## 2. Layout and Visual Hierarchy
- [ ] Unify page spacing and card styles (Spacing/Padding/Border)
- [ ] Remove hardcoded colors, use ThemeResource throughout
- [ ] Verify readability in dark theme

## 3. Usability Improvements
- [ ] Add session list search/filter
- [ ] Show primary-screen badge and current bound device summary on monitor cards
- [ ] Add guidance messages for empty states (no sessions, no devices)

## 4. Stability and Performance
- [ ] Throttle high-frequency events (window move, session refresh)
- [ ] Verify complete event unsubscription on control unload
- [ ] Evaluate list virtualization for large numbers of sessions

## 5. Settings Experience
- [ ] Add first-launch onboarding (bind display to device first)
- [ ] Consolidate auto-start, tray, and config-restore toggles into the settings page
- [ ] Add “reset all bindings” and “import/export config”
