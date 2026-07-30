/** Palette pastel stable dérivée du code activité (grille + légende). */
export function activityPastelColor(code?: string): string {
  if (!code) return 'hsl(215 16% 88%)';
  let hash = 0;
  for (let i = 0; i < code.length; i++) hash = (hash * 31 + code.charCodeAt(i)) | 0;
  const hue = Math.abs(hash) % 360;
  return `hsl(${hue} 55% 88%)`;
}

export function activityPastelBorder(code?: string): string {
  if (!code) return 'hsl(215 16% 65%)';
  let hash = 0;
  for (let i = 0; i < code.length; i++) hash = (hash * 31 + code.charCodeAt(i)) | 0;
  const hue = Math.abs(hash) % 360;
  return `hsl(${hue} 45% 55%)`;
}
