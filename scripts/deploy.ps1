. "$PSScriptRoot\env.ps1"

$ImageTag = "v$((Get-Date).Ticks)"
$ImageUri = "cr.yandex/$env:REGISTRY_ID/$env:CONTAINER_NAME`:$ImageTag"

docker build -t $ImageUri .\FinalTask
docker push $ImageUri


yc serverless container revision deploy `
  --container-name $env:CONTAINER_NAME `
  --image $ImageUri `
  --cores 1 `
  --memory 256MB `
  --concurrency 1 `
  --execution-timeout 30s `
  --service-account-id $env:SERVICE_ACCOUNT_ID `
  --environment YDB_ENDPOINT=$env:YDB_ENDPOINT,YDB_DATABASE=$env:YDB_DATABASE,APP_VERSION=$ImageTag,ASPNETCORE_URLS=http://+:8080 `
  --folder-id $env:FOLDER_ID


$ContainerId = (yc serverless container get $env:CONTAINER_NAME --format json | ConvertFrom-Json).id

yc storage s3 cp .\front\index.html "s3://$env:BUCKET_NAME/index.html"

$OpenApiContent = Get-Content .\openapi.yaml -Raw
$OpenApiContent = $OpenApiContent.Replace('${CONTAINER_ID}', $ContainerId)
$OpenApiContent = $OpenApiContent.Replace('${SERVICE_ACCOUNT_ID}', $env:SERVICE_ACCOUNT_ID)
$OpenApiContent = $OpenApiContent.Replace('${BUCKET_NAME}', $env:BUCKET_NAME)

Set-Content -Path .\openapi_rendered.yaml -Value $OpenApiContent -Encoding UTF8

yc serverless api-gateway update --name $env:GATEWAY_NAME --spec=openapi_rendered.yaml --folder-id $env:FOLDER_ID

$Domain = (yc serverless api-gateway get $env:GATEWAY_NAME --format json | ConvertFrom-Json).domain
Write-Host "URL: https://$Domain" -ForegroundColor Green