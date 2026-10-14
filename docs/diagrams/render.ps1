# Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
# Regenerates docs/diagrams print sheets. Requires Node and Microsoft Edge.
# npm installs opentype.js and puppeteer-core into a temp tools folder, not the repo.

$ErrorActionPreference = 'Stop'
$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$tools = if ($env:MPS_DIAGRAM_TOOLS) { $env:MPS_DIAGRAM_TOOLS } else { Join-Path $env:TEMP 'mps-diagram-tools' }
$opentype = Join-Path $tools 'node_modules\opentype.js\dist\opentype.mjs'
if (-not (Test-Path $opentype)) {
  New-Item -ItemType Directory -Force -Path $tools | Out-Null
  npm install --prefix $tools opentype.js puppeteer-core
}
$env:MPS_DIAGRAM_TOOLS = $tools
Push-Location $repo
try {
  node (Join-Path $PSScriptRoot 'render.mjs') @args
  if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
finally {
  Pop-Location
}
