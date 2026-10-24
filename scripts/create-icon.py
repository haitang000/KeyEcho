"""Build the app's original vector-style keyboard icon at multiple resolutions."""
from pathlib import Path
from PIL import Image, ImageDraw

output = Path(__file__).resolve().parents[1] / 'src/KeyEcho.App/Assets/KeyEcho.ico'
output.parent.mkdir(parents=True, exist_ok=True)
image = Image.new('RGBA', (256, 256), (0, 0, 0, 0))
draw = ImageDraw.Draw(image)
draw.rounded_rectangle((4, 4, 252, 252), radius=54, fill='#1769D2')
draw.rounded_rectangle((38, 73, 218, 188), radius=18, fill='white')
for row in range(2):
    for col in range(5):
        x, y = 54 + 31 * col, 91 + 30 * row
        draw.rounded_rectangle((x, y, x + 22, y + 19), radius=4, fill='#1769D2')
draw.rounded_rectangle((69, 151, 187, 169), radius=5, fill='#1769D2')
draw.arc((85, 27, 171, 90), 210, 330, fill='white', width=7)
draw.arc((101, 43, 155, 83), 210, 330, fill='white', width=6)
image.save(output, format='ICO', sizes=[(16,16),(24,24),(32,32),(48,48),(64,64),(128,128),(256,256)])
