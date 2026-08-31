import { encodeBase64 } from "jsr:@std/encoding/base64";

// Native clipboard bridge for ClipboardService's auto-clear (core/services/clipboard.service.ts).
// The web Clipboard API's readText()/writeText() require live transient user activation in
// Chromium/WebView2 - fine for the initial copy (fired from a click), but the auto-clear runs
// from a setTimeout ~30s later with no such activation, so the browser silently refuses it and
// the password is left sitting in the clipboard. Shelling out to the OS's own clipboard tool from
// here sidesteps that entirely - this runs in the privileged Deno host process, not the sandboxed
// webview content, so it isn't subject to that permission gate. Windows-only for now (matches the
// only platform actually shipping); macOS/Linux builds fall back to the web Clipboard API in
// ClipboardService until this gets pbcopy/pbpaste or xclip/wl-copy equivalents.
// Deno.Command has no windowsHide option (unlike Node's child_process.spawn), and powershell's own
// -WindowStyle Hidden flag still flashes a console window on screen momentarily - verified directly,
// a well-known limitation (the OS allocates the console window at process creation, before
// PowerShell's own startup code that processes -WindowStyle ever runs). WshShell.Run's windowStyle
// argument doesn't have that problem, since it's respected at CreateProcess time - the standard
// technique for launching something genuinely invisible on Windows. wscript.exe itself never shows
// a window running a .vbs script, so nothing flashes at any point in the chain.
async function runHidden(commandLine: string, env?: Record<string, string>): Promise<void> {
  const vbsPath = await Deno.makeTempFile({ suffix: ".vbs" });
  try {
    const vbs = 'Set WshShell = CreateObject("WScript.Shell")\r\n' +
      // Doubling embedded double quotes is VBScript string-literal escaping, not shell escaping -
      // this is a fixed, app-authored command line (see callers below), not raw user input.
      `exitCode = WshShell.Run("${commandLine.replace(/"/g, '""')}", 0, True)\r\n` +
      "WScript.Quit(exitCode)\r\n";
    await Deno.writeTextFile(vbsPath, vbs);

    const { code, stderr } = await new Deno.Command("wscript", {
      args: ["//B", "//Nologo", vbsPath],
      env,
      stdout: "null",
      stderr: "piped",
    }).output();

    if (code !== 0) {
      throw new Error(`hidden command exited ${code}: ${new TextDecoder().decode(stderr).trim()}`);
    }
  } finally {
    await Deno.remove(vbsPath).catch(() => {});
  }
}

async function runPowerShellHidden(script: string, env?: Record<string, string>): Promise<void> {
  const quoted = script.replace(/"/g, '\\"');
  await runHidden(`powershell.exe -NoProfile -NonInteractive -Command "${quoted}"`, env);
}

export async function readClipboardText(): Promise<string> {
  // WshShell.Run doesn't expose the launched process's stdout to the caller at all (fire-and-wait
  // only) - the script writes its own output to a temp file instead, which we then read back.
  const outFile = await Deno.makeTempFile({ suffix: ".txt" });
  try {
    // [Console]::OutputEncoding isn't in play here since output goes to a file, not the console,
    // but -Encoding UTF8 on Windows PowerShell 5.1 always prepends a BOM (a well-known quirk fixed
    // in PowerShell 7's utf8NoBOM) - stripped below rather than relying on that not happening.
    await runPowerShellHidden(
      `(Get-Clipboard -Raw) | Out-File -FilePath '${outFile}' -Encoding UTF8 -NoNewline`,
    );
    const content = await Deno.readTextFile(outFile);
    return content.replace(/^﻿/, "");
  } finally {
    await Deno.remove(outFile).catch(() => {});
  }
}

export async function writeClipboardText(text: string): Promise<void> {
  // The value travels through an env var, base64-encoded, rather than interpolated into the
  // -Command script text (vault values are arbitrary user input - building a script string out
  // of one directly would be a PowerShell injection hole) or piped over stdin (Windows
  // PowerShell's handling of a piped native stdin stream via $input/[Console]::In turned out to
  // be unreliable when launched non-interactively like this - that's what silently no-op'd every
  // copy before this, leaving whatever was already on the clipboard in place). Verified directly
  // against real PowerShell, including special characters, quotes, backticks, emoji and CJK text.
  await runPowerShellHidden(
    "Set-Clipboard -Value ([System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($env:EV_CLIPBOARD_VALUE_B64)))",
    { EV_CLIPBOARD_VALUE_B64: encodeBase64(text) },
  );
}

export async function clearClipboardText(): Promise<void> {
  // Set-Clipboard -Value '' throws ArgumentNullException on this PowerShell (empty string binds
  // to null for this cmdlet's parameter) - verified directly. Clipboard.Clear() actually empties
  // it instead of setting empty text, and doesn't have that problem.
  await runPowerShellHidden("Add-Type -AssemblyName System.Windows.Forms; [System.Windows.Forms.Clipboard]::Clear()");
}
