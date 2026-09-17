"""Concrete, original C12 surface/trim studies; not approved final artistic recipes."""
STYLES = ("Classic", "Modern", "Vintage", "Minimal", "Artisan")
QUALITIES = ("economy", "standard")
# Stable surface roles across styles; constant PBR only, no image/font/dependency.
PALETTES = (
    {"plaster": (.68, .62, .51), "floor": (.48, .43, .35), "wood": (.26, .12, .055),
     "metal": (.20, .12, .06), "metallic": .65, "woodRoughness": .5,
     "cloth": ((.14, .21, .22), (.48, .26, .12), (.64, .58, .46), (.21, .24, .30)), "corniceHeight": .12},
    {"plaster": (.66, .69, .68), "floor": (.30, .33, .34), "wood": (.09, .11, .12),
     "metal": (.055, .065, .075), "metallic": .75, "woodRoughness": .35,
     "cloth": ((.055, .16, .20), (.47, .22, .12), (.72, .70, .62), (.12, .17, .27)), "corniceHeight": .07},
    {"plaster": (.65, .57, .42), "floor": (.36, .28, .19), "wood": (.15, .23, .18),
     "metal": (.22, .12, .045), "metallic": .5, "woodRoughness": .68,
     "cloth": ((.15, .23, .18), (.38, .14, .10), (.60, .47, .28), (.22, .18, .25)), "corniceHeight": .16},
    {"plaster": (.75, .74, .70), "floor": (.56, .56, .52), "wood": (.60, .57, .48),
     "metal": (.36, .38, .38), "metallic": .45, "woodRoughness": .6,
     "cloth": ((.30, .32, .31), (.47, .43, .35), (.71, .69, .63), (.21, .24, .25)), "corniceHeight": .04},
    {"plaster": (.62, .44, .29), "floor": (.40, .29, .20), "wood": (.29, .14, .065),
     "metal": (.075, .065, .05), "metallic": .25, "woodRoughness": .8,
     "cloth": ((.18, .24, .17), (.43, .18, .095), (.61, .52, .36), (.18, .21, .25)), "corniceHeight": .14},
)
QUALITY_GEOMETRY = {"economy": {"rodSides": 6, "bevelSegments": 1},
                    "standard": {"rodSides": 12, "bevelSegments": 2}}


def validate_variant(style, quality):
    if type(style) is not int or not 0 <= style < len(STYLES):
        raise ValueError("variant: style must be an integer from 0 to 4")
    if type(quality) is not str or quality not in QUALITIES:
        raise ValueError("variant: quality must be economy or standard")


def variant_slug(style, quality):
    validate_variant(style, quality)
    return "c12-" + STYLES[style].lower() + "-" + quality
