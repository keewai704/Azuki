import { readFileSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const repoRoot = dirname(dirname(fileURLToPath(import.meta.url)));
const versionPath = join(repoRoot, "app-version.json");

const versionConfig = JSON.parse(readFileSync(versionPath, "utf8"));
const version = versionConfig.version;

if (typeof version !== "string" || !/^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$/.test(version)) {
  throw new Error(`Invalid app version in app-version.json: ${String(version)}`);
}

writeFileSync(
  join(repoRoot, "installer/AppVersion.iss"),
  `; Generated from app-version.json by scripts/sync-version.mjs.\n#define MyAppVersion "${version}"\n`,
);
