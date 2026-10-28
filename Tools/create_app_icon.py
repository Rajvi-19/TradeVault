"""Build the original TradeVault vault/monogram icon (development only)."""
from pathlib import Path
from PIL import Image, ImageDraw
root = Path(__file__).resolve().parents[1] / 'Src' / 'TradeVault.Desktop' / 'Assets'
root.mkdir(exist_ok=True)
image = Image.new('RGBA', (1024, 1024))
d = ImageDraw.Draw(image)
d.rounded_rectangle((24, 24, 1000, 1000), radius=208, fill='#10283F')
# Two interlocking ribbon strokes: trade movement and a restrained TV monogram.
d.polygon([(200,270),(680,270),(636,374),(462,374),(462,744),(348,744),(348,374),(200,374)],fill='#FFFFFF')
d.polygon([(528,432),(638,432),(700,614),(790,366),(900,366),(752,744),(644,744)],fill='#32C5B4')
image.resize((256, 256), Image.Resampling.LANCZOS).save(root / 'TradeVault.png')
image.save(root / 'TradeVault.ico', sizes=[(16,16),(24,24),(32,32),(48,48),(64,64),(128,128),(256,256)])
