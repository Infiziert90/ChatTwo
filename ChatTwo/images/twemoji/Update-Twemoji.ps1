$ErrorActionPreference = "Stop"

$twemojiRepo = "https://github.com/twitter/twemoji.git"
$emojibaseRepo = "https://github.com/milesj/emojibase.git"

# Clone the Twemoji repo to a temporary directory
$tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ([System.IO.Path]::GetRandomFileName())
New-Item -ItemType Directory -Path $tempDir | Out-Null
Write-Host "Cloning twemoji to '$tempDir'..."
git clone --depth=1 $twemojiRepo $tempDir
if ($LASTEXITCODE -ne 0) { throw "Failed to clone Twemoji repository." }
$commitSha = (git -C $tempDir rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw "Failed to resolve Twemoji commit SHA." }

# Copy the SVG files from the Twemoji repo to the local images/twemoji/assets directory
$sourceDir = Join-Path $tempDir "assets/svg"
$destinationDir = Resolve-Path (Join-Path $PSScriptRoot "assets")
Write-Host "Deleting existing SVG files in '$destinationDir'..."
Get-ChildItem -Path $destinationDir -Filter "*.svg" -Recurse | Remove-Item -Force
Write-Host "Copying SVG files from '$sourceDir' to '$destinationDir'..."
Copy-Item -Path (Join-Path $sourceDir "*.svg") -Destination $destinationDir -Recurse -Force

# Delete the temporary Twemoji directory
Write-Host "Deleting temporary directory '$tempDir'..."
Remove-Item -Path $tempDir -Recurse -Force

# Update the commit SHA for Twemoji in README.md
$readmePath = Join-Path $PSScriptRoot "README.md"
$readmeContent = Get-Content -Path $readmePath
$readmeContent = $readmeContent -replace "(?<=twemoji/commit/)[a-f0-9]+", $commitSha
Set-Content -Path $readmePath -Value $readmeContent

# Clone the Emojibase repo to a temporary directory
$tempDirEmojibase = Join-Path ([System.IO.Path]::GetTempPath()) ([System.IO.Path]::GetRandomFileName())
New-Item -ItemType Directory -Path $tempDirEmojibase | Out-Null
Write-Host "Cloning emojibase to '$tempDirEmojibase'..."
git clone --depth=1 $emojibaseRepo $tempDirEmojibase
if ($LASTEXITCODE -ne 0) { throw "Failed to clone Emojibase repository." }
$commitShaEmojibase = (git -C $tempDirEmojibase rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw "Failed to resolve Emojibase commit SHA." }

# Copy the emojibase.raw.json file from the Emojibase repo to the images/twemoji directory
# TODO: support more than just English shortcodes in the future
$sourceFile = Join-Path $tempDirEmojibase "packages/data/en/shortcodes/emojibase.raw.json"
$destinationFile = Join-Path $PSScriptRoot "emojibase.raw.en.json"
Write-Host "Copying emojibase.raw.json from '$sourceFile' to '$destinationFile'..."
Copy-Item -Path $sourceFile -Destination $destinationFile -Force

# Delete the temporary Emojibase directory
Write-Host "Deleting temporary directory '$tempDirEmojibase'..."
Remove-Item -Path $tempDirEmojibase -Recurse -Force

# Update the commit SHA for Emojibase in README.md
$readmeContent = Get-Content -Path $readmePath
$readmeContent = $readmeContent -replace "(?<=emojibase/commit/)[a-f0-9]+", $commitShaEmojibase
Set-Content -Path $readmePath -Value $readmeContent
