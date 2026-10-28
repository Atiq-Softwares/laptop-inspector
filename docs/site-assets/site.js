"use strict";

// The working HTML fallback points directly to the reviewed v1.1.1 application.
// If GitHub is reachable, select only a published Windows application ZIP.
// Never substitute zipball_url, which is a source-code archive.
(async () => {
  try {
    const response = await fetch("https://api.github.com/repos/Atiq-Softwares/laptop-inspector/releases/latest", {
      headers: { Accept: "application/vnd.github+json" },
      credentials: "omit",
      signal: AbortSignal.timeout(6000)
    });
    if (!response.ok) return;
    const release = await response.json();
    if (release.draft || release.prerelease || !/^v\d+\.\d+(?:\.\d+)?(?:-[A-Za-z0-9.-]+)?$/.test(release.tag_name)) return;
    const expectedName = `Atiq-Laptop-Inspector-${release.tag_name}-Windows-x64.zip`;
    const asset = Array.isArray(release.assets) ? release.assets.find(item => item.name === expectedName) : null;
    if (!asset || !Number.isSafeInteger(asset.size) || asset.size <= 0) return;
    const url = new URL(asset.browser_download_url);
    const expectedPath = `/Atiq-Softwares/laptop-inspector/releases/download/${release.tag_name}/${expectedName}`;
    if (url.origin !== "https://github.com" || url.pathname !== expectedPath || url.search || url.hash || url.username || url.password) return;
    document.querySelectorAll("[data-download]").forEach(link => { link.href = url.href; });
    document.querySelectorAll("[data-version]").forEach(label => { label.textContent = release.tag_name; });
    document.querySelectorAll("[data-size]").forEach(label => { label.textContent = `${(asset.size / 1048576).toFixed(1)} MB`; });
  } catch {
    // Offline browsing, API limits and older browsers retain the direct fallback.
  }
})();
