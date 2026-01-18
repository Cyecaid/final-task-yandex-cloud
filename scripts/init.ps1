. "$PSScriptRoot\env.ps1"


$Container = yc serverless container get $env:CONTAINER_NAME --format json | ConvertFrom-Json

if (-not $Container) {
    exit
}

$ImageUrl = $null

if ($env:IMAGE_URL) {
    $ImageUrl = $env:IMAGE_URL
}

elseif ($Container.revision_id) {
    $Revision = yc serverless container revision get $Container.revision_id --format json | ConvertFrom-Json
    $ImageUrl = $Revision.image.image_url
    
    if (-not $ImageUrl) { $ImageUrl = $Revision.image.imageUrl }
}

yc serverless container revision deploy `
  --container-name $env:CONTAINER_NAME `
  --image $ImageUrl `
  --service-account-id $env:SERVICE_ACCOUNT_ID `
  --environment YDB_ENDPOINT=$env:YDB_ENDPOINT,YDB_DATABASE=$env:YDB_DATABASE `
  --command "dotnet" `
  --args "FinalTask.dll" `
  --args "--migrate" `
  --folder-id $env:FOLDER_ID
  
$Domain = (yc serverless api-gateway get $env:GATEWAY_NAME --format json | ConvertFrom-Json).domain

$Url = "https://$Domain/api/info" 

try { 
  Invoke-WebRequest -Uri $Url -Method Get -UseBasicParsing -TimeoutSec 10 | Out-Null 
} 
catch { 

}