# windock glass W icon

Created with the built-in image_gen tool using the user's three images as visual references, not edit targets. The selected production image is saved in `SuperDock/Assets/logo-master.png`. No external API key or fallback CLI was used.

## Assets

- `SuperDock/Assets/logo-master.png`: original transparent generated master.
- `SuperDock/Assets/logo.png`: 512 px transparent application image used by settings, about and window icons.
- `SuperDock/Assets/logo.ico`: 16, 20, 24, 32, 40, 48, 64, 96, 128 and 256 px icon frames, used in the application and setup executable.
- Published `windock-icon-v2.ico`: dedicated shortcut and uninstall icon. A new filename avoids reusing the old bird icon's shell cache entry.

Run `powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File installer/make-logo.ps1` to export PNG and ICO from the master. The installer build also runs this export. The old bird XAML source has been removed.

## Final generation prompt

Use case: logo-brand. Design a finished, exceptionally polished premium Windows desktop application icon for windock, using the three supplied images ONLY as visual references. This is a new icon, not a screenshot or a brand presentation. Main idea: a confident flowing uppercase W made of thick sculpted translucent liquid-glass ribbon, cyan/turquoise on the left crest, luminous blue in the middle and violet on the right, rising above a small glass Dock shelf. The W is the hero, large, instantly recognizable with two clean troughs and three upper crests. Beneath it a slim beautifully rounded translucent frosted-glass Dock tray with ONLY three simple luminous rounded-square app slots, no miniature third-party logos. Integrate both within one refined deep sapphire blue squircle app tile with smooth continuous corners, delicate bright blue glass rim, restrained inner gradient, subtle internal light reflections and dimensional edge highlights. Rich crisp material like a premium macOS native icon, tasteful and balanced, no excessive glow, no thin wisps, no star sparkles. Square composition, centered front view with just slight depth on the tray, symbol occupies about 78% of tile, generous balanced safe margins and strong silhouette legible at 32px. The rounded-square tile itself occupies 90% of the square image, with genuinely transparent pixels outside its curved silhouette. NO text, NO wordmark, NO caption, NO watermark, NO surrounding scene, NO alternative concepts, NO white background, NO drop shadow beyond the silhouette. Deliver one production-ready app icon on a transparent background, high resolution.
