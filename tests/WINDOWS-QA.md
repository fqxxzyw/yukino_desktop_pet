# Standalone pet validation

## Automated checks

Run from the repository root on Windows with .NET Framework 4.x:

```powershell
powershell -ExecutionPolicy Bypass -File .\tests\run-tests.ps1
powershell -ExecutionPolicy Bypass -File .\src\YukinoPet\build.ps1
```

The first command compiles the actual animation, placement, and bubble classes. It checks:

- regular versus delayed render callbacks, fractional timing, pause/resume, interruption priority, completion callbacks, and bounded resume after a long suspension;
- fixed bubble dimensions for a short quote, the longest bundled Chinese quote, and a 240-character custom quote (wrap plus ellipsis, not automatic window growth);
- top/bottom/side placement, negative desktop origins, very small work areas, six character scales, and repeated clamp/drag arithmetic;
- every configured clip staying within its atlas and never referencing a fully transparent frame. The legacy generic `run` clip has six nonblank cells, not eight; normal walking still uses its separate eight-frame strips.

The second command builds the complete offline EXE using the existing Windows source-build script. The CI artifact is a **validation build, not a released or signed executable**. A successful CI compile/layout test is not evidence that human interaction or mixed-DPI behavior has passed.

## Manual Windows checklist — record actual results, do not infer a pass

Use a fresh temporary settings backup, then repeat with existing settings. Preserve the user's normal settings/voice assets. Record Windows version, monitor arrangement, taskbar edge, monitor DPI, and character scale.

- [ ] At 100%, 125%, 150%, and 200% display scaling, drag from the body to all four work-area corners. Repeat at least ten complete drags and verify the reachable area does not shrink. Repeat at 80%, 100%, 125%, and 150% character size.
- [ ] Drag across two monitors in both directions, including a monitor left/above the primary (negative coordinates) and mixed 100%/150% DPI. The grabbed point should remain under the pointer; release must clamp only to the destination monitor's current work area, not a previous walk target or pet-sized reduced rectangle.
- [ ] Start walking, grab immediately, release, then drag again. No old autonomous velocity/target should resume. Verify all taskbar edges, screen-resolution changes, and reconnecting a monitor.
- [ ] Interrupt a held drag with another app taking capture, hiding to tray, or a right-click. Release outside the pet. The next click/drag must work and no lifted state or capture may remain stuck.
- [ ] Move repeatedly while holding a drag: the lifted animation must not be re-entered on every move. Recognize a head pat and continue moving: the waiting clip must advance rather than restart continuously. Release, then click again.
- [ ] Trigger short/long Chinese quotes at every edge/corner and while walking. At 100% pet size the frame stays 320×128 DIP; at other pet sizes it scales consistently. It should flip below or to a side when above does not fit, avoid the character when space permits, and remain inside the work area.
- [ ] Test a 240-character custom quote and a narrow/short work area. Text wraps and is ellipsized/clipped inside the frame rather than growing the window. If the pet and bubble cannot both fit, confirm overlap is minimized and no claim of impossible zero overlap is made.
- [ ] Open the context menu at every edge/corner, including both monitors. The main menu should open beside the pet when space permits; the pet must not walk behind it. Open voice/size/quote submenus and confirm screen-edge visibility. Native submenu placement is retained; verify it on the target Windows configuration.
- [ ] Close menus with Escape, outside click, selection, and repeated right-click. Confirm keyboard arrows/Enter work and normal idle behavior resumes after dismissal, with no position drift. Resize/opacity/topmost/quiet/focus settings must retain their previous behavior.
- [ ] Pause/resume animation, hide/show from tray, and suspend/resume Windows. Confirm no crash, rapid unbounded replay, or duplicate completion interaction. Check click voice playback and that successive voices still replace rather than overlap.
- [ ] Compare walking/idle/wave visually with the same source assets. There are only 8/7/4 authored frames respectively; do not report a higher pose rate or "60 FPS sprites" from a renderer callback count. Record CPU/render observations separately if performance is measured.

## Implementation references

- [Microsoft: per-frame WPF rendering](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/how-to-render-on-a-per-frame-interval-using-compositiontarget)
- [Microsoft: screen-to-visual coordinates](https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.visual.pointfromscreen)
- [Microsoft: custom popup placement](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/how-to-specify-a-custom-popup-position)
- [Microsoft: lost mouse capture](https://learn.microsoft.com/en-us/dotnet/api/system.windows.uielement.lostmousecapture)
