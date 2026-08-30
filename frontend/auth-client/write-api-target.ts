// Bakes API_PROXY_TARGET into a file that `--include`d into the compiled desktop binary,
// since env vars set for the `deno desktop` build command aren't captured in the resulting
// .exe - Deno.env.get() in server.ts would read the *launching* process's environment,
// which is empty when a user double-clicks the exe.
const target = Deno.args[0];
if (!target) {
  console.error("Usage: deno run -A write-api-target.ts <url>");
  Deno.exit(1);
}
await Deno.writeTextFile(`${import.meta.dirname}/api-target.txt`, target);
