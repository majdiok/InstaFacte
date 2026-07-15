import { isPlatformBrowser } from '@angular/common';

/** Browser-only clipboard write; returns false if unavailable or denied. */
export async function writeTextToClipboard(text: string, platformId: object): Promise<boolean> {
  if (!isPlatformBrowser(platformId) || !text) {
    return false;
  }
  try {
    await navigator.clipboard.writeText(text);
    return true;
  } catch {
    try {
      const ta = document.createElement('textarea');
      ta.value = text;
      ta.setAttribute('readonly', '');
      ta.style.position = 'fixed';
      ta.style.left = '-9999px';
      document.body.appendChild(ta);
      ta.select();
      const ok = document.execCommand('copy');
      document.body.removeChild(ta);
      return ok;
    } catch {
      return false;
    }
  }
}
