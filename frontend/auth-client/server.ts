import { serveDir } from "@std/http/file-server";

import { clearClipboardText, readClipboardText, writeClipboardText } from "./desktop/powershell.ts";

const DIST = `${import.meta.dirname}/dist/auth-client/browser`;

// Mirrors proxy.conf.mjs's API_PROXY_TARGET convention so /api behaves the same way
// across ng serve, Docker/nginx, and this desktop build - just proxied here instead of
// by a dev server or nginx.
//
// The env var only covers `deno task dev`/`dev:prod` (live processes where it's actually set
// at launch). A compiled --output build is later launched with no such env var present, so
// build tasks must bake the target into api-target.txt (see write-api-target.ts) and
// --include it in the binary; that file is checked as a fallback here. No default in either
// case: an unset target must fail loudly rather than silently ship a desktop build wired to
// someone's localhost backend.
async function resolveApiProxyTarget(): Promise<string> {
  const fromEnv = Deno.env.get("API_PROXY_TARGET");
  if (fromEnv) return fromEnv;
  try {
    return (await Deno.readTextFile(`${import.meta.dirname}/api-target.txt`)).trim();
  } catch {
    throw new Error(
      "API_PROXY_TARGET must be set, or api-target.txt must be baked into the build " +
        "(e.g. http://localhost:8080 for local testing)",
    );
  }
}

const API_PROXY_TARGET = await resolveApiProxyTarget();

// Set only for `deno task dev`/`dev:prod` - when present, every non-/api request (including
// the WebSocket upgrade Angular's dev server uses for HMR/live-reload) proxies to `ng serve`
// instead of serving static dist/ files, so dev mode gets Angular's real HMR rather than a
// full page reload after each `ng build --watch` rebuild.
const ANGULAR_DEV_SERVER = Deno.env.get("ANGULAR_DEV_SERVER");

// Flags the compiled app to Angular (see core/utils/desktop.util.ts) so it can skip the web
// marketing landing page and go straight to login/the app instead. Only this desktop server
// ever serves index.html this way - the deployed website (Static Web App/nginx) serves the
// prebuilt dist/ directly and never runs server.ts, so the flag can't leak there. Not injected
// for the ANGULAR_DEV_SERVER proxy path below (`deno task dev`) since that serves ng serve's
// raw HTML untouched - only the compiled --output build gets the flag.
async function serveIndexHtml(): Promise<Response> {
  const html = await Deno.readTextFile(`${DIST}/index.html`);
  const injected = html.replace("<head>", "<head><script>window.__EV_DESKTOP__=true;</script>");
  return new Response(injected, { headers: { "content-type": "text/html; charset=utf-8" } });
}

function proxyWebSocket(req: Request, target: string): Response {
  const url = new URL(req.url);
  const upstreamUrl = new URL(url.pathname + url.search, target);
  upstreamUrl.protocol = upstreamUrl.protocol === "https:" ? "wss:" : "ws:";

  const { socket: clientSocket, response } = Deno.upgradeWebSocket(req);
  const upstreamSocket = new WebSocket(upstreamUrl);
  const pending: (string | ArrayBufferLike | Blob | ArrayBufferView)[] = [];

  clientSocket.onmessage = (e) => {
    if (upstreamSocket.readyState === WebSocket.OPEN) upstreamSocket.send(e.data);
    else pending.push(e.data);
  };
  upstreamSocket.onopen = () => {
    for (const data of pending.splice(0)) upstreamSocket.send(data);
  };
  upstreamSocket.onmessage = (e) => {
    if (clientSocket.readyState === WebSocket.OPEN) clientSocket.send(e.data);
  };
  clientSocket.onclose = () => upstreamSocket.close();
  upstreamSocket.onclose = () => clientSocket.close();
  clientSocket.onerror = () => upstreamSocket.close();
  upstreamSocket.onerror = () => clientSocket.close();

  return response;
}

async function proxyHttp(req: Request, target: string): Promise<Response> {
  const url = new URL(req.url);
  const targetUrl = new URL(url.pathname + url.search, target);
  const reqHeaders = new Headers(req.headers);
  reqHeaders.set("host", targetUrl.host);
  reqHeaders.delete("content-length");
  reqHeaders.delete("connection");
  const bodyBuffer = req.body ? await req.arrayBuffer() : undefined;
  const upstream = await fetch(targetUrl, {
    method: req.method,
    headers: reqHeaders,
    body: bodyBuffer,
    redirect: "manual",
  });
  const resHeaders = new Headers(upstream.headers);
  resHeaders.delete("content-encoding");
  resHeaders.delete("content-length");
  return new Response(upstream.body, {
    status: upstream.status,
    statusText: upstream.statusText,
    headers: resHeaders,
  });
}

Deno.serve(async (req) => {
  try {
    const url = new URL(req.url);
    if (url.pathname.startsWith("/api")) {
      const target = new URL(url.pathname + url.search, API_PROXY_TARGET);
      const reqHeaders = new Headers(req.headers);
      reqHeaders.set("host", target.host);
      reqHeaders.delete("content-length");
      reqHeaders.delete("connection");
      reqHeaders.delete("origin");
      reqHeaders.delete("referer");
      // Isolated via direct Invoke-WebRequest tests: forwarding the browser's Accept-Encoding
      // header (any value, not just zstd) to Azure Container Apps' Envoy ingress gets an
      // empty-body 400 before the request ever reaches the .NET app - baseline requests with
      // no Accept-Encoding at all reach the app fine (401 for an invalid refresh token). This
      // backend is designed to be reached only through the Static Web App edge (see
      // Program.cs's ForwardedHeaders comment), which presumably normalizes this before
      // forwarding; calling the Container App's FQDN directly bypasses that. We don't need
      // Azure to compress the response for this local server-to-server hop anyway.
      reqHeaders.delete("accept-encoding");
      // These are small JSON auth payloads, not file uploads - buffer the body fully instead
      // of streaming it. A curl request with a normal (non-streamed) body reached the app fine
      // (401), while our streamed body (chunked transfer-encoding via fetch's duplex mode) got
      // an empty-body 400 from Azure's ingress before ever reaching the app, so something about
      // that chunked framing is the actual problem - buffering sidesteps it and lets fetch set
      // a normal content-length itself.
      const bodyBuffer = req.body ? await req.arrayBuffer() : undefined;
      const upstream = await fetch(target, {
        method: req.method,
        headers: reqHeaders,
        body: bodyBuffer,
        redirect: "manual",
      });

      // fetch() transparently decompresses the upstream body, but leaves
      // content-encoding/content-length describing the *original* compressed bytes. Passing
      // those through verbatim makes the browser try to re-decompress an already-decoded
      // body and fail silently (empty response body, status/headers otherwise unaffected).
      const resHeaders = new Headers(upstream.headers);
      resHeaders.delete("content-encoding");
      resHeaders.delete("content-length");

      return new Response(upstream.body, {
        status: upstream.status,
        statusText: upstream.statusText,
        headers: resHeaders,
      });
    }

    if (ANGULAR_DEV_SERVER) {
      if (req.headers.get("upgrade")?.toLowerCase() === "websocket") {
        return proxyWebSocket(req, ANGULAR_DEV_SERVER);
      }
      return await proxyHttp(req, ANGULAR_DEV_SERVER);
    }

    if (url.pathname === "/" || url.pathname === "/index.html") {
      return await serveIndexHtml();
    }

    const res = await serveDir(req, { fsRoot: DIST, quiet: true });
    if (res.status === 404) {
      return await serveIndexHtml();
    }
    return res;
  } catch (err) {
    console.error("Request failed:", err);
    return new Response(
      `Server error: ${err instanceof Error ? err.message : String(err)}`,
      { status: 500 },
    );
  }
});

// Deno.serve() above is a long-running async operation that never resolves on its own, so the
// runtime's "exit when no windows are open and no other live async tasks remain" heuristic
// never triggers on its own - clicking the window's close button closes the window but leaves
// the process (and WebView2) running in the background indefinitely. The first
// `new Deno.BrowserWindow()` call adopts deno desktop's automatic window rather than creating
// a second one, giving us a handle to explicitly exit when the user actually closes it.
// title overrides the default, which otherwise falls back to the project's package name
// ("emergency-vault") rather than a proper display name.
const win = new Deno.BrowserWindow({ title: "Emergency Vault" });
win.addEventListener("close", () => Deno.exit(0));

win.bind("readClipboardText", readClipboardText);
win.bind("writeClipboardText", writeClipboardText);
win.bind("clearClipboardText", clearClipboardText);
