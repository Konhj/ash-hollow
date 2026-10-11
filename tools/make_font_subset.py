import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from fontTools import subset
from fontTools.ttLib import TTFont

import i18n

SOURCE = "/usr/share/fonts/truetype/wqy/wqy-microhei.ttc"
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "fonts", "wqy-subset.ttf")

chars = set("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789")
chars |= set(" !\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~")
for entry in i18n.STRINGS.values():
    chars |= set(entry["en"])
    chars |= set(entry["zh"])
text = "".join(sorted(chars))

options = subset.Options()
options.name_IDs = ["*"]
options.notdef_outline = True
font = TTFont(SOURCE, fontNumber=0)
subsetter = subset.Subsetter(options)
subsetter.populate(text=text)
subsetter.subset(font)

os.makedirs(os.path.dirname(OUT), exist_ok=True)
subset.save_font(font, OUT, options)
print("written:", OUT, os.path.getsize(OUT), "bytes,", len(text), "chars")
