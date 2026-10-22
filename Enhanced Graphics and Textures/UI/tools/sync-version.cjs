// Version.props is the single source for DLL, options, publication and UI metadata.
// No npm dependencies: this also runs before npm ci on the first C# build.
const fs = require("node:fs");
const path = require("node:path");
const root = path.resolve(__dirname, "../..");

function syncVersion(projectRoot) {
    const props = fs.readFileSync(path.join(projectRoot, "Version.props"), "utf8");
    const versions = [...props.matchAll(/<Version>\s*([^<]+?)\s*<\/Version>/g)];
    if (versions.length !== 1) throw new Error("Version.props must contain exactly one <Version> value.");
    const version = versions[0][1].trim();
    if (!/^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$/.test(version) ||
        version.split(".").some(part => Number(part) > 65534)) {
        throw new Error("Version.props: use Major.Minor.Patch, each from 0 to 65534 (for example 1.4.0).");
    }

    const publishPath = path.join(projectRoot, "Properties/PublishConfiguration.xml");
    const uiPath = path.join(projectRoot, "UI/mod.json");
    const publish = fs.readFileSync(publishPath, "utf8");
    const ui = fs.readFileSync(uiPath, "utf8");
    const modVersion = /(<ModVersion\b[^>]*\bValue\s*=\s*")[^"]*(")/g;
    const uiVersion = /("version"\s*:\s*")[^"]*(")/g;
    if ([...publish.matchAll(modVersion)].length !== 1) {
        throw new Error("PublishConfiguration.xml must contain exactly one ModVersion Value attribute.");
    }
    JSON.parse(ui.replace(/^\uFEFF/, ""));
    if ([...ui.matchAll(uiVersion)].length !== 1) {
        throw new Error("UI/mod.json must contain exactly one version field.");
    }
    // Validate both inputs before writing; retain descriptions, changelog and formatting.
    const nextPublish = publish.replace(modVersion, (_, start, end) => start + version + end);
    const nextUi = ui.replace(uiVersion, (_, start, end) => start + version + end);
    if (nextPublish !== publish) fs.writeFileSync(publishPath, nextPublish, "utf8");
    if (nextUi !== ui) fs.writeFileSync(uiPath, nextUi, "utf8");
    return version;
}

if (require.main === module) {
    try {
        console.log("Enhanced Graphics and Textures version: " + syncVersion(root));
    } catch (error) {
        console.error("Version synchronization failed: " + error.message);
        process.exitCode = 1;
    }
}
module.exports = { syncVersion };
