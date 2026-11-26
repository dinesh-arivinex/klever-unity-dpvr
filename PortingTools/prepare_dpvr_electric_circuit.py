"""Build the DPVR rendition of the hosted Electric Circuit animation."""
import argparse
import concurrent.futures
import io
import re
import urllib.parse
import urllib.request
from pathlib import Path

from PIL import Image

SOURCE = "https://d3vdjegbtyvp.cloudfront.net/Electric_Circuit/"
RENDITION = "/Electric_Circuit-dpvr/v2/"
ATLAS = re.compile(r'src:"(images/Electric_Circuit_atlas_\d+\.png)(?:\?[^"]*)?"')
SPRITE_SHEET = 'ss[ssMetadata[i].name] = new createjs.SpriteSheet( {"images": [queue.getResult(ssMetadata[i].name)], "frames": ssMetadata[i].frames} )'
RENDER_PATCH = """
// Preserve Animate's original logical frame rectangles on half-size atlases.
var kleverDrawImage = CanvasRenderingContext2D.prototype.drawImage;
CanvasRenderingContext2D.prototype.drawImage = function(image, x, y, w, h, dx, dy, dw, dh) {
    if (image && image.kleverAtlasScale && arguments.length === 9) {
        return kleverDrawImage.call(this, image, x * 0.5, y * 0.5, w * 0.5, h * 0.5, dx, dy, dw, dh);
    }
    return kleverDrawImage.apply(this, arguments);
};
"""


def fetch(path):
    with urllib.request.urlopen(urllib.parse.urljoin(SOURCE, path), timeout=30) as response:
        return response.read()


def resize_atlas(path, output):
    with Image.open(io.BytesIO(fetch(path))) as image:
        if image.width % 2 or image.height % 2:
            raise ValueError(f"Unexpected odd atlas dimensions in {path}")
        atlas = image.convert("RGBA").resize((image.width // 2, image.height // 2), Image.Resampling.LANCZOS)
        atlas.save((output / path).with_suffix(".webp"), format="WEBP", lossless=True, method=2)
        return atlas.width * atlas.height * 4


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("output", type=Path)
    output = parser.parse_args().output
    html = fetch("index.html").decode("utf-8-sig")
    paths = ATLAS.findall(html)
    if len(paths) != 373 or len(set(paths)) != len(paths) or html.count(SPRITE_SHEET) != 1:
        raise ValueError("Electric Circuit export changed. Review its atlas manifest and sprite initialization.")
    (output / "images").mkdir(parents=True, exist_ok=True)
    with concurrent.futures.ThreadPoolExecutor(max_workers=8) as executor:
        decoded_bytes = sum(executor.map(lambda path: resize_atlas(path, output), paths))
    html = ATLAS.sub(lambda match: 'src:"' + RENDITION + match[1][:-4] + '.webp", type:"image"', html)
    html = html.replace("<head>", '<head>\n<base href="/Electric_Circuit/">', 1)
    if '<base href="/Electric_Circuit/">' not in html:
        raise ValueError("Electric Circuit export lacks the expected head element")
    html = html.replace(SPRITE_SHEET,
                        'var image = queue.getResult(ssMetadata[i].name);\n'
                        'image.kleverAtlasScale = true;\n'
                        'ss[ssMetadata[i].name] = new createjs.SpriteSheet({images: [image], frames: ssMetadata[i].frames});')
    html = html.replace("function handleComplete(evt,comp) {",
                        RENDER_PATCH + "\nfunction handleComplete(evt,comp) {", 1)
    if RENDER_PATCH not in html:
        raise ValueError("Electric Circuit completion callback changed")
    start_loading = "loader.loadManifest(lib.properties.manifest);"
    if html.count(start_loading) != 1:
        raise ValueError("Electric Circuit loader changed")
    html = html.replace(start_loading, """
        loader.setMaxConnections(8);
        loader.addEventListener("progress", function(evt) {
            document.title = "Electric Circuit — " + Math.floor(evt.progress * 100) + "%";
        });
        loader.addEventListener("error", function() {
            window.kleverAssetLoadErrors = (window.kleverAssetLoadErrors || 0) + 1;
        });
        loader.loadManifest(lib.properties.manifest);
    """)
    (output / "index.html").write_text(html, encoding="utf-8")
    print(f"Prepared {len(paths)} sprite sheets, {decoded_bytes / (1024 ** 2):.0f} MiB decoded")


if __name__ == "__main__":
    main()
