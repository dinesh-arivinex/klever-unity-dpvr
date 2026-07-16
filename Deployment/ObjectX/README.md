# ObjectX Quest Browser launcher

Deploy `objectx-launcher.html` at:

`https://klever-edu.com/objectx-launcher.html`

The Unity application opens this page for ObjectX simulations. The page embeds
the supplied `https://api.objectx.in/gcl/embed...` URL in an iframe, so ObjectX
receives the whitelisted `https://klever-edu.com` referrer while the content
runs in Quest Browser, which provides WebXR.

Recommended response headers for the launcher page:

```text
Referrer-Policy: origin
Permissions-Policy: xr-spatial-tracking=(self "https://api.objectx.in"), fullscreen=(self "https://api.objectx.in"), autoplay=(self "https://api.objectx.in")
Content-Security-Policy: default-src 'self'; frame-src https://api.objectx.in; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'
```

Do not redirect this page to another hostname such as `www.klever-edu.com`
unless ObjectX has also whitelisted that exact hostname.

After deployment, test a URL in Quest Browser using:

```text
https://klever-edu.com/objectx-launcher.html?src=URL_ENCODED_OBJECTX_EMBED_URL
```

The launcher also includes a "Return to Klever" button that opens
`klever://webtest`, which is already registered by the Android application.
