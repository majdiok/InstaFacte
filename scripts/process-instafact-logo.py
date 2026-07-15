from PIL import Image
import os, shutil
SRC = r"C:\Users\sabriko\.cursor\projects\c-Solution-FactuTrust-Copy\assets\c__Users_sabriko_AppData_Roaming_Cursor_User_workspaceStorage_de19749589f08ea57973172be82caf93_images_logofact-3ab47501-4f07-4ee6-8162-c04cb6d29b30.png"
WEB = r"c:\Solution\FactuTrust - Copy\src\Frontend\factutrust-web\src"
BASE = os.path.join(WEB, "assets", "branding")
PLUTO = os.path.join(WEB, "assets", "theme", "pluto", "images", "logo")
CHAIN = os.path.join(WEB, "assets", "theme", "chain", "assets", "images")
API = r"c:\Solution\FactuTrust - Copy\src\Backend\FactuTrust.API\wwwroot\assets\theme\pluto\images\logo"
for d in [BASE, PLUTO, CHAIN, API]:
    os.makedirs(d, exist_ok=True)
img = Image.open(SRC).convert("RGBA")
px = img.load(); w, h = img.size
for y in range(h):
    for x in range(w):
        r,g,b,a = px[x,y]
        if r < 35 and g < 35 and b < 35: px[x,y]=(r,g,b,0)
        elif r < 50 and g < 50 and b < 50 and max(r,g,b)-min(r,g,b) < 15: px[x,y]=(r,g,b,0)
bbox = img.getbbox()
if bbox: img = img.crop(bbox)
lockup = img.copy()
if lockup.width < 800:
    s = max(1, 800 // lockup.width)
    lockup = lockup.resize((lockup.width*s, lockup.height*s), Image.Resampling.LANCZOS)
lockup_path = os.path.join(BASE, "instafact-lockup.png")
lockup.save(lockup_path, "PNG")
iw, ih = img.size
icon = img.crop((0, 0, max(1,int(iw*0.28)), ih))
side = max(icon.width, icon.height)
icon_sq = Image.new("RGBA", (side, side), (0,0,0,0))
icon_sq.paste(icon, ((side-icon.width)//2, (side-icon.height)//2), icon)
icon_512 = icon_sq.resize((512,512), Image.Resampling.LANCZOS)
icon_path = os.path.join(BASE, "instafact-icon.png")
icon_sq.save(icon_path, "PNG")
icon_512.save(os.path.join(BASE, "instafact-icon-512.png"), "PNG")
for s,d in [(lockup_path, os.path.join(PLUTO,"logo.png")), (icon_path, os.path.join(PLUTO,"logo_icon.png")), (lockup_path, os.path.join(CHAIN,"logo.png")), (icon_path, os.path.join(BASE,"assistant-ia-mark.png")), (lockup_path, os.path.join(API,"logo.png"))]:
    shutil.copy2(s,d)
print("Done", lockup.size)
